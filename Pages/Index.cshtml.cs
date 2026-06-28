using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Pages;

public class IndexModel : PageModel
{
    private readonly EmailService _emailService;
    private readonly ActivityEventService _activityEvents;
    private readonly QuestionReportService _questionReports;
    private readonly PracticeQuizService _practiceQuiz;
    private readonly PracticeAnswerService _practiceAnswer;
    private readonly PracticeIndexPageService _indexPage;
    private readonly AdminUserService _adminUsers;

    public IndexModel(
        EmailService emailService = null,
        ActivityEventService activityEvents = null,
        QuestionReportService questionReports = null,
        PracticeQuizService practiceQuiz = null,
        PracticeAnswerService practiceAnswer = null,
        PracticeIndexPageService indexPage = null,
        AdminUserService adminUsers = null)
    {
        _emailService = emailService;
        _activityEvents = activityEvents;
        _questionReports = questionReports;
        _practiceQuiz = practiceQuiz;
        _practiceAnswer = practiceAnswer;
        _indexPage = indexPage;
        _adminUsers = adminUsers;
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
    public int FeedbackMilestone { get; set; }
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

    public string QuestionImageUrl { get; set; }
    public Dictionary<string, string> AnswerImageUrls { get; set; }
    public string QuestionImageOriginalName { get; set; }
    public Dictionary<string, string> AnswerImageOriginalNames { get; set; }

    private int _lastXpGain;
    private int _levelUpTo;
    private int _brokenStreakAt;
    private bool _dailyJustCompleted;
    private int _dailyFinalScore;

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            if (_indexPage == null)
            {
                Username = HttpContext.Session.GetString("Username");
                if (string.IsNullOrEmpty(Username))
                    return RedirectToPage("/Login");
            }
            else
            {
                var prep = await _indexPage.PrepareGetPageAsync(HttpContext);
                if (prep.RedirectLogin)
                    return RedirectToPage("/Login");
                if (prep.RedirectBanned)
                {
                    HttpContext.Session.Clear();
                    RememberMeService.Clear(Response);
                    return RedirectToPage("/Login");
                }

                Username = prep.Username;
                OnlineCount = prep.OnlineCount;
                ActiveNoticeId = prep.ActiveNoticeId;
                ApplyFeedbackPrompt(prep.FeedbackPrompt);
                ApplyGitHubStarPrompt(prep.GitHubStarPrompt);

                _indexPage.ApplyPracticeQueryParams(
                    HttpContext,
                    Request.Query["mode"].ToString(),
                    Request.Query["difficulty"].ToString());
                if (PracticeQuizService.ShouldClearAnswerFlashOnGet(Request))
                    _practiceQuiz?.ClearAnswerFlash(HttpContext.Session);

                ApplyUserStatsView(await _indexPage.BuildUserStatsViewAsync(prep.User, HttpContext.Session, DailyTotal));
                NewlyUnlockedAchievements = _indexPage.LoadPendingAchievements(HttpContext.Session);
                if (!await TryRestoreAnswerFlashAsync())
                {
                    try { await LoadRandomQuestionAsync(); }
                    catch (Exception ex) { Console.WriteLine($"[OnGetAsync PreloadQuestion Error] {ex.Message}"); }
                }

                return Page();
            }

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
            var auth = await RequireUserAsync();
            if (auth.Redirect != null)
                return auth.Redirect;

            if (Request.Form.ContainsKey("reset"))
                return await HandleResetPostAsync(auth.User);

            if (!Request.Form.ContainsKey("answer"))
            {
                _practiceQuiz?.ClearAnswerFlash(HttpContext.Session);
                _indexPage?.ApplyPracticeQueryParams(
                    HttpContext,
                    Request.Query["mode"].ToString(),
                    Request.Query["difficulty"].ToString());
                try { await LoadRandomQuestionAsync(); }
                catch (Exception ex) { Console.WriteLine($"[OnPostAsync ReloadQuestion Error] {ex.Message}"); }
                ApplyUserStatsView(await _indexPage.BuildUserStatsViewAsync(auth.User, HttpContext.Session, DailyTotal));
                return Page();
            }

            ProcessSubmittedAnswerForm();
            await ProcessSubmittedAnswerCoreAsync(auth.User);
            var cheaterAction = await DetectCheaterAsync(auth.User);
            if (cheaterAction == CheaterDetectionAction.RedirectLogin)
                return RedirectToPage("/Login");
            if (cheaterAction == CheaterDetectionAction.RedirectCheater)
                return RedirectToPage("/Cheater");

            SaveAnswerFlash();
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

            _practiceQuiz?.EnrichReportFromFlash(HttpContext.Session, payload);

            var baseUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}";
            if (_questionReports != null)
                await _questionReports.SubmitAsync(payload, baseUrl, _emailService, _activityEvents);

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
            var auth = await RequireUserAsync();
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

