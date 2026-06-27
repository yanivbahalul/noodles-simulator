using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Pages;

public class IndexModel : PageModel
{
    private readonly AuthService _authService;
    private readonly SupabaseStorageService _storage; // may be null if not configured
    private readonly EmailService _emailService;
    private readonly QuestionStatsService _stats;
    private readonly UserProgressService _userProgress;
    private readonly AchievementService _achievements;
    private readonly QuestionDifficultyService _difficultyService;
    private readonly ActivityEventService _activityEvents;
    private readonly UserFeedbackService _feedbackService;

    private static List<string> _localImagesCache;
    private static DateTime _localImagesCachedAt;
    private static readonly TimeSpan _localImagesTtl = TimeSpan.FromMinutes(2);
    private static List<List<string>> _storageGroupsCache;
    private static DateTime _storageGroupsCachedAt;
    private static readonly TimeSpan _storageGroupsTtl = TimeSpan.FromMinutes(30);
    // Prevent the same question (first image in a group of 5) from showing >3 times/hour
    private static readonly object _questionRateLock = new object();
    private static readonly Dictionary<string, List<DateTime>> _questionShownTimes = new Dictionary<string, List<DateTime>>();

    // Shuffle-bag to ensure we cycle through all groups before repeating
    private static readonly object _bagLock = new object();
    private static List<int> _bagOrder;
    private static int _bagIndex = 0;
    private static int _bagSourceCount = 0;
    private static DateTime _bagBuiltAt;
    private static readonly TimeSpan _bagTtl = TimeSpan.FromMinutes(30);
    private static readonly Dictionary<string, int> _groupShownCount = new Dictionary<string, int>();

