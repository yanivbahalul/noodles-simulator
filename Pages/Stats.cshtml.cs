using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NoodlesSimulator.Models;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Pages;

public class StatsModel : PageModel
{
    private readonly AuthService _authService;
    private readonly UserProgressService _userProgress;
    private readonly TestSessionService _testSessions;
    private readonly DashboardDataService _dashboard;

    public StatsModel(
        AuthService authService,
        UserProgressService userProgress = null,
        TestSessionService testSessions = null,
        DashboardDataService dashboard = null)
    {
        _authService = authService;
        _userProgress = userProgress;
        _testSessions = testSessions;
        _dashboard = dashboard;
    }

    public bool IsCommunityScope { get; set; }
    public string Username { get; set; } = "";
    public int CorrectAnswers { get; set; }
    public int TotalAnswered { get; set; }
    public int WrongAnswers => Math.Max(0, TotalAnswered - CorrectAnswers);
    public int SuccessRate { get; set; }
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public int Xp { get; set; }
    public int Level { get; set; }
    public int XpProgressPercent { get; set; }
    public int XpToNextLevel { get; set; }
    public int DailyCorrect { get; set; }
    public int WeeklyCorrect { get; set; }
    public int DistinctQuestions { get; set; }
    public double QuestionAccuracy { get; set; }
    public int BestExamScore { get; set; }
    public int BestExamCorrect { get; set; }
    public int ExamsCompleted { get; set; }
    public int DailyChallengesCompleted { get; set; }
    public int AchievementCount { get; set; }
    public int TotalAchievements => AchievementCatalog.All.Count;
    public int CompletedExamsCount { get; set; }

    public List<AchievementRow> Achievements { get; set; } = new();
    public List<RecentQuestionRow> RecentQuestions { get; set; } = new();

    public DashboardDataService.Summary CommunitySummary { get; set; } = new();
    public int CommunityTotalAnswered { get; set; }
    public int CommunityTotalCorrect { get; set; }
    public List<DashboardDataService.UserRow> TopUsers { get; set; } = new();
    public List<DashboardDataService.ActiveExamRow> ActiveExams { get; set; } = new();
    public List<DashboardDataService.ActivityRow> RecentActivity { get; set; } = new();

    public class AchievementRow
    {
        public string Key { get; set; } = "";
        public string Title { get; set; } = "";
        public string Emoji { get; set; } = "🏅";
        public string Description { get; set; } = "";
        public bool Unlocked { get; set; }
    }

    public class RecentQuestionRow
    {
        public string QuestionId { get; set; } = "";
        public string QuestionLabel => QuestionDisplayHelper.FormatLabel(QuestionId);
        public int Attempts { get; set; }
        public int Correct { get; set; }
        public int SuccessPercent { get; set; }
        public DateTime LastAnsweredLocal { get; set; }
        public bool LastWasCorrect { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string scope)
    {
        try
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
                return RedirectToPage("/Login");

            var user = await _authService.GetUserAsync(username);
            if (user == null)
                return RedirectToPage("/Login");

            var isAdmin = HttpContext.Session.GetString("IsAdmin") == "1";
            ViewData["ShowAdminLink"] = isAdmin;

            IsCommunityScope = string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase);
            if (IsCommunityScope)
            {
                if (!isAdmin)
                    return RedirectToPage("/Stats");

                if (_dashboard == null)
                    return RedirectToPage("/Dashboard");

                var snapshot = await _dashboard.GetSnapshotAsync();
                CommunitySummary = snapshot.Summary;
                TopUsers = snapshot.TopUsersList;
                ActiveExams = snapshot.ActiveExams;
                RecentActivity = snapshot.RecentActivity;
                CommunityTotalAnswered = snapshot.AllUsersList.Sum(u => u.TotalAnswered);
                CommunityTotalCorrect = snapshot.AllUsersList.Sum(u => u.CorrectAnswers);
                return Page();
            }

            Username = user.Username;
            CurrentStreak = HttpContext.Session.GetInt32("CurrentStreak") ?? 0;

            if (_userProgress != null)
            {
                var progress = _userProgress.Load(username);
                var (progTotal, progCorrect) = _userProgress.GetAnswerTotals(username);
                var (accuracy, distinct) = _userProgress.GetOverallAccuracyStats(username);

                CorrectAnswers = Math.Max(user.CorrectAnswers, progCorrect);
                TotalAnswered = Math.Max(user.TotalAnswered, progTotal);
                Xp = Math.Max(user.Xp, progress.Xp);
                BestStreak = progress.BestStreak;
                DailyCorrect = Math.Max(user.DailyCorrect, progress.DailyCorrect);
                WeeklyCorrect = Math.Max(user.WeeklyCorrect, progress.WeeklyCorrect);
                BestExamScore = Math.Max(user.BestExamScore, progress.BestExamScore);
                BestExamCorrect = Math.Max(user.BestExamCorrect, progress.BestExamCorrect);
                ExamsCompleted = progress.ExamsCompleted;
                DailyChallengesCompleted = progress.DailyChallengesCompleted;
                DistinctQuestions = distinct;
                QuestionAccuracy = accuracy;

                Achievements = AchievementCatalog.All
                    .Select(a => new AchievementRow
                    {
                        Key = a.Key,
                        Title = a.Title,
                        Emoji = a.Emoji,
                        Description = a.Description,
                        Unlocked = progress.Achievements?.Contains(a.Key, StringComparer.OrdinalIgnoreCase) == true
                    })
                    .OrderByDescending(a => a.Unlocked)
                    .ThenBy(a => a.Title)
                    .ToList();

                AchievementCount = Achievements.Count(a => a.Unlocked);

                RecentQuestions = progress.QuestionStats?
                    .Where(kv => kv.Value.LastAnsweredUtc > DateTime.MinValue)
                    .OrderByDescending(kv => kv.Value.LastAnsweredUtc)
                    .Take(12)
                    .Select(kv =>
                    {
                        var stat = kv.Value;
                        return new RecentQuestionRow
                        {
                            QuestionId = kv.Key,
                            Attempts = stat.Attempts,
                            Correct = stat.Correct,
                            SuccessPercent = stat.Attempts > 0
                                ? (int)Math.Round((double)stat.Correct / stat.Attempts * 100)
                                : 0,
                            LastAnsweredLocal = stat.LastAnsweredUtc.ToLocalTime(),
                            LastWasCorrect = stat.LastWasCorrect
                        };
                    })
                    .ToList() ?? new List<RecentQuestionRow>();
            }
            else
            {
                CorrectAnswers = user.CorrectAnswers;
                TotalAnswered = user.TotalAnswered;
                Xp = user.Xp;
                DailyCorrect = user.DailyCorrect;
                WeeklyCorrect = user.WeeklyCorrect;
                BestExamScore = user.BestExamScore;
                BestExamCorrect = user.BestExamCorrect;
            }

            Level = user.Level > 0 ? user.Level : QuizGamification.LevelFromXp(Xp);
            XpProgressPercent = QuizGamification.XpProgressPercent(Xp);
            XpToNextLevel = QuizGamification.XpToNextLevel(Xp);
            SuccessRate = TotalAnswered > 0
                ? (int)Math.Round((double)CorrectAnswers / TotalAnswered * 100)
                : 0;

            if (_testSessions != null)
            {
                var exams = await _testSessions.GetUserSessionsAsync(username, 50);
                CompletedExamsCount = exams.Count(e =>
                    string.Equals(e.Status, "completed", StringComparison.OrdinalIgnoreCase));
            }

            return Page();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Stats OnGetAsync Error] {ex}");
            return RedirectToPage("/Index");
        }
    }
}