            var lastSubmitted = HttpContext.Session.GetString(PracticeQuizService.LastSubmittedQuestionKey);
            if (!string.IsNullOrEmpty(lastSubmitted)
                && string.Equals(lastSubmitted, questionImage, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(HttpContext.Session.GetString(PracticeQuizService.FlashQuestionKey)))
            {
                await TryRestoreAnswerFlashAsync();
                ApplyUserStatsView(await _indexPage.BuildUserStatsViewAsync(auth.User, HttpContext.Session, DailyTotal));
                NewlyUnlockedAchievements = new List<string>();
                return new JsonResult(BuildSubmitAnswerResponse());
            }

            ApplyEvaluation(_practiceAnswer?.PrepareSubmittedAnswer(
                HttpContext.Session, questionImage, answer, tryRecover: true)
                ?? new PracticeAnswerEvaluation { QuestionImage = questionImage, SelectedAnswer = answer });

            await ProcessSubmittedAnswerCoreAsync(auth.User);

            var cheaterAction = await DetectCheaterAsync(auth.User);
            if (cheaterAction == CheaterDetectionAction.RedirectLogin)
                return new JsonResult(new { redirect = "/Login" });
            if (cheaterAction == CheaterDetectionAction.RedirectCheater)
                return new JsonResult(new { redirect = "/Cheater" });

            SaveAnswerFlash();
            _practiceQuiz?.ClearPrefetch(HttpContext.Session);
            ApplyUserStatsView(await _indexPage.BuildUserStatsViewAsync(auth.User, HttpContext.Session, DailyTotal));
            ApplyGitHubStarPrompt(_indexPage?.ResolveGitHubStarPrompt(auth.User));
            await ApplyUrlsFromServiceAsync();
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
            var auth = await RequireUserAsync();
            if (auth.Redirect != null)
                return new JsonResult(new { error = "Unauthorized", redirect = "/Login" }) { StatusCode = 401 };

            _practiceQuiz?.ClearAnswerFlash(HttpContext.Session);
            if (_practiceQuiz != null)
            {
                var display = await _practiceQuiz.AdvanceToNextQuestionDisplayAsync(HttpContext.Session);
                ApplyQuestionDisplay(display);
            }

            ApplyUserStatsView(await _indexPage.BuildUserStatsViewAsync(auth.User, HttpContext.Session, DailyTotal));
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
            var auth = await RequireUserAsync();
            if (auth.Redirect != null)
                return new JsonResult(new { error = "Unauthorized", redirect = "/Login" }) { StatusCode = 401 };

            if (_practiceQuiz == null)
                return new StatusCodeResult(204);

            var response = await _practiceQuiz.BuildPrefetchApiResponseAsync(
                HttpContext.Session, MakeNextQuestionSnapshot());
            if (response == null)
                return new StatusCodeResult(204);

            ApplyUserStatsView(await _indexPage.BuildUserStatsViewAsync(auth.User, HttpContext.Session, DailyTotal));
            return new JsonResult(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnGetPrefetchNextQuestionAsync Error] {ex}");
            return new JsonResult(new { error = "Server error" }) { StatusCode = 500 };
        }
    }

    private async Task<(User User, IActionResult Redirect)> RequireUserAsync()
    {
        var result = _indexPage != null
            ? await _indexPage.TryRequireUserAsync(HttpContext)
            : new PracticeAuthResult
            {
                RedirectLogin = string.IsNullOrEmpty(HttpContext.Session.GetString("Username"))
            };

        if (result.RedirectLogin)
            return (null, RedirectToPage("/Login"));

        Username = result.Username ?? "";
        return (result.User, null);
    }

    private void ApplyUserStatsView(PracticeUserStatsView? view)
    {
        if (view == null) return;

        CurrentStreak = view.CurrentStreak;
        UserCorrect = view.UserCorrect;
        UserTotal = view.UserTotal;
        UserSuccessRate = view.UserSuccessRate;
        UserXp = view.UserXp;
        UserLevel = view.UserLevel;
        XpProgressPercent = view.XpProgressPercent;
        PracticeMode = view.PracticeMode;
        PracticeDifficulty = view.PracticeDifficulty;
        DailyProgress = view.DailyProgress;
        IsDailyComplete = view.IsDailyComplete;
    }

    private void ApplyFeedbackPrompt(PracticeFeedbackPromptView view)
    {
        ShowFeedbackModal = view.Show;
        FeedbackCampaignId = view.CampaignId;
        FeedbackMilestone = view.Milestone;
    }

    private void ApplyGitHubStarPrompt(PracticeGitHubStarPromptView? view)
    {
        if (view == null) return;
        ShowGitHubStarModal = view.Show;
        GitHubStarMilestone = view.Milestone;
    }

    private void ApplyEvaluation(PracticeAnswerEvaluation evaluation)
    {
        QuestionImage = evaluation.QuestionImage;
        SelectedAnswer = evaluation.SelectedAnswer;
        CorrectAnswerKey = evaluation.CorrectAnswerKey;
        ShuffledAnswers = evaluation.ShuffledAnswers;
        IsCorrect = evaluation.IsCorrect;
        AnswerChecked = true;
    }

    private void ApplyAnswerFlash(PracticeQuizService.PracticeAnswerFlash flash)
    {
        QuestionImage = flash.QuestionImage;
        SelectedAnswer = flash.SelectedAnswer;
        IsCorrect = flash.IsCorrect;
        CorrectAnswerKey = flash.CorrectAnswerKey;
        ShuffledAnswers = flash.ShuffledAnswers;
        AnswerChecked = true;
    }

    private void ApplyQuestionDisplay(PracticeQuestionDisplay display)
    {
        if (display?.State == null) return;

        QuestionImage = display.State.QuestionImage;
        CorrectAnswerKey = display.State.CorrectAnswerKey;
        ShuffledAnswers = display.State.ShuffledAnswers;
        ApplyQuestionUrls(display.Urls);
    }

    private void ApplyQuestionUrls(PracticeQuestionUrls urls)
    {
        if (urls == null) return;

        QuestionImageUrl = urls.QuestionImageUrl;
        AnswerImageUrls = urls.AnswerImageUrls;
        QuestionImageOriginalName = urls.QuestionImageOriginalName;
        AnswerImageOriginalNames = urls.AnswerImageOriginalNames;
    }

    private void SaveAnswerFlash()
    {
        if (_practiceQuiz == null) return;
        _practiceQuiz.SaveAnswerFlash(HttpContext.Session, new PracticeQuizService.PracticeAnswerFlash
        {
            QuestionImage = QuestionImage ?? "",
            SelectedAnswer = SelectedAnswer ?? "",
            IsCorrect = IsCorrect,
            CorrectAnswerKey = CorrectAnswerKey ?? "",
            ShuffledAnswers = ShuffledAnswers ?? new Dictionary<string, string>()
        });
    }

    private async Task<bool> TryRestoreAnswerFlashAsync()
    {
        if (_practiceQuiz == null || !_practiceQuiz.TryReadAnswerFlash(HttpContext.Session, out var flash))
            return false;

        ApplyAnswerFlash(flash);
        var display = await _practiceQuiz.BuildDisplayAsync(QuestionImage, ShuffledAnswers, CorrectAnswerKey);
        ApplyQuestionDisplay(display);
        SavePracticeQuestionState();
        return true;
    }

    private async Task<IActionResult> HandleResetPostAsync(User user)
    {
        if (_adminUsers != null)
        {
            var (success, error) = await _adminUsers.ResetUserProgressAsync(user.Username);
            if (!success)
                Console.WriteLine($"[OnPostAsync Reset Error] {error}");
        }

        _practiceQuiz?.ClearQuizProgressSession(HttpContext.Session);
        return RedirectToPage("/Index");
    }

    private async Task ProcessSubmittedAnswerCoreAsync(User user)
    {
        if (_practiceAnswer == null) return;

        ResetAnswerFeedbackState();
        _practiceAnswer.ApplyAnswerToUserStats(user, QuestionImage, IsCorrect);

        var feedback = await _practiceAnswer.ProcessAnswerAsync(
            HttpContext.Session,
            user,
            QuestionImage,
            IsCorrect,
            DailyTotal);

        ApplyAnswerFeedback(feedback);
    }

    private void ApplyAnswerFeedback(PracticeAnswerFeedback feedback)
    {
        CurrentStreak = feedback.CurrentStreak;
        _lastXpGain = feedback.XpGain;
        _levelUpTo = feedback.LevelUpTo;
        _brokenStreakAt = feedback.BrokenStreakAt;
        _dailyJustCompleted = feedback.DailyJustCompleted;
        _dailyFinalScore = feedback.DailyFinalScore;
        IsDailyComplete = feedback.IsDailyComplete;
        if (feedback.NewAchievements.Count > 0)
            NewlyUnlockedAchievements.AddRange(feedback.NewAchievements);
    }

    private async Task<CheaterDetectionAction> DetectCheaterAsync(User user)
    {
        if (_practiceAnswer == null)
            return CheaterDetectionAction.None;

        var action = await _practiceAnswer.DetectCheaterAsync(HttpContext.Session, user);
        if (action == CheaterDetectionAction.RedirectLogin)
            RememberMeService.Clear(Response);

        return action;
    }

    private void ProcessSubmittedAnswerForm()
    {
        var questionImage = Request.Form["questionImage"].ToString();
        var answer = Request.Form["answer"].ToString();
        ApplyEvaluation(_practiceAnswer?.PrepareSubmittedAnswer(HttpContext.Session, questionImage, answer)
            ?? new PracticeAnswerEvaluation
            {
                QuestionImage = questionImage,
                SelectedAnswer = answer
            });
    }

    private void SavePracticeQuestionState()
    {
        _practiceQuiz?.SavePracticeQuestionState(
            HttpContext.Session,
            QuestionImage,
            ShuffledAnswers,
            CorrectAnswerKey);
    }

    private async Task LoadRandomQuestionAsync()
    {
        if (_practiceQuiz == null)
            return;

        try
        {
            var display = await _practiceQuiz.LoadRandomQuestionDisplayAsync(HttpContext.Session);
            ApplyQuestionDisplay(display);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoadRandomQuestionAsync Error] {ex.Message}");
        }
    }

    private async Task ApplyUrlsFromServiceAsync()
    {
        if (_practiceQuiz == null)
            return;

        var urls = await _practiceQuiz.PopulateUrlsAsync(QuestionImage, ShuffledAnswers);
        ApplyQuestionUrls(urls);
    }

    private PracticeNextQuestionSnapshot MakeNextQuestionSnapshot() => new()
    {
        PracticeMode = PracticeMode,
        PracticeDifficulty = PracticeDifficulty,
        DailyProgress = DailyProgress,
        DailyTotal = DailyTotal,
        CurrentStreak = CurrentStreak
    };

    private void ResetAnswerFeedbackState()
    {
        _lastXpGain = 0;
        _levelUpTo = 0;
        _brokenStreakAt = 0;
        _dailyJustCompleted = false;
        _dailyFinalScore = 0;
    }

    private object BuildSubmitAnswerResponse() =>
        PracticeQuizApiResponses.BuildSubmitAnswer(new PracticeSubmitAnswerSnapshot
        {
            IsCorrect = IsCorrect,
            SelectedAnswer = SelectedAnswer ?? "",
            CorrectAnswerKey = CorrectAnswerKey ?? "",
            ShuffledAnswers = ShuffledAnswers ?? new Dictionary<string, string>(),
            AnswerImageUrls = AnswerImageUrls,
            AnswerImageOriginalNames = AnswerImageOriginalNames,
            UserCorrect = UserCorrect,
            UserTotal = UserTotal,
            UserSuccessRate = UserSuccessRate,
            CurrentStreak = CurrentStreak,
            UserXp = UserXp,
            UserLevel = UserLevel,
            XpProgressPercent = XpProgressPercent,
            XpGain = _lastXpGain,
            LevelUpTo = _levelUpTo,
            BrokenStreakAt = _brokenStreakAt,
            DailyJustCompleted = _dailyJustCompleted,
            DailyFinalScore = _dailyFinalScore,
            DailyTotal = DailyTotal,
            NewlyUnlockedAchievements = NewlyUnlockedAchievements,
            ShowFeedbackModal = ShowFeedbackModal,
            FeedbackCampaignId = FeedbackCampaignId,
            FeedbackMilestone = FeedbackMilestone,
            ShowGitHubStarModal = ShowGitHubStarModal,
            GitHubStarMilestone = GitHubStarMilestone
        });

    private object BuildNextQuestionResponse() =>
        PracticeQuizApiResponses.BuildNextQuestion(new PracticeNextQuestionSnapshot
        {
            QuestionImage = QuestionImage ?? "",
            QuestionImageUrl = QuestionImageUrl ?? "",
            QuestionImageOriginalName = QuestionImageOriginalName ?? QuestionImage ?? "",
            ShuffledAnswers = ShuffledAnswers ?? new Dictionary<string, string>(),
            AnswerImageUrls = AnswerImageUrls,
            AnswerImageOriginalNames = AnswerImageOriginalNames,
            PracticeMode = PracticeMode,
            PracticeDifficulty = PracticeDifficulty,
            DailyProgress = DailyProgress,
            DailyTotal = DailyTotal,
            CurrentStreak = CurrentStreak
        });
}