    // Debug snapshots for diagnostics endpoint
    public static (int trackedQuestions, int throttledNow) GetThrottleSnapshot()
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(-1);
        lock (_questionRateLock)
        {
            int throttled = 0;
            foreach (var kv in _questionShownTimes)
            {
                var list = kv.Value;
                list.RemoveAll(t => t < cutoff);
                if (list.Count >= 3) throttled++;
            }
            return (_questionShownTimes.Count, throttled);
        }
    }

    public static Dictionary<int, int> GetGroupShownHistogramSnapshot()
    {
        lock (_bagLock)
        {
            var hist = new Dictionary<int, int>();
            foreach (var kv in _groupShownCount)
            {
                var c = kv.Value;
                if (!hist.ContainsKey(c)) hist[c] = 0;
                hist[c]++;
            }
            return hist;
        }
    }

    public IndexModel(AuthService authService, SupabaseStorageService storage = null, EmailService emailService = null, QuestionStatsService stats = null, UserProgressService userProgress = null, AchievementService achievements = null, QuestionDifficultyService difficultyService = null, ActivityEventService activityEvents = null, UserFeedbackService feedbackService = null)
    {
        _authService = authService;
        _storage = storage;
        _emailService = emailService;
        _stats = stats;
        _userProgress = userProgress;
        _achievements = achievements;
        _difficultyService = difficultyService;
        _activityEvents = activityEvents;
        _feedbackService = feedbackService;
    }

    public bool AnswerChecked { get; set; }
    public bool IsCorrect { get; set; }
    public string SelectedAnswer { get; set; }
    public string QuestionImage { get; set; }
    public Dictionary<string, string> ShuffledAnswers { get; set; }
    /// <summary>Opaque correct option key — server-side only, for post-answer UI.</summary>
    public string CorrectAnswerKey { get; set; }
    public string Username { get; set; }
    public int OnlineCount { get; set; }
    public string ActiveNoticeId { get; set; } = "";
    public bool ShowFeedbackModal { get; set; }
    public string FeedbackCampaignId { get; set; } = "";
    public bool ShowGitHubStarModal { get; set; }
    public int GitHubStarMilestone { get; set; }

    public int CurrentStreak { get; set; }
    public int UserCorrect { get; set; }
    public int UserTotal { get; set; }
    public int UserSuccessRate { get; set; }
    public int UserXp { get; set; }
    public int UserLevel { get; set; }
    public int XpProgressPercent { get; set; }
    public string PracticeMode { get; set; } = "normal";
    public string PracticeDifficulty { get; set; } = "";
    public List<string> NewlyUnlockedAchievements { get; set; } = new();
    public bool IsDailyComplete { get; set; }
    public int DailyProgress { get; set; }
    public int DailyTotal { get; set; } = 10;

    // Holds signed URLs for current question and answers for rendering
    public string QuestionImageUrl { get; set; }
    public Dictionary<string, string> AnswerImageUrls { get; set; }
    
    // Store original file names for reporting purposes
    public string QuestionImageOriginalName { get; set; }
    public Dictionary<string, string> AnswerImageOriginalNames { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            Username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(Username))
                return RedirectToPage("/Login");

            if (HttpContext.Session.GetString("SessionStart") == null)
            {
                HttpContext.Session.SetString("SessionStart", DateTime.UtcNow.ToString());
                HttpContext.Session.SetInt32("RapidTotal", 0);
                HttpContext.Session.SetInt32("RapidCorrect", 0);
            }

            var userTask = _authService.GetUserAsync(Username);
            var onlineTask = _authService.GetOnlineUserCountAsync();
            await Task.WhenAll(userTask, onlineTask);

            User user = null;
            try { user = await userTask; }
            catch (Exception ex) { Console.WriteLine($"[OnGetAsync GetUserAsync Error] {ex.Message}"); }

            try { OnlineCount = await onlineTask; }
            catch (HttpRequestException ex) { Console.WriteLine($"[OnGetAsync GetOnlineUserCount Error] {ex.Message}"); OnlineCount = 0; }

            if (user != null)
            {
                if (user.IsBanned)
                {
                    HttpContext.Session.Clear();
                    RememberMeService.Clear(Response);
                    return RedirectToPage("/Login");
                }
                _ = _authService.TouchLastSeenAsync(user.Username, DateTime.UtcNow);
                ActiveNoticeId = AppNotices.GetFirstUndismissed(user.DismissedNotices) ?? "";

                var isAdmin = string.Equals(HttpContext.Session.GetString("IsAdmin"), "1", StringComparison.Ordinal);
                var campaignId = FeedbackCampaigns.GetCampaignIdForUser(DateTime.UtcNow, isAdmin);
                if (!string.IsNullOrEmpty(campaignId) && _feedbackService != null && _feedbackService.IsEnabled)
                {
                    var alreadySubmitted = await _feedbackService.HasSubmittedAsync(user.Username, campaignId);
                    var achievementCount = GetUnlockedAchievementCount(user.Username);
                    if (!alreadySubmitted && FeedbackCampaigns.IsEligible(achievementCount))
                    {
                        ShowFeedbackModal = true;
                        FeedbackCampaignId = campaignId;
                    }
                }

                if (GitHubStarPrompt.ShouldPrompt(user))
                {
                    ShowGitHubStarModal = true;
                    GitHubStarMilestone = user.TotalAnswered;
                }
            }

            ApplyPracticeQueryParams();
            if (ShouldClearAnswerFlashOnGet())
                ClearAnswerFlash();

            await PopulateUserStatsAsync(user);
            LoadPendingAchievements();
            if (!await TryRestoreAnswerFlashAsync())
            {
                try { await LoadRandomQuestionAsync(); }
                catch (Exception ex) { Console.WriteLine($"[OnGetAsync PreloadQuestion Error] {ex.Message}"); }
            }
            return Page();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnGetAsync Error] {ex}");
            return RedirectToPage("/Login");
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            if (Request.Form.ContainsKey("logout"))
                return HandleLogoutPost();

            var auth = await TryRequireAuthenticatedUserAsync();
            if (auth.Redirect != null)
                return auth.Redirect;

            if (Request.Form.ContainsKey("reset"))
                return await HandleResetPostAsync(auth.User);

            if (!Request.Form.ContainsKey("answer"))
            {
                ClearAnswerFlash();
                ApplyPracticeQueryParams();
                try { await LoadRandomQuestionAsync(); }
                catch (Exception ex) { Console.WriteLine($"[OnPostAsync ReloadQuestion Error] {ex.Message}"); }
                await PopulateUserStatsAsync(auth.User);
                return Page();
            }

            await ProcessSubmittedAnswerAsync(auth.User);
            var cheaterRedirect = await TryHandleCheaterDetectionAsync(auth.User);
            if (cheaterRedirect != null)
                return cheaterRedirect;

            SaveAnswerFlashToSession();
            return RedirectToPage("/Index");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnPostAsync Error] {ex}");
            return StatusCode(500, "Server error");
        }
    }

    public async Task<IActionResult> OnPostReportErrorAsync()
    {
        try
        {
            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                body = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
                return new JsonResult(new { error = "Empty body" }) { StatusCode = 400 };

            using var doc = JsonDocument.Parse(body);
            var payload = ErrorReportBuilder.TryParse(doc, HttpContext.Session.GetString("Username"));
            if (payload == null)
                return new JsonResult(new { error = "Invalid body" }) { StatusCode = 400 };

            EnrichReportFromSession(payload);

            var request = HttpContext.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";
            var htmlBody = ErrorReportBuilder.BuildHtmlBody(payload, baseUrl);
            var reportSubject = ErrorReportBuilder.BuildSubject(payload.Username);

            await TrySendReportEmailAsync(reportSubject, htmlBody);
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnPostReportErrorAsync Error] {ex}");
            return new JsonResult(new { error = "Server error" }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> OnPostSubmitAnswerAsync()
    {
        try
        {
            var auth = await TryRequireAuthenticatedUserAsync();
            if (auth.Redirect != null)
                return new JsonResult(new { error = "Unauthorized", redirect = "/Login" }) { StatusCode = 401 };

            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                body = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
                return new JsonResult(new { error = "Empty body" }) { StatusCode = 400 };

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("questionImage", out var qEl) || !root.TryGetProperty("answer", out var aEl))
                return new JsonResult(new { error = "Invalid body" }) { StatusCode = 400 };

            var questionImage = qEl.GetString();
            var answer = aEl.GetString();
            if (string.IsNullOrWhiteSpace(questionImage) || string.IsNullOrWhiteSpace(answer))
                return new JsonResult(new { error = "Invalid body" }) { StatusCode = 400 };

            var lastSubmitted = HttpContext.Session.GetString(LastSubmittedQuestionKey);
            if (!string.IsNullOrEmpty(lastSubmitted)
                && string.Equals(lastSubmitted, questionImage, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(HttpContext.Session.GetString(FlashQuestionKey)))
            {
                await TryRestoreAnswerFlashAsync();
                await PopulateUserStatsAsync(auth.User);
                NewlyUnlockedAchievements = new List<string>();
                return new JsonResult(BuildSubmitAnswerResponse());
            }

            ParseSubmittedAnswer(questionImage, answer);
            if (string.IsNullOrEmpty(CorrectAnswerKey) || ShuffledAnswers == null || ShuffledAnswers.Count == 0)
            {
                await LoadPracticeQuestionFromSessionAsync();
                if (string.Equals(HttpContext.Session.GetString(PracticeQuestionKey), questionImage, StringComparison.OrdinalIgnoreCase))
                    TryHydrateModelFromPracticeSession();
            }

            await ProcessSubmittedAnswerCoreAsync(auth.User);

            var cheaterRedirect = await TryHandleCheaterDetectionAsync(auth.User);
            var redirectPath = GetRedirectPath(cheaterRedirect);
            if (redirectPath != null)
                return new JsonResult(new { redirect = redirectPath });

            SaveAnswerFlashToSession();
            ClearPrefetch();
            await PopulateUserStatsAsync(auth.User);
            ShowGitHubStarModal = GitHubStarPrompt.ShouldPrompt(auth.User);
            if (ShowGitHubStarModal)
                GitHubStarMilestone = auth.User.TotalAnswered;
            await PopulateUrlsAsync();
            return new JsonResult(BuildSubmitAnswerResponse());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnPostSubmitAnswerAsync Error] {ex}");
            return new JsonResult(new { error = "Server error" }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> OnGetNextQuestionAsync()
    {
        try
        {
            var auth = await TryRequireAuthenticatedUserAsync();
            if (auth.Redirect != null)
                return new JsonResult(new { error = "Unauthorized", redirect = "/Login" }) { StatusCode = 401 };

            ClearAnswerFlash();
            if (!await TryPromotePrefetchAsync())
                await LoadRandomQuestionAsync();
            else
                SavePracticeQuestionState();
            await PopulateUserStatsAsync(auth.User);
            return new JsonResult(BuildNextQuestionResponse());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnGetNextQuestionAsync Error] {ex}");
            return new JsonResult(new { error = "Server error" }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> OnGetPrefetchNextQuestionAsync()
    {
        try
        {
            var auth = await TryRequireAuthenticatedUserAsync();
            if (auth.Redirect != null)
                return new JsonResult(new { error = "Unauthorized", redirect = "/Login" }) { StatusCode = 401 };

            await BuildPrefetchIfNeededAsync();
            if (!HasValidPrefetch())
                return new StatusCodeResult(204);

            await PopulateUserStatsAsync(auth.User);
            return new JsonResult(await BuildPrefetchResponseAsync());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnGetPrefetchNextQuestionAsync Error] {ex}");
            return new JsonResult(new { error = "Server error" }) { StatusCode = 500 };
        }
    }

    private Task PopulateUserStatsAsync(User user)
    {
        if (user == null) return Task.CompletedTask;
        CurrentStreak = HttpContext.Session.GetInt32("CurrentStreak") ?? 0;

        if (_userProgress != null)
        {
            var progress = _userProgress.Load(user.Username);
            var (progTotal, progCorrect) = _userProgress.GetAnswerTotals(user.Username);
            UserCorrect = Math.Max(user.CorrectAnswers, progCorrect);
            UserTotal = Math.Max(user.TotalAnswered, progTotal);
            UserXp = Math.Max(user.Xp, progress.Xp);
            user.Xp = UserXp;
            user.Level = QuizGamification.LevelFromXp(UserXp);
            UserLevel = user.Level;
            XpProgressPercent = QuizGamification.XpProgressPercent(UserXp);
        }
        else
        {
            UserCorrect = user.CorrectAnswers;
            UserTotal = user.TotalAnswered;
            UserXp = user.Xp;
            UserLevel = user.Level > 0 ? user.Level : QuizGamification.LevelFromXp(user.Xp);
            XpProgressPercent = QuizGamification.XpProgressPercent(UserXp);
        }

        UserSuccessRate = UserTotal > 0 ? (int)((double)UserCorrect / UserTotal * 100) : 0;

        PracticeMode = HttpContext.Session.GetString("PracticeMode") ?? "normal";
        PracticeDifficulty = HttpContext.Session.GetString("PracticeDifficulty") ?? "";

        if (PracticeMode == "daily")
        {
            DailyProgress = HttpContext.Session.GetInt32("DailyQuestionIndex") ?? 0;
            var dailyDate = HttpContext.Session.GetString("DailyDate") ?? "";
            IsDailyComplete = dailyDate == UserProgressService.TodayKey() && DailyProgress >= DailyTotal;
        }
        return Task.CompletedTask;
    }

    private void ApplyPracticeQueryParams()
    {
        var mode = Request.Query["mode"].ToString();
        var difficulty = Request.Query["difficulty"].ToString();

        if (!string.IsNullOrWhiteSpace(mode))
        {
            HttpContext.Session.SetString("PracticeMode", mode);
            if (mode != "normal")
                HttpContext.Session.Remove("PracticeDifficulty");
            else if (string.IsNullOrWhiteSpace(difficulty))
                HttpContext.Session.Remove("PracticeDifficulty");
        }

        if (!string.IsNullOrWhiteSpace(difficulty))
        {
            HttpContext.Session.SetString("PracticeDifficulty", difficulty);
            HttpContext.Session.SetString("PracticeMode", "normal");
        }

        if (mode == "daily")
            EnsureDailyChallengeSession();
    }

    private int GetUnlockedAchievementCount(string username)
    {
        if (_userProgress == null || string.IsNullOrWhiteSpace(username))
            return 0;

        var achievements = _userProgress.Load(username)?.Achievements;
        return achievements?.Count ?? 0;
    }

    private void LoadPendingAchievements()
    {
        var json = HttpContext.Session.GetString("PendingAchievements");
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            NewlyUnlockedAchievements = JsonSerializer.Deserialize<List<string>>(json, AppJson.Options) ?? new List<string>();
            HttpContext.Session.Remove("PendingAchievements");
        }
        catch { /* ignore */ }
    }

    private const string FlashQuestionKey = "AnswerFlash_QuestionImage";
    private const string FlashSelectedKey = "AnswerFlash_SelectedAnswer";
    private const string FlashAnswersKey = "AnswerFlash_AnswersJson";
    private const string FlashCorrectKey = "AnswerFlash_IsCorrect";
    private const string FlashCorrectKeyKey = "AnswerFlash_CorrectKey";
    private const string PracticeQuestionKey = "Practice_QuestionImage";
    private const string PracticeOptionsKey = "Practice_OptionsJson";
    private const string PracticeCorrectKey = "Practice_CorrectKey";
    private const string PrefetchQuestionKey = "Practice_Prefetch_QuestionImage";
    private const string PrefetchOptionsKey = "Practice_Prefetch_OptionsJson";
    private const string PrefetchCorrectKey = "Practice_Prefetch_CorrectKey";
    private const string PrefetchAnchorKey = "Practice_Prefetch_Anchor";
    private const string LastSubmittedQuestionKey = "LastSubmittedQuestion";

    private void SaveAnswerFlashToSession()
    {
        HttpContext.Session.SetString(FlashQuestionKey, QuestionImage ?? "");
        HttpContext.Session.SetString(FlashSelectedKey, SelectedAnswer ?? "");
        HttpContext.Session.SetString(FlashAnswersKey,
            JsonSerializer.Serialize(ShuffledAnswers ?? new Dictionary<string, string>(), AppJson.Options));
        HttpContext.Session.SetString(FlashCorrectKeyKey, CorrectAnswerKey ?? "");
        HttpContext.Session.SetString(FlashCorrectKey, IsCorrect ? "1" : "0");
        HttpContext.Session.SetString(LastSubmittedQuestionKey, QuestionImage ?? "");
    }

    private static bool ShouldClearAnswerFlashOnGet(HttpRequest request)
    {
        if (request.Query.ContainsKey("next"))
            return true;
        if (!string.IsNullOrEmpty(request.Query["mode"]))
            return true;
        return !string.IsNullOrEmpty(request.Query["difficulty"]);
    }

    private bool ShouldClearAnswerFlashOnGet() => ShouldClearAnswerFlashOnGet(Request);

    private void ClearAnswerFlash()
    {
        HttpContext.Session.Remove(FlashQuestionKey);
        HttpContext.Session.Remove(FlashSelectedKey);
        HttpContext.Session.Remove(FlashAnswersKey);
        HttpContext.Session.Remove(FlashCorrectKey);
        HttpContext.Session.Remove(FlashCorrectKeyKey);
    }

    private async Task<bool> TryRestoreAnswerFlashAsync()
    {
        var questionImage = HttpContext.Session.GetString(FlashQuestionKey);
        if (string.IsNullOrWhiteSpace(questionImage))
            return false;

        QuestionImage = questionImage;
        SelectedAnswer = HttpContext.Session.GetString(FlashSelectedKey) ?? "";
        IsCorrect = HttpContext.Session.GetString(FlashCorrectKey) == "1";
        AnswerChecked = true;

        var answersJson = HttpContext.Session.GetString(FlashAnswersKey);
        try
        {
            ShuffledAnswers = DeserializeAnswerOptions(answersJson);
        }
        catch (JsonException)
        {
            ShuffledAnswers = new Dictionary<string, string>();
        }

        CorrectAnswerKey = HttpContext.Session.GetString(FlashCorrectKeyKey) ?? "";

        await PopulateUrlsAsync();
        SavePracticeQuestionState();
        return true;
    }

    private void EnsureDailyChallengeSession()
    {
        var today = UserProgressService.TodayKey();
        var existingDate = HttpContext.Session.GetString("DailyDate");
        if (existingDate == today) return;

        HttpContext.Session.SetString("DailyDate", today);
        HttpContext.Session.SetInt32("DailyQuestionIndex", 0);
        HttpContext.Session.SetInt32("DailyScore", 0);
        HttpContext.Session.Remove("DailyQuestions");
    }

    private IActionResult HandleLogoutPost()
    {
        HttpContext.Session.Clear();
        RememberMeService.Clear(Response);
        return RedirectToPage("/Login");
    }

    private async Task<(User User, IActionResult Redirect)> TryRequireAuthenticatedUserAsync()
    {
        Username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(Username))
            return (null, RedirectToPage("/Login"));

        User user = null;
        try { user = await _authService.GetUserAsync(Username); }
        catch (Exception ex) { Console.WriteLine($"[OnPostAsync GetUserAsync Error] {ex.Message}"); }
        if (user == null)
            return (null, RedirectToPage("/Login"));

        if (user.IsBanned)
        {
            HttpContext.Session.Clear();
            RememberMeService.Clear(Response);
            return (null, RedirectToPage("/Login"));
        }

        return (user, null);
    }

    private async Task<IActionResult> HandleResetPostAsync(User user)
    {
        user.CorrectAnswers = 0;
        user.TotalAnswered = 0;
        user.IsCheater = false;
        user.Xp = 0;
        user.Level = 1;
        user.WeeklyCorrect = 0;
        user.WeekKey = UserProgressService.GetWeekKey();
        user.DailyCorrect = 0;
        user.DayKey = UserProgressService.TodayKey();
        user.DailyChallengeScore = 0;
        user.DailyChallengeDate = "";
        user.BestExamScore = 0;
        user.BestExamCorrect = 0;

        try { await _authService.UpdateUserAsync(user); }
        catch (Exception ex) { Console.WriteLine($"[OnPostAsync Reset UpdateUserAsync Error] {ex.Message}"); }

        try
        {
            await _authService.SyncLeaderboardStatsAsync(
                user.Username, 0, user.WeekKey, 0, user.DayKey, 0, "", 0, 0);
        }
        catch (Exception ex) { Console.WriteLine($"[OnPostAsync Reset SyncLeaderboardStatsAsync Error] {ex.Message}"); }

        try { _userProgress?.ResetAll(user.Username); }
        catch (Exception ex) { Console.WriteLine($"[OnPostAsync Reset UserProgress Error] {ex.Message}"); }

        ClearQuizProgressSession();
        _activityEvents?.Log(user.Username, "progress_reset");

        return RedirectToPage("/Index");
    }

    private void ClearQuizProgressSession()
    {
        HttpContext.Session.SetInt32("CurrentStreak", 0);
        HttpContext.Session.Remove("RecentQuestions");
        HttpContext.Session.Remove("DailyDate");
        HttpContext.Session.Remove("DailyQuestionIndex");
        HttpContext.Session.Remove("DailyScore");
        HttpContext.Session.Remove("DailyQuestions");
        HttpContext.Session.Remove("PendingAchievements");
        HttpContext.Session.Remove("CheaterCount");
        HttpContext.Session.SetInt32("RapidTotal", 0);
        HttpContext.Session.SetInt32("RapidCorrect", 0);
        HttpContext.Session.SetString("SessionStart", DateTime.UtcNow.ToString("o"));
        HttpContext.Session.Remove(LastSubmittedQuestionKey);
        ClearAnswerFlash();
    }

    private async Task ProcessSubmittedAnswerAsync(User user)
    {
        ParseSubmittedAnswerForm();
        await ProcessSubmittedAnswerCoreAsync(user);
    }

    private async Task ProcessSubmittedAnswerCoreAsync(User user)
    {
        ApplyAnswerToUserStats(user);

        var streak = UpdateAnswerStreak();
        var practiceMode = HttpContext.Session.GetString("PracticeMode") ?? "normal";
        var practiceDifficulty = HttpContext.Session.GetString("PracticeDifficulty") ?? "";
        var xpGain = IsCorrect ? XpGainForCorrectAnswer(practiceMode, practiceDifficulty) : 0;

        RecordPracticeProgress(user, streak, practiceMode, practiceDifficulty, xpGain);
        SyncUserLevelFromProgress(user);
        await FinalizeAnswerSubmissionAsync(user, streak);
    }

    private void ParseSubmittedAnswerForm()
    {
        ParseSubmittedAnswer(
            Request.Form["questionImage"].ToString(),
            Request.Form["answer"].ToString());
    }

    private void ParseSubmittedAnswer(string questionImage, string answer)
    {
        SelectedAnswer = answer;
        AnswerChecked = true;
        QuestionImage = questionImage;

        if (!TryLoadQuestionStateForSubmit(questionImage))
        {
            IsCorrect = false;
            CorrectAnswerKey = "";
            ShuffledAnswers = new Dictionary<string, string>();
            return;
        }

        SavePracticeQuestionState();

        if (string.IsNullOrEmpty(CorrectAnswerKey) || !ShuffledAnswers.ContainsKey(answer))
        {
            IsCorrect = false;
            return;
        }

        IsCorrect = string.Equals(answer, CorrectAnswerKey, StringComparison.Ordinal);
    }

    private bool TryLoadQuestionStateForSubmit(string questionImage)
    {
        EnsurePracticeHydratedForQuestion(questionImage);

        var sessionQuestion = HttpContext.Session.GetString(PracticeQuestionKey);
        if (string.Equals(sessionQuestion, questionImage, StringComparison.OrdinalIgnoreCase)
            && TryHydrateModelFromPracticeSession())
            return true;

        return string.Equals(HttpContext.Session.GetString(FlashQuestionKey), questionImage, StringComparison.OrdinalIgnoreCase)
               && TryHydrateModelFromFlash(persistToPractice: true);
    }

    private void EnsurePracticeHydratedForQuestion(string questionImage)
    {
        var sessionQuestion = HttpContext.Session.GetString(PracticeQuestionKey);
        if (string.Equals(sessionQuestion, questionImage, StringComparison.OrdinalIgnoreCase))
            return;

        TryHydrateModelFromFlash(persistToPractice: true);
    }

    private bool TryHydrateModelFromPracticeSession()
    {
        var optionsJson = HttpContext.Session.GetString(PracticeOptionsKey);
        CorrectAnswerKey = HttpContext.Session.GetString(PracticeCorrectKey) ?? "";
        ShuffledAnswers = DeserializeAnswerOptions(optionsJson);
        return !string.IsNullOrEmpty(CorrectAnswerKey) && ShuffledAnswers.Count > 0;
    }

    private bool TryHydrateModelFromFlash(bool persistToPractice)
    {
        var optionsJson = HttpContext.Session.GetString(FlashAnswersKey);
        var correctKey = HttpContext.Session.GetString(FlashCorrectKeyKey);
        if (string.IsNullOrWhiteSpace(optionsJson) || string.IsNullOrWhiteSpace(correctKey))
            return false;

        ShuffledAnswers = DeserializeAnswerOptions(optionsJson);
        CorrectAnswerKey = correctKey;

        if (persistToPractice)
        {
            HttpContext.Session.SetString(PracticeQuestionKey, HttpContext.Session.GetString(FlashQuestionKey) ?? "");
            HttpContext.Session.SetString(PracticeOptionsKey, optionsJson);
            HttpContext.Session.SetString(PracticeCorrectKey, correctKey);
        }

        return ShuffledAnswers.Count > 0;
    }

    private static Dictionary<string, string> DeserializeAnswerOptions(string optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
            return new Dictionary<string, string>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(optionsJson, AppJson.Options)
                   ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private void SavePracticeQuestionState()
    {
        HttpContext.Session.SetString(PracticeQuestionKey, QuestionImage ?? "");
        HttpContext.Session.SetString(PracticeOptionsKey,
            JsonSerializer.Serialize(ShuffledAnswers ?? new Dictionary<string, string>(), AppJson.Options));
        HttpContext.Session.SetString(PracticeCorrectKey, CorrectAnswerKey ?? "");
    }

    private void EnrichReportFromSession(ErrorReportBuilder.ReportPayload payload)
    {
        var flashQuestion = HttpContext.Session.GetString(FlashQuestionKey);
        if (!string.Equals(flashQuestion, payload.QuestionImage, StringComparison.Ordinal))
            return;

        var optionsJson = HttpContext.Session.GetString(FlashAnswersKey);
        var correctKey = HttpContext.Session.GetString(FlashCorrectKeyKey);
        if (string.IsNullOrWhiteSpace(optionsJson) || string.IsNullOrWhiteSpace(correctKey))
            return;

        try
        {
            var options = JsonSerializer.Deserialize<Dictionary<string, string>>(optionsJson, AppJson.Options)
                          ?? new Dictionary<string, string>();
            payload.AnswersDict = options;
            if (options.TryGetValue(correctKey, out var correctFile))
                payload.CorrectAnswer = correctFile;
        }
        catch (JsonException) { /* ignore */ }

        var selected = HttpContext.Session.GetString(FlashSelectedKey);
        if (!string.IsNullOrWhiteSpace(selected) && payload.AnswersDict.TryGetValue(selected, out var selectedFile))
            payload.SelectedAnswer = selectedFile;
    }

    private void ApplyAnswerToUserStats(User user)
    {
        user.TotalAnswered++;
        if (IsCorrect)
        {
            user.CorrectAnswers++;
            if (_storage == null)
            {
                try { MoveCorrectImagesLocal(); }
                catch (Exception ex) { Console.WriteLine($"[MoveCorrectImagesLocal Error] {ex}"); }
            }
        }

        try { _stats?.Record(QuestionImage, IsCorrect); }
        catch (Exception ex) { Console.WriteLine($"[OnPostAsync RecordStats Error] {ex.Message}"); }
    }

    private int UpdateAnswerStreak()
    {
        var streak = HttpContext.Session.GetInt32("CurrentStreak") ?? 0;
        if (IsCorrect)
        {
            streak++;
            HttpContext.Session.SetInt32("CurrentStreak", streak);
        }
        else
        {
            streak = 0;
            HttpContext.Session.SetInt32("CurrentStreak", 0);
        }

        CurrentStreak = streak;
        return streak;
    }

    private void RecordPracticeProgress(User user, int streak, string practiceMode, string practiceDifficulty, int xpGain)
    {
        if (_userProgress == null) return;

        _userProgress.RecordAnswer(user.Username, QuestionImage, IsCorrect, xpGain);
        _userProgress.UpdateBestStreak(user.Username, streak);

        if (IsCorrect)
            RecordCorrectAnswerProgress(user, practiceMode, practiceDifficulty);

        if (practiceMode == "daily")
            RecordDailyChallengeProgress(user);

        _activityEvents?.Log(user.Username, "answer", new Dictionary<string, object>
        {
            ["questionId"] = QuestionImage ?? "",
            ["correct"] = IsCorrect,
            ["mode"] = practiceMode ?? "normal",
            ["difficulty"] = practiceDifficulty ?? "easy"
        });
    }

    private void RecordCorrectAnswerProgress(User user, string practiceMode, string practiceDifficulty)
    {
        if (practiceMode == "weak")
            _userProgress.IncrementWeakCorrect(user.Username);
        if (practiceDifficulty == "hard")
            _userProgress.IncrementHardCorrect(user.Username);
        if (practiceMode != "review" || _achievements == null
            || !_userProgress.RemoveSessionMistake(user.Username, QuestionImage))
            return;

        _userProgress.IncrementReviewClear(user.Username);
        NewlyUnlockedAchievements.AddRange(_achievements.CheckReviewClear(user.Username));
    }

    private void RecordDailyChallengeProgress(User user)
    {
        _userProgress.RecordDailyChallengeAnswer(user.Username, IsCorrect);
        var dailyIdx = (HttpContext.Session.GetInt32("DailyQuestionIndex") ?? 0) + 1;
        HttpContext.Session.SetInt32("DailyQuestionIndex", dailyIdx);

        if (IsCorrect)
        {
            var dailyScore = (HttpContext.Session.GetInt32("DailyScore") ?? 0) + 1;
            HttpContext.Session.SetInt32("DailyScore", dailyScore);
        }

        if (dailyIdx < DailyTotal || _achievements == null) return;

        var finalScore = HttpContext.Session.GetInt32("DailyScore") ?? 0;
        _userProgress.RecordDailyChallengeComplete(user.Username, finalScore >= DailyTotal);
        NewlyUnlockedAchievements.AddRange(_achievements.CheckDailyAchievements(user.Username));
    }

    private void SyncUserLevelFromProgress(User user)
    {
        if (_userProgress == null) return;

        var progress = _userProgress.Load(user.Username);
        user.Xp = progress.Xp;
        user.Level = QuizGamification.LevelFromXp(user.Xp);
    }

    private async Task FinalizeAnswerSubmissionAsync(User user, int streak)
    {
        if (_achievements != null)
            NewlyUnlockedAchievements.AddRange(_achievements.CheckPracticeAchievements(user.Username, streak, user.TotalAnswered, user.Xp));

        if (NewlyUnlockedAchievements.Count > 0)
            HttpContext.Session.SetString("PendingAchievements", JsonSerializer.Serialize(NewlyUnlockedAchievements, AppJson.Options));

        try { await _authService.UpdateUserAsync(user); }
        catch (Exception ex) { Console.WriteLine($"[OnPostAsync UpdateUserAsync Error] {ex.Message}"); }

        UpdateRapidAnswerCounters();
    }

    private static int XpGainForCorrectAnswer(string practiceMode, string practiceDifficulty) =>
        practiceMode == "daily"
            ? QuizGamification.DailyChallengeXpPerCorrect
            : QuizGamification.XpForDifficulty(practiceDifficulty);

    private void UpdateRapidAnswerCounters()
    {
        var sessionStartStr = HttpContext.Session.GetString("SessionStart");
        DateTime.TryParse(sessionStartStr, out var sessionStart);
        var now = DateTime.UtcNow;
        var elapsedSeconds = (now - sessionStart).TotalSeconds;

        var rapidTotal = HttpContext.Session.GetInt32("RapidTotal") ?? 0;
        var rapidCorrect = HttpContext.Session.GetInt32("RapidCorrect") ?? 0;

        if (elapsedSeconds <= 120)
        {
            HttpContext.Session.SetInt32("RapidTotal", rapidTotal + 1);
            if (IsCorrect)
                HttpContext.Session.SetInt32("RapidCorrect", rapidCorrect + 1);
        }
        else
        {
            HttpContext.Session.SetString("SessionStart", now.ToString());
            HttpContext.Session.SetInt32("RapidTotal", 1);
            HttpContext.Session.SetInt32("RapidCorrect", IsCorrect ? 1 : 0);
        }
    }

    private async Task<IActionResult> TryHandleCheaterDetectionAsync(User user)
    {
        if (IsCheaterDetectionExempt(user.Username))
            return null;

        var rapidTotal = HttpContext.Session.GetInt32("RapidTotal") ?? 0;
        var rapidCorrect = HttpContext.Session.GetInt32("RapidCorrect") ?? 0;
        if (rapidTotal < 20 && rapidCorrect < 15)
            return null;

        Console.WriteLine($"[CHEATER DETECTED] User: {user.Username} | RapidTotal: {rapidTotal} | RapidCorrect: {rapidCorrect}");
        user.IsCheater = true;
        try { await _authService.UpdateUserAsync(user); }
        catch (Exception ex) { Console.WriteLine($"[OnPostAsync CheaterMark UpdateUserAsync Error] {ex.Message}"); }

        var cheaterCount = (HttpContext.Session.GetInt32("CheaterCount") ?? 0) + 1;
        HttpContext.Session.SetInt32("CheaterCount", cheaterCount);

        if (cheaterCount >= 3)
        {
            user.IsBanned = true;
            try { await _authService.UpdateUserAsync(user); }
            catch (Exception ex) { Console.WriteLine($"[OnPostAsync Ban UpdateUserAsync Error] {ex.Message}"); }
            HttpContext.Session.Clear();
            RememberMeService.Clear(Response);
            return RedirectToPage("/Login");
        }

        HttpContext.Session.SetInt32("RapidTotal", 0);
        HttpContext.Session.SetInt32("RapidCorrect", 0);
        return RedirectToPage("/Cheater");
    }

    private static bool IsCheaterDetectionExempt(string username) =>
        !string.IsNullOrWhiteSpace(username) &&
        username.StartsWith("e2etest", StringComparison.OrdinalIgnoreCase);

    private Task TrySendReportEmailAsync(string reportSubject, string htmlBody)
    {
        try
        {
            if (_emailService == null || !_emailService.IsConfigured)
            {
                Console.WriteLine("[Report] Email service not configured, skipping email notification");
                return Task.CompletedTask;
            }

            Console.WriteLine("[Report] Sending error report email...");
            var result = _emailService.Send(reportSubject, htmlBody);
            if (result)
                Console.WriteLine("[Report] Error report email sent successfully");
            else
                Console.WriteLine("[Report] Failed to send error report email");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReportEmail Dispatch Error] {ex}");
        }

        return Task.CompletedTask;
    }

    private async Task LoadRandomQuestionAsync()
    {
        try
        {
            if (!await PickQuestionIntoModelAsync())
            {
                QuestionImage = "placeholder.jpg";
                ShuffledAnswers = new Dictionary<string, string>();
                CorrectAnswerKey = "";
                SavePracticeQuestionState();
                await PopulateUrlsAsync();
                return;
            }

            RecordQuestionShown(QuestionImage);
            IncrementGroupShown(QuestionImage);
            AddRecentQuestionToSession(QuestionImage);
            HttpContext.Session.Remove(LastSubmittedQuestionKey);
            SavePracticeQuestionState();
            await PopulateUrlsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoadRandomQuestionAsync Error] {ex.Message}");
        }
    }

    private async Task<bool> PickQuestionIntoModelAsync()
    {
        var grouped = await LoadAllQuestionGroupsAsync();
        if (grouped.Count == 0)
            return false;

        var mode = HttpContext.Session.GetString("PracticeMode") ?? "normal";
        var difficulty = HttpContext.Session.GetString("PracticeDifficulty") ?? "";
        var username = HttpContext.Session.GetString("Username") ?? "";

        List<string> chosen;

        if (mode == "daily")
        {
            chosen = await PickDailyQuestionAsync(grouped);
        }
        else if (mode == "review" && _userProgress != null)
        {
            chosen = PickReviewQuestion(grouped, username);
        }
        else
        {
            var pool = await FilterGroupsAsync(grouped, mode, difficulty, username);
            if (pool.Count == 0) pool = grouped;
            chosen = PickFromPool(pool, username, useSpaced: mode == "review");
        }

        if (chosen == null || chosen.Count < 2)
            return false;

        QuestionImage = chosen[0];
        var correct = chosen[1];
        var wrong = chosen.Skip(2).Take(3).ToList();
        var shuffled = AnswerOptionShuffle.Create(correct, wrong);
        ShuffledAnswers = shuffled.Options;
        CorrectAnswerKey = shuffled.CorrectKey;
        return true;
    }

    private bool HasValidPrefetch()
    {
        var anchor = HttpContext.Session.GetString(PrefetchAnchorKey);
        var current = HttpContext.Session.GetString(PracticeQuestionKey);
        var prefetchQuestion = HttpContext.Session.GetString(PrefetchQuestionKey);
        return !string.IsNullOrWhiteSpace(prefetchQuestion)
               && !string.IsNullOrWhiteSpace(anchor)
               && string.Equals(anchor, current, StringComparison.Ordinal);
    }

    private void ClearPrefetch()
    {
        HttpContext.Session.Remove(PrefetchQuestionKey);
        HttpContext.Session.Remove(PrefetchOptionsKey);
        HttpContext.Session.Remove(PrefetchCorrectKey);
        HttpContext.Session.Remove(PrefetchAnchorKey);
    }

    private async Task LoadPracticeQuestionFromSessionAsync()
    {
        QuestionImage = HttpContext.Session.GetString(PracticeQuestionKey) ?? "";
        CorrectAnswerKey = HttpContext.Session.GetString(PracticeCorrectKey) ?? "";
        var optionsJson = HttpContext.Session.GetString(PracticeOptionsKey);
        try
        {
            ShuffledAnswers = string.IsNullOrWhiteSpace(optionsJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(optionsJson, AppJson.Options)
                  ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            ShuffledAnswers = new Dictionary<string, string>();
        }

        await PopulateUrlsAsync();
    }

    private void ApplyPrefetchToModel()
    {
        QuestionImage = HttpContext.Session.GetString(PrefetchQuestionKey) ?? "";
        CorrectAnswerKey = HttpContext.Session.GetString(PrefetchCorrectKey) ?? "";
        var optionsJson = HttpContext.Session.GetString(PrefetchOptionsKey);
        try
        {
            ShuffledAnswers = string.IsNullOrWhiteSpace(optionsJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(optionsJson, AppJson.Options)
                  ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            ShuffledAnswers = new Dictionary<string, string>();
        }
    }

    private async Task BuildPrefetchIfNeededAsync()
    {
        if (HasValidPrefetch())
            return;

        ClearPrefetch();
        var anchor = HttpContext.Session.GetString(PracticeQuestionKey);
        if (string.IsNullOrWhiteSpace(anchor))
            return;

        if (!await PickQuestionIntoModelAsync())
            return;

        HttpContext.Session.SetString(PrefetchQuestionKey, QuestionImage ?? "");
        HttpContext.Session.SetString(PrefetchOptionsKey,
            JsonSerializer.Serialize(ShuffledAnswers ?? new Dictionary<string, string>(), AppJson.Options));
        HttpContext.Session.SetString(PrefetchCorrectKey, CorrectAnswerKey ?? "");
        HttpContext.Session.SetString(PrefetchAnchorKey, anchor);

        await LoadPracticeQuestionFromSessionAsync();
    }

    private async Task<bool> TryPromotePrefetchAsync()
    {
        if (!HasValidPrefetch())
            return false;

        ApplyPrefetchToModel();
        RecordQuestionShown(QuestionImage);
        IncrementGroupShown(QuestionImage);
        AddRecentQuestionToSession(QuestionImage);
        HttpContext.Session.Remove(LastSubmittedQuestionKey);
        SavePracticeQuestionState();
        ClearPrefetch();
        await PopulateUrlsAsync();
        return true;
    }

    private async Task<object> BuildPrefetchResponseAsync()
    {
        ApplyPrefetchToModel();
        await PopulateUrlsAsync();
        var response = BuildNextQuestionResponse();
        await LoadPracticeQuestionFromSessionAsync();
        return response;
    }

    private async Task<List<List<string>>> LoadAllQuestionGroupsAsync()
    {
        if (_storageGroupsCache != null && (DateTime.UtcNow - _storageGroupsCachedAt) < _storageGroupsTtl)
            return _storageGroupsCache;

        List<string> filtered;
        if (_storage != null)
        {
            var allImages = await _storage.ListFilesAsync("");
            filtered = allImages
                .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".webp"))
                .OrderBy(name => name)
                .ToList();
        }
        else
        {
            var imagesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
            if (!Directory.Exists(imagesDir))
                return new List<List<string>>();

            if (_localImagesCache != null && (DateTime.UtcNow - _localImagesCachedAt) < _localImagesTtl)
            {
                filtered = _localImagesCache;
            }
            else
            {
                filtered = Directory.GetFiles(imagesDir)
                    .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".webp"))
                    .Select(Path.GetFileName)
                    .OrderBy(name => name)
                    .ToList();
                _localImagesCache = filtered;
                _localImagesCachedAt = DateTime.UtcNow;
            }
        }

        var grouped = new List<List<string>>();
        for (int i = 0; i + 4 < filtered.Count; i += 5)
            grouped.Add(filtered.GetRange(i, 5));

        if (_storage != null && grouped.Count > 0)
        {
            _storageGroupsCache = grouped;
            _storageGroupsCachedAt = DateTime.UtcNow;
        }

        return grouped;
    }

    private async Task<List<List<string>>> FilterGroupsAsync(List<List<string>> grouped, string mode, string difficulty, string username)
    {
        var pool = grouped;

        if (!string.IsNullOrEmpty(difficulty) && _difficultyService != null)
        {
            var allowed = await _difficultyService.GetQuestionsByDifficultyAsync(difficulty);
            if (allowed.Count > 0)
            {
                var allowedSet = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
                pool = pool.Where(g => g.Count > 0 && allowedSet.Contains(g[0])).ToList();
            }
        }

        if (mode == "weak" && _userProgress != null && !string.IsNullOrEmpty(username))
        {
            var weak = new HashSet<string>(_userProgress.GetWeakQuestions(username), StringComparer.OrdinalIgnoreCase);
            if (weak.Count > 0)
                pool = pool.Where(g => g.Count > 0 && weak.Contains(g[0])).ToList();
        }

        return pool;
    }

    private List<string> PickReviewQuestion(List<List<string>> grouped, string username)
    {
        if (_userProgress == null || string.IsNullOrEmpty(username))
            return grouped[RandomNumberGenerator.GetInt32(grouped.Count)];

        var mistakes = _userProgress.Load(username).SessionMistakes;
        if (mistakes.Count == 0)
            return PickFromPool(grouped, username, useSpaced: true);

        var mistakeSet = new HashSet<string>(mistakes, StringComparer.OrdinalIgnoreCase);
        var reviewGroups = grouped.Where(g => g.Count > 0 && mistakeSet.Contains(g[0])).ToList();
        if (reviewGroups.Count == 0)
            return PickFromPool(grouped, username, useSpaced: true);

        var lastSubmitted = HttpContext.Session.GetString(LastSubmittedQuestionKey) ?? "";
        var withoutLast = reviewGroups
            .Where(g => !string.Equals(g[0], lastSubmitted, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (withoutLast.Count > 0)
            reviewGroups = withoutLast;

        return reviewGroups[RandomNumberGenerator.GetInt32(reviewGroups.Count)];
    }

    private Task<List<string>> PickDailyQuestionAsync(List<List<string>> grouped)
    {
        EnsureDailyChallengeSession();
        var dailyJson = HttpContext.Session.GetString("DailyQuestions");
        List<string> dailyList;

        if (string.IsNullOrWhiteSpace(dailyJson))
        {
            var today = UserProgressService.TodayKey();
            var seed = today.GetHashCode();
            var rng = new Random(seed);
            var indices = Enumerable.Range(0, grouped.Count).OrderBy(_ => rng.Next()).Take(Math.Min(DailyTotal, grouped.Count)).ToList();
            dailyList = indices.Select(i => grouped[i][0]).ToList();
            HttpContext.Session.SetString("DailyQuestions", JsonSerializer.Serialize(dailyList, AppJson.Options));
        }
        else
        {
            dailyList = JsonSerializer.Deserialize<List<string>>(dailyJson, AppJson.Options) ?? new List<string>();
        }

        var idx = HttpContext.Session.GetInt32("DailyQuestionIndex") ?? 0;
        if (idx >= dailyList.Count)
            return Task.FromResult(grouped[0]);

        var questionFile = dailyList[idx];
        var group = grouped.FirstOrDefault(g => g.Count > 0 && string.Equals(g[0], questionFile, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(group ?? grouped[RandomNumberGenerator.GetInt32(grouped.Count)]);
    }

    private List<string> PickFromPool(List<List<string>> pool, string username, bool useSpaced)
    {
        if (pool.Count == 0) return null;

        var recent = GetRecentQuestionsFromSession();
        var lastSubmitted = HttpContext.Session.GetString(LastSubmittedQuestionKey) ?? "";

        if (useSpaced && _userProgress != null && !string.IsNullOrEmpty(username))
        {
            var progress = _userProgress.Load(username);
            var withPriority = pool
                .Where(g => g.Count > 0)
                .Select(g => (g, priority: _userProgress.GetSpacedPriority(progress, g[0])))
                .GroupBy(x => x.priority)
                .OrderByDescending(g => g.Key)
                .First();

            pool = withPriority.Select(x => x.g).ToList();
        }

        var eligible = pool.Where(g => g.Count > 0
            && !IsQuestionThrottled(g[0])
            && !recent.Contains(g[0])
            && !string.Equals(g[0], lastSubmitted, StringComparison.OrdinalIgnoreCase)).ToList();
        if (eligible.Count == 0)
        {
            eligible = pool.Where(g => g.Count > 0
                && !string.Equals(g[0], lastSubmitted, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (eligible.Count == 0)
            eligible = pool.Where(g => g.Count > 0).ToList();

        int chosenIdx;
        lock (_bagLock)
        {
            var now = DateTime.UtcNow;
            var needRebuild = _bagOrder == null || _bagSourceCount != eligible.Count || _bagIndex >= (_bagOrder?.Count ?? 0) || (now - _bagBuiltAt) > _bagTtl;
            if (needRebuild)
            {
                var order = Enumerable.Range(0, eligible.Count).ToList();
                FisherYatesShuffle(order);
                _bagOrder = order;
                _bagIndex = 0;
                _bagSourceCount = eligible.Count;
                _bagBuiltAt = now;
            }

            chosenIdx = _bagOrder[_bagIndex % _bagOrder.Count];
            _bagIndex++;
        }

        return eligible[chosenIdx % eligible.Count];
    }

    private void ClearImageUrlState()
    {
        QuestionImageUrl = string.Empty;
        AnswerImageUrls = new Dictionary<string, string>();
        QuestionImageOriginalName = string.Empty;
        AnswerImageOriginalNames = new Dictionary<string, string>();
    }

    private static void FisherYatesShuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            if (j != i)
            {
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }

    private static void IncrementGroupShown(string questionImage)
    {
        lock (_bagLock)
        {
            if (!_groupShownCount.ContainsKey(questionImage))
                _groupShownCount[questionImage] = 0;
            _groupShownCount[questionImage]++;
        }
    }

    private static bool IsQuestionThrottled(string questionImage)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(-1);
        lock (_questionRateLock)
        {
            if (!_questionShownTimes.TryGetValue(questionImage, out var times))
                return false;
            times.RemoveAll(t => t < cutoff);
            return times.Count >= 3;
        }
    }

    private static void RecordQuestionShown(string questionImage)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(-1);
        lock (_questionRateLock)
        {
            if (!_questionShownTimes.TryGetValue(questionImage, out var times))
            {
                times = new List<DateTime>();
                _questionShownTimes[questionImage] = times;
            }
            times.RemoveAll(t => t < cutoff);
            times.Add(now);
        }
    }

    private List<string> GetRecentQuestionsFromSession()
    {
        try
        {
            var json = HttpContext.Session.GetString("RecentQuestions");
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            var list = JsonSerializer.Deserialize<List<string>>(json, AppJson.Options) ?? new List<string>();
            return list.TakeLast(10).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private void AddRecentQuestionToSession(string questionImage)
    {
        try
        {
            var list = GetRecentQuestionsFromSession();
            list.Add(questionImage);
            if (list.Count > 20)
                list = list.TakeLast(20).ToList();
            HttpContext.Session.SetString("RecentQuestions", JsonSerializer.Serialize(list, AppJson.Options));
        }
        catch (Exception ex) { Console.WriteLine($"[AddRecentQuestionToSession Error] {ex.Message}"); }
    }

    private async Task PopulateUrlsAsync()
    {
        if (_storage != null)
        {
            await PopulateSignedUrlsAsync();
            return;
        }

        try
        {
            // Store original file names for reporting
            QuestionImageOriginalName = QuestionImage;
            AnswerImageOriginalNames = new Dictionary<string, string>();
            foreach (var kv in ShuffledAnswers ?? new Dictionary<string, string>())
            {
                if (!string.IsNullOrWhiteSpace(kv.Value))
                    AnswerImageOriginalNames[kv.Key] = kv.Value;
            }

            QuestionImageUrl = string.IsNullOrWhiteSpace(QuestionImage) ? string.Empty : ($"/images/{QuestionImage}");
            AnswerImageUrls = new Dictionary<string, string>();
            foreach (var kv in ShuffledAnswers ?? new Dictionary<string, string>())
            {
                if (!string.IsNullOrWhiteSpace(kv.Value))
                    AnswerImageUrls[kv.Key] = $"/images/{kv.Value}";
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[PopulateUrls Local Error] {ex.Message}");
            ClearImageUrlState();
        }
    }

    private async Task PopulateSignedUrlsAsync()
    {
        try
        {
            QuestionImageOriginalName = QuestionImage;
            AnswerImageOriginalNames = new Dictionary<string, string>();
            foreach (var kv in ShuffledAnswers ?? new Dictionary<string, string>())
            {
                if (!string.IsNullOrWhiteSpace(kv.Value))
                    AnswerImageOriginalNames[kv.Key] = kv.Value;
            }

            QuestionImageUrl = string.IsNullOrWhiteSpace(QuestionImage)
                ? string.Empty
                : await _storage.GetSignedUrlAsync(QuestionImage);

            var keys = ShuffledAnswers?.Values?.Where(v => !string.IsNullOrWhiteSpace(v)).ToList() ?? new List<string>();
            var urls = await _storage.GetSignedUrlsAsync(keys);
            AnswerImageUrls = new Dictionary<string, string>();
            foreach (var kv in ShuffledAnswers ?? new Dictionary<string, string>())
            {
                if (!string.IsNullOrWhiteSpace(kv.Value) && urls.TryGetValue(kv.Value, out var url))
                    AnswerImageUrls[kv.Key] = url;
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[PopulateSignedUrls Error] {ex.Message}");
            ClearImageUrlState();
        }
    }

    private void MoveCorrectImagesLocal()
    {
        try
        {
            var wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var imagesPath = Path.Combine(wwwroot, "images");
            var correctPath = Path.Combine(wwwroot, "correct_answers");

            if (!Directory.Exists(correctPath))
                Directory.CreateDirectory(correctPath);

            var allFiles = new List<string> { QuestionImage };
            if (!string.IsNullOrEmpty(CorrectAnswerKey)
                && ShuffledAnswers.TryGetValue(CorrectAnswerKey, out var correctFile))
                allFiles.Add(correctFile);
            allFiles.AddRange(ShuffledAnswers.Values.Where(v => !string.IsNullOrWhiteSpace(v)));

            foreach (var file in allFiles.Distinct())
            {
                if (string.IsNullOrEmpty(file)) continue;
                var source = Path.Combine(imagesPath, file);
                var dest = Path.Combine(correctPath, file);
                if (System.IO.File.Exists(source) && !System.IO.File.Exists(dest))
                    System.IO.File.Move(source, dest);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MoveCorrectImagesLocal Error] {ex}");
        }
    }

    private object BuildSubmitAnswerResponse()
    {
        var achievements = NewlyUnlockedAchievements
            .Select(key =>
            {
                var def = AchievementCatalog.Find(key);
                return def == null
                    ? null
                    : new { emoji = def.Emoji, title = def.Title, description = def.Description };
            })
            .Where(x => x != null)
            .ToList();

        return new
        {
            isCorrect = IsCorrect,
            selectedKey = SelectedAnswer,
            correctKey = CorrectAnswerKey,
            correctAnswerFile = !string.IsNullOrEmpty(CorrectAnswerKey)
                && ShuffledAnswers != null
                && ShuffledAnswers.TryGetValue(CorrectAnswerKey, out var correctFile)
                ? correctFile
                : "",
            correctAnswerUrl = !string.IsNullOrEmpty(CorrectAnswerKey)
                && AnswerImageUrls != null
                && AnswerImageUrls.TryGetValue(CorrectAnswerKey, out var correctUrl)
                ? correctUrl
                : "",
            answers = (ShuffledAnswers ?? new Dictionary<string, string>())
                .Select(kv => new
                {
                    key = kv.Key,
                    fileName = AnswerImageOriginalNames?.TryGetValue(kv.Key, out var fn) == true
                        ? fn
                        : kv.Value
                })
                .ToList(),
            stats = new
            {
                correct = UserCorrect,
                total = UserTotal,
                successRate = UserSuccessRate,
                streak = CurrentStreak,
                xp = UserXp,
                level = UserLevel
            },
            achievements,
            redirect = (string)null,
            showGitHubStarPrompt = ShowGitHubStarModal,
            githubStarMilestone = GitHubStarMilestone,
            githubStarUrl = GitHubStarPrompt.RepoUrl
        };
    }

    private object BuildNextQuestionResponse()
    {
        return new
        {
            questionImage = QuestionImage,
            questionImageUrl = QuestionImageUrl,
            questionImageOriginalName = QuestionImageOriginalName ?? QuestionImage,
            answers = (ShuffledAnswers ?? new Dictionary<string, string>())
                .Select(kv => new
                {
                    key = kv.Key,
                    imageUrl = AnswerImageUrls?.TryGetValue(kv.Key, out var url) == true ? url : "",
                    fileName = AnswerImageOriginalNames?.TryGetValue(kv.Key, out var fn) == true
                        ? fn
                        : kv.Value
                })
                .ToList(),
            practiceModeLabel = GetPracticeModeLabel(),
            practiceMode = PracticeMode,
            dailyProgress = DailyProgress,
            dailyTotal = DailyTotal,
            streak = CurrentStreak
        };
    }

    private string GetPracticeModeLabel()
    {
        return PracticeMode switch
        {
            "weak" => "תרגול חולשות",
            "review" => "סקירת טעויות",
            "daily" => "אתגר יומי",
            "normal" => PracticeDifficulty switch
            {
                "easy" => "תרגול — קל",
                "medium" => "תרגול — בינוני",
                "hard" => "תרגול — קשה",
                _ => "תרגול חופשי"
            },
            _ => PracticeDifficulty switch
            {
                "easy" => "תרגול — קל",
                "medium" => "תרגול — בינוני",
                "hard" => "תרגול — קשה",
                _ => "תרגול חופשי"
            }
        };
    }

    private static string GetRedirectPath(IActionResult result)
    {
        if (result is RedirectToPageResult redirect)
        {
            var page = redirect.PageName ?? "";
            return page.StartsWith("/") ? page : "/" + page;
        }

        return null;
    }
}
