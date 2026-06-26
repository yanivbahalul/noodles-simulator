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

    public IndexModel(AuthService authService, SupabaseStorageService storage = null, EmailService emailService = null, QuestionStatsService stats = null, UserProgressService userProgress = null, AchievementService achievements = null, QuestionDifficultyService difficultyService = null)
    {
        _authService = authService;
        _storage = storage;
        _emailService = emailService;
        _stats = stats;
        _userProgress = userProgress;
        _achievements = achievements;
        _difficultyService = difficultyService;
    }

    public bool AnswerChecked { get; set; }
    public bool IsCorrect { get; set; }
    public string SelectedAnswer { get; set; }
    public string QuestionImage { get; set; }
    public Dictionary<string, string> ShuffledAnswers { get; set; }
    public string Username { get; set; }
    public int OnlineCount { get; set; }
    public bool ShowExamFixNotice { get; set; }

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
                ShowExamFixNotice = !_authService.HasDismissedNotice(user, AppNotices.ExamFix);
            }

            ApplyPracticeQueryParams();
            await PopulateUserStatsAsync(user);
            LoadPendingAchievements();
            try { await LoadRandomQuestionAsync(); } catch (Exception ex) { Console.WriteLine($"[OnGetAsync PreloadQuestion Error] {ex.Message}"); }
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

            if (string.IsNullOrEmpty(Request.Form["answersJson"]))
            {
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

            await PopulateUserStatsAsync(auth.User);

            _ = Task.Run(async () =>
            {
                try { OnlineCount = await _authService.GetOnlineUserCountAsync(); }
                catch { OnlineCount = 0; }
            });

            await PopulateUrlsAsync();
            return Page();
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

    private Task PopulateUserStatsAsync(User user)
    {
        if (user == null) return Task.CompletedTask;
        UserCorrect = user.CorrectAnswers;
        UserTotal = user.TotalAnswered;
        UserSuccessRate = UserTotal > 0 ? (int)((double)UserCorrect / UserTotal * 100) : 0;
        CurrentStreak = HttpContext.Session.GetInt32("CurrentStreak") ?? 0;

        if (_userProgress != null)
        {
            var progress = _userProgress.Load(user.Username);
            UserXp = Math.Max(user.Xp, progress.Xp);
            user.Xp = UserXp;
            user.Level = QuizGamification.LevelFromXp(UserXp);
            UserLevel = user.Level;
            XpProgressPercent = QuizGamification.XpProgressPercent(UserXp);
        }
        else
        {
            UserXp = user.Xp;
            UserLevel = user.Level > 0 ? user.Level : QuizGamification.LevelFromXp(user.Xp);
            XpProgressPercent = QuizGamification.XpProgressPercent(UserXp);
        }

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
        HttpContext.Session.SetInt32("CurrentStreak", 0);
        try { await _authService.UpdateUserAsync(user); }
        catch (Exception ex) { Console.WriteLine($"[OnPostAsync Reset UpdateUserAsync Error] {ex.Message}"); }
        return RedirectToPage("/Index");
    }

    private async Task ProcessSubmittedAnswerAsync(User user)
    {
        ParseSubmittedAnswerForm();
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
        var answer = Request.Form["answer"];
        var questionImage = Request.Form["questionImage"];
        var answersJson = Request.Form["answersJson"];

        SelectedAnswer = answer;
        AnswerChecked = true;
        QuestionImage = questionImage;
        try { ShuffledAnswers = JsonSerializer.Deserialize<Dictionary<string, string>>(answersJson, AppJson.Options); }
        catch (JsonException) { ShuffledAnswers = new Dictionary<string, string>(); }
        IsCorrect = answer == "correct";
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
        var rapidTotal = HttpContext.Session.GetInt32("RapidTotal") ?? 0;
        var rapidCorrect = HttpContext.Session.GetInt32("RapidCorrect") ?? 0;
        if (rapidTotal < 20 && rapidCorrect < 15)
            return null;

        Console.WriteLine($"[CHEATER DETECTED] User: {user.Username} | RapidTotal: {rapidTotal} | RapidCorrect: {rapidCorrect}");
        user.CorrectAnswers = 0;
        user.TotalAnswered = 0;
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
            var grouped = await LoadAllQuestionGroupsAsync();
            if (grouped.Count == 0)
            {
                QuestionImage = "placeholder.jpg";
                ShuffledAnswers = new Dictionary<string, string>();
                await PopulateUrlsAsync();
                return;
            }

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
                chosen = PickFromPool(pool, username, mode != "weak");
            }

            if (chosen == null || chosen.Count < 2)
            {
                QuestionImage = "placeholder.jpg";
                ShuffledAnswers = new Dictionary<string, string>();
                await PopulateUrlsAsync();
                return;
            }

            QuestionImage = chosen[0];
            var correct = chosen[1];
            var wrong = chosen.Skip(2).Take(3).ToList();

            ShuffledAnswers = new List<(string, string)>
            {
                ("correct", correct),
                ("a", wrong.Count > 0 ? wrong[0] : null),
                ("b", wrong.Count > 1 ? wrong[1] : null),
                ("c", wrong.Count > 2 ? wrong[2] : null)
            }
            .Where(x => !string.IsNullOrEmpty(x.Item2))
            .OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue))
            .ToDictionary(x => x.Item1, x => x.Item2);

            RecordQuestionShown(QuestionImage);
            IncrementGroupShown(QuestionImage);
            AddRecentQuestionToSession(QuestionImage);
            await PopulateUrlsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoadRandomQuestionAsync Error] {ex.Message}");
        }
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

        if (useSpaced && _userProgress != null && !string.IsNullOrEmpty(username))
        {
            var withPriority = pool
                .Where(g => g.Count > 0)
                .Select(g => (g, priority: _userProgress.GetSpacedPriority(username, g[0])))
                .GroupBy(x => x.priority)
                .OrderByDescending(g => g.Key)
                .First();

            pool = withPriority.Select(x => x.g).ToList();
        }

        var eligible = pool.Where(g => g.Count > 0 && !IsQuestionThrottled(g[0]) && !recent.Contains(g[0])).ToList();
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

            var allFiles = new[]
            {
                QuestionImage,
                ShuffledAnswers.ContainsKey("correct") ? ShuffledAnswers["correct"] : null,
                ShuffledAnswers.ContainsKey("a") ? ShuffledAnswers["a"] : null,
                ShuffledAnswers.ContainsKey("b") ? ShuffledAnswers["b"] : null,
                ShuffledAnswers.ContainsKey("c") ? ShuffledAnswers["c"] : null
            };

            foreach (var file in allFiles)
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
}
