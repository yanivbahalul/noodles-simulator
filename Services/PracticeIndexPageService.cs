using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public sealed class PracticeUserStatsView
{
    public int CurrentStreak { get; set; }
    public int UserCorrect { get; set; }
    public int UserTotal { get; set; }
    public int UserSuccessRate { get; set; }
    public int UserXp { get; set; }
    public int UserLevel { get; set; }
    public int XpProgressPercent { get; set; }
    public string PracticeMode { get; set; } = "normal";
    public string PracticeDifficulty { get; set; } = "";
    public int DailyProgress { get; set; }
    public bool IsDailyComplete { get; set; }
}

public sealed class PracticeFeedbackPromptView
{
    public bool Show { get; set; }
    public string CampaignId { get; set; } = "";
    public int Milestone { get; set; }
}

public sealed class PracticeGitHubStarPromptView
{
    public bool Show { get; set; }
    public int Milestone { get; set; }
}

public sealed class PracticeWelcomePromptView
{
    public bool Show { get; set; }
}

public sealed class PracticeIndexGetPrepareResult
{
    public bool RedirectLogin { get; set; }
    public bool RedirectBanned { get; set; }
    public string Username { get; set; } = "";
    public User? User { get; set; }
    public int OnlineCount { get; set; }
    public string ActiveNoticeId { get; set; } = "";
    public PracticeFeedbackPromptView FeedbackPrompt { get; set; } = new();
    public PracticeGitHubStarPromptView GitHubStarPrompt { get; set; } = new();
    public PracticeWelcomePromptView WelcomePrompt { get; set; } = new();
}

public sealed class PracticeAuthResult
{
    public User? User { get; init; }
    public string? Username { get; init; }
    public bool RedirectLogin { get; init; }
}

public class PracticeIndexPageService
{
    private readonly AuthService _auth;
    private readonly UserProgressService? _userProgress;
    private readonly UserFeedbackService? _feedback;
    private readonly ActivityEventService? _activityEvents;
    private readonly PracticeQuizService? _practiceQuiz;

    public PracticeIndexPageService(
        AuthService auth,
        UserProgressService? userProgress = null,
        UserFeedbackService? feedback = null,
        ActivityEventService? activityEvents = null,
        PracticeQuizService? practiceQuiz = null)
    {
        _auth = auth;
        _userProgress = userProgress;
        _feedback = feedback;
        _activityEvents = activityEvents;
        _practiceQuiz = practiceQuiz;
    }

    public void EnsureQuizSessionStarted(ISession session)
    {
        if (session.GetString("SessionStart") != null)
            return;

        session.SetString("SessionStart", DateTime.UtcNow.ToString());
        session.SetInt32("RapidTotal", 0);
        session.SetInt32("RapidCorrect", 0);
    }

    public async Task<PracticeIndexGetPrepareResult> PrepareGetPageAsync(HttpContext http)
    {
        var username = http.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
            return new PracticeIndexGetPrepareResult { RedirectLogin = true };

        EnsureQuizSessionStarted(http.Session);

        var userTask = _auth.GetUserAsync(username);
        var onlineTask = _auth.GetOnlineUserCountAsync();
        await Task.WhenAll(userTask, onlineTask);

        User? user = null;
        try { user = await userTask; }
        catch (Exception ex) { Console.WriteLine($"[PrepareGetPage GetUserAsync Error] {ex.Message}"); }

        int onlineCount = 0;
        try { onlineCount = await onlineTask; }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[PrepareGetPage GetOnlineUserCount Error] {ex.Message}");
        }

        var result = new PracticeIndexGetPrepareResult
        {
            Username = username,
            User = user,
            OnlineCount = onlineCount
        };

        if (user == null)
            return result;

        if (user.IsBanned)
            return new PracticeIndexGetPrepareResult { RedirectBanned = true };

        _ = _auth.TouchLastSeenAsync(user.Username, DateTime.UtcNow);
        result.WelcomePrompt = ResolveWelcomePrompt(http);
        result.ActiveNoticeId = AppNotices.GetFirstUndismissed(user.DismissedNotices) ?? "";

        var isAdmin = string.Equals(http.Session.GetString("IsAdmin"), "1", StringComparison.Ordinal);
        result.FeedbackPrompt = await ResolveFeedbackPromptAsync(user, isAdmin);
        result.GitHubStarPrompt = ResolveGitHubStarPrompt(user);

