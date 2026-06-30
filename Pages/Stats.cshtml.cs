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

    public StatsModel(
        AuthService authService,
        UserProgressService userProgress = null,
        TestSessionService testSessions = null)
    {
        _authService = authService;
        _userProgress = userProgress;
        _testSessions = testSessions;
    }

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

    public List<RecentQuestionRow> RecentQuestions { get; set; } = new();

    public class RecentQuestionRow
    {
        public string QuestionId { get; set; } = "";
        public string QuestionLabel => Models.QuestionLabel.Format(QuestionId);
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

            if (string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase))
                return isAdmin ? RedirectToPage("/Dashboard") : RedirectToPage("/Stats");

            Username = user.Username;
            CurrentStreak = HttpContext.Session.GetInt32("CurrentStreak") ?? 0;

            if (_userProgress != null)
            {
                var progress = await _userProgress.LoadAsync(username);
                var (progTotal, progCorrect) = await _userProgress.GetAnswerTotalsAsync(username);
                var (accuracy, distinct) = await _userProgress.GetOverallAccuracyStatsAsync(username);

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
                AchievementCount = progress.Achievements?.Count ?? 0;

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
