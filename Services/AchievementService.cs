using System.Collections.Generic;
using System.Linq;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class AchievementService
{
    private readonly UserProgressService _progress;

    public AchievementService(UserProgressService progress)
    {
        _progress = progress;
    }

    public List<string> CheckPracticeAchievements(string username, int streak, int totalAnswered, int xp)
    {
        var candidates = new List<string>();
        if (streak >= 5) candidates.Add("streak_5");
        if (streak >= 10) candidates.Add("streak_10");
        if (totalAnswered >= 100) candidates.Add("questions_100");
        if (totalAnswered >= 500) candidates.Add("questions_500");
        if (QuizGamification.LevelFromXp(xp) >= 5) candidates.Add("level_5");
        return _progress.UnlockAchievements(username, candidates);
    }

    public List<string> CheckExamAchievements(string username, int correctCount, int totalQuestions, bool isFirstExam)
    {
        var candidates = new List<string>();
        if (isFirstExam) candidates.Add("first_exam");
        if (correctCount >= totalQuestions && totalQuestions > 0) candidates.Add("perfect_exam");
        return _progress.UnlockAchievements(username, candidates);
    }

    public List<string> CheckDailyChallengeAchievement(string username)
    {
        return _progress.UnlockAchievements(username, new[] { "daily_complete" });
    }

    public List<AchievementDefinition> GetUnlocked(string username)
    {
        var data = _progress.Load(username);
        return AchievementCatalog.All
            .Where(a => data.Achievements.Contains(a.Key, System.StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    public List<AchievementDefinition> GetAllWithStatus(string username)
    {
        var data = _progress.Load(username);
        return AchievementCatalog.All
            .Select(a => a)
            .ToList();
    }

    public bool IsUnlocked(string username, string key) =>
        _progress.HasAchievement(username, key);
}