        return result;
    }

    public async Task<PracticeAuthResult> TryRequireUserAsync(HttpContext http)
    {
        var username = http.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
            return new PracticeAuthResult { RedirectLogin = true };

        User? user = null;
        try { user = await _auth.GetUserAsync(username); }
        catch (Exception ex) { Console.WriteLine($"[PracticeIndexPage TryRequireUser GetUserAsync Error] {ex.Message}"); }

        if (user == null)
            return new PracticeAuthResult { RedirectLogin = true };

        if (user.IsBanned)
        {
            http.Session.Clear();
            RememberMeService.Clear(http.Response);
            return new PracticeAuthResult { RedirectLogin = true };
        }

        return new PracticeAuthResult { User = user, Username = username };
    }

    // ponytail: quiz AJAX submit skips Supabase user fetch; session + in-request progress updates are enough.
    public PracticeAuthResult TryRequireQuizUser(HttpContext http)
    {
        var username = http.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
            return new PracticeAuthResult { RedirectLogin = true };

        return new PracticeAuthResult
        {
            User = new User { Username = username },
            Username = username
        };
    }

    public PracticeUserStatsView BuildUserStatsView(User user, ISession session, int dailyTotal = 10)
    {
        var view = new PracticeUserStatsView
        {
            CurrentStreak = session.GetInt32("CurrentStreak") ?? 0,
            PracticeMode = session.GetString("PracticeMode") ?? "normal",
            PracticeDifficulty = session.GetString("PracticeDifficulty") ?? ""
        };

        if (user == null)
            return view;

        view.UserCorrect = user.CorrectAnswers;
        view.UserTotal = user.TotalAnswered;
        view.UserXp = user.Xp;
        view.UserLevel = user.Level > 0 ? user.Level : QuizGamification.LevelFromXp(user.Xp);
        view.XpProgressPercent = QuizGamification.XpProgressPercent(view.UserXp);
        view.UserSuccessRate = view.UserTotal > 0
            ? (int)((double)view.UserCorrect / view.UserTotal * 100)
            : 0;

        if (view.PracticeMode == "daily")
        {
            view.DailyProgress = session.GetInt32("DailyQuestionIndex") ?? 0;
            var dailyDate = session.GetString("DailyDate") ?? "";
            view.IsDailyComplete = dailyDate == UserProgressService.TodayKey() && view.DailyProgress >= dailyTotal;
        }

        return view;
    }

    public async Task<PracticeUserStatsView> BuildUserStatsViewFromProgressAsync(
        User user,
        ISession session,
        int dailyTotal = 10)
    {
        var view = BuildUserStatsView(user, session, dailyTotal);
        if (user == null || _userProgress == null)
            return view;

        var data = _userProgress.TryGetCached(user.Username, out var cached)
            ? cached
            : await _userProgress.LoadAsync(user.Username);

        var (total, correct) = UserProgressService.SumQuestionStats(data);
        view.UserCorrect = Math.Max(user.CorrectAnswers, correct);
        view.UserTotal = Math.Max(user.TotalAnswered, total);
        view.UserXp = Math.Max(user.Xp, data.Xp);
        user.Xp = view.UserXp;
        view.UserLevel = QuizGamification.LevelFromXp(view.UserXp);
        user.Level = view.UserLevel;
        view.XpProgressPercent = QuizGamification.XpProgressPercent(view.UserXp);
        view.UserSuccessRate = view.UserTotal > 0
            ? (int)((double)view.UserCorrect / view.UserTotal * 100)
            : 0;
        return view;
    }

    public static void SaveQuizStatsSession(ISession session, PracticeUserStatsView view)
    {
        if (session == null || view == null) return;
        session.SetInt32(PracticeQuizService.QuizStatsTotalKey, view.UserTotal);
        session.SetInt32(PracticeQuizService.QuizStatsCorrectKey, view.UserCorrect);
        session.SetInt32(PracticeQuizService.QuizStatsXpKey, view.UserXp);
    }

    public static void HydrateUserFromQuizSession(ISession session, User user)
    {
        if (session == null || user == null) return;

        var total = session.GetInt32(PracticeQuizService.QuizStatsTotalKey);
        var correct = session.GetInt32(PracticeQuizService.QuizStatsCorrectKey);
        var xp = session.GetInt32(PracticeQuizService.QuizStatsXpKey);

        if (total.HasValue)
            user.TotalAnswered = Math.Max(user.TotalAnswered, total.Value);
        if (correct.HasValue)
            user.CorrectAnswers = Math.Max(user.CorrectAnswers, correct.Value);
        if (xp.HasValue)
        {
            user.Xp = Math.Max(user.Xp, xp.Value);
            user.Level = QuizGamification.LevelFromXp(user.Xp);
        }
    }

    public async Task<PracticeUserStatsView> BuildUserStatsViewAsync(User user, ISession session, int dailyTotal = 10)
    {
        var view = new PracticeUserStatsView
        {
            CurrentStreak = session.GetInt32("CurrentStreak") ?? 0,
            PracticeMode = session.GetString("PracticeMode") ?? "normal",
            PracticeDifficulty = session.GetString("PracticeDifficulty") ?? ""
        };

        if (user == null)
            return view;

        if (_userProgress != null)
        {
            var snap = await _userProgress.GetQuizStatsSnapshotAsync(user.Username);
            view.UserCorrect = Math.Max(user.CorrectAnswers, snap.CorrectAnswers);
            view.UserTotal = Math.Max(user.TotalAnswered, snap.TotalAnswered);
            view.UserXp = Math.Max(user.Xp, snap.Xp);
            user.Xp = view.UserXp;
            view.UserLevel = QuizGamification.LevelFromXp(view.UserXp);
            user.Level = view.UserLevel;
            view.XpProgressPercent = QuizGamification.XpProgressPercent(view.UserXp);
        }
        else
        {
            view.UserCorrect = user.CorrectAnswers;
            view.UserTotal = user.TotalAnswered;
            view.UserXp = user.Xp;
            view.UserLevel = user.Level > 0 ? user.Level : QuizGamification.LevelFromXp(user.Xp);
            view.XpProgressPercent = QuizGamification.XpProgressPercent(view.UserXp);
        }

        view.UserSuccessRate = view.UserTotal > 0
            ? (int)((double)view.UserCorrect / view.UserTotal * 100)
            : 0;

        if (view.PracticeMode == "daily")
        {
            view.DailyProgress = session.GetInt32("DailyQuestionIndex") ?? 0;
            var dailyDate = session.GetString("DailyDate") ?? "";
            view.IsDailyComplete = dailyDate == UserProgressService.TodayKey() && view.DailyProgress >= dailyTotal;
        }

        return view;
    }

    public async Task<PracticeFeedbackPromptView> ResolveFeedbackPromptAsync(User user, bool isAdmin)
    {
        var view = new PracticeFeedbackPromptView();
        if (user == null || _feedback == null || !_feedback.IsEnabled)
            return view;

        var achievementCount = await GetUnlockedAchievementCountAsync(user.Username);
        var campaignId = FeedbackCampaigns.GetActiveCampaignIdForUser(DateTime.UtcNow, isAdmin, achievementCount);
        if (string.IsNullOrEmpty(campaignId))
            return view;

        if (await _feedback.HasSubmittedFeedbackAsync(user.Username))
            return view;

        if (await _feedback.HasRespondedAsync(user.Username, campaignId))
            return view;

        view.Show = true;
        view.CampaignId = campaignId;
        view.Milestone = FeedbackCampaigns.GetActiveMilestone(achievementCount);
        return view;
    }

    public PracticeGitHubStarPromptView ResolveGitHubStarPrompt(User? user)
    {
        var view = new PracticeGitHubStarPromptView();
        if (user == null || !GitHubStarPrompt.ShouldPrompt(user))
            return view;

        view.Show = true;
        view.Milestone = GitHubStarPrompt.GetActiveMilestone(user.TotalAnswered);
        return view;
    }

    public PracticeWelcomePromptView ResolveWelcomePrompt(HttpContext http) =>
        new() { Show = WelcomePrompt.ShouldPrompt(http) };

    public async Task<int> GetUnlockedAchievementCountAsync(string username)
    {
        if (_userProgress == null || string.IsNullOrWhiteSpace(username))
            return 0;

        var achievements = (await _userProgress.LoadAsync(username))?.Achievements;
        return achievements?.Count ?? 0;
    }

    public List<string> LoadPendingAchievements(ISession session)
    {
        var json = session.GetString("PendingAchievements");
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            session.Remove("PendingAchievements");
            return JsonSerializer.Deserialize<List<string>>(json, AppJson.Options) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public void ApplyPracticeQueryParams(HttpContext http, string mode, string difficulty)
    {
        var session = http.Session;
        var username = session.GetString("Username");

        if (!string.IsNullOrWhiteSpace(mode))
        {
            var prevMode = session.GetString("PracticeMode") ?? "normal";
            session.SetString("PracticeMode", mode);
            if (!string.IsNullOrWhiteSpace(username) && mode != prevMode && IsNotablePracticeMode(mode))
                _activityEvents?.Log(username, ActivityEventCatalog.PracticeStart, new Dictionary<string, object>
                {
                    ["mode"] = mode ?? "normal"
                });
            if (mode != "normal")
                session.Remove("PracticeDifficulty");
            else if (string.IsNullOrWhiteSpace(difficulty))
                session.Remove("PracticeDifficulty");
        }

        if (!string.IsNullOrWhiteSpace(difficulty))
        {
            session.SetString("PracticeDifficulty", difficulty);
            session.SetString("PracticeMode", "normal");
        }

        if (mode == "daily" && _practiceQuiz != null)
            _practiceQuiz.EnsureDailyChallengeSession(session);
    }

    private static bool IsNotablePracticeMode(string mode) =>
        mode is "daily" or "weak" or "review";
}
