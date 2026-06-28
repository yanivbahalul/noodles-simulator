using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class AchievementService
{
    private readonly UserProgressService _progress;
    private readonly ActivityEventService _activityEvents;

    public AchievementService(UserProgressService progress, ActivityEventService activityEvents = null)
    {
        _progress = progress;
        _activityEvents = activityEvents;
    }

    public async Task<List<string>> CheckPracticeAchievementsAsync(string username, int streak, int totalAnswered, int xp)
    {
        var data = await _progress.LoadAsync(username);
        var candidates = new List<string>();
        candidates.AddRange(CollectStreakCandidates(data, streak));
        candidates.AddRange(CollectVolumeCandidates(totalAnswered, xp));
        candidates.AddRange(CollectWeeklyCandidates(data));
        candidates.AddRange(CollectModeCandidates(data));
        candidates.AddRange(await CollectAccuracyCandidatesAsync(username));
        return await UnlockAndLogAsync(username, candidates);
    }

    public async Task<List<string>> CheckExamAchievementsAsync(
        string username,
        int correctCount,
        int totalQuestions,
        bool isFirstExam,
        int previousExamCorrect)
    {
        var data = await _progress.LoadAsync(username);
        var candidates = new List<string>();
        candidates.AddRange(CollectExamCountCandidates(data.ExamsCompleted));
        candidates.AddRange(CollectExamBestCandidates(data.BestExamCorrect));
        candidates.AddRange(CollectPerfectExamCandidates(data.PerfectExamsCount));
        if (!isFirstExam)
            candidates.AddRange(CollectExamImproveCandidates(data.MaxExamImprovement));
        return await UnlockAndLogAsync(username, candidates);
    }

    public async Task<List<string>> CheckDailyAchievementsAsync(string username)
    {
        var data = await _progress.LoadAsync(username);
        return await UnlockAndLogAsync(username, CollectDailyCandidates(data, forCompletion: true));
    }

    public async Task<List<string>> CheckReviewClearAsync(string username)
    {
        var data = await _progress.LoadAsync(username);
        return await UnlockAndLogAsync(username, CollectReviewCandidates(data.ReviewClearCount));
    }

    public async Task<List<string>> CheckAllAchievementsAsync(string username, User user)
    {
        var data = await _progress.LoadAsync(username);
        var totalAnswered = user?.TotalAnswered ?? 0;
        var candidates = new List<string>();
        candidates.AddRange(CollectStreakCandidates(data, data.BestStreak));
        candidates.AddRange(CollectVolumeCandidates(totalAnswered, data.Xp));
        candidates.AddRange(CollectWeeklyCandidates(data));
        candidates.AddRange(CollectModeCandidates(data));
        candidates.AddRange(await CollectAccuracyCandidatesAsync(username));
        candidates.AddRange(CollectDailyCandidates(data, forCompletion: false));
        candidates.AddRange(CollectExamCountCandidates(data.ExamsCompleted));
        candidates.AddRange(CollectExamBestCandidates(data.BestExamCorrect));
        candidates.AddRange(CollectPerfectExamCandidates(data.PerfectExamsCount));
        candidates.AddRange(CollectExamImproveCandidates(data.MaxExamImprovement));
        candidates.AddRange(CollectReviewCandidates(data.ReviewClearCount));
        return await UnlockAndLogAsync(username, candidates);
    }

    private async Task<List<string>> UnlockAndLogAsync(string username, IEnumerable<string> candidates)
    {
        var newly = await _progress.UnlockAchievementsAsync(username, candidates);
        if (_activityEvents == null || newly.Count == 0) return newly;

        foreach (var key in newly)
        {
            var def = AchievementCatalog.Find(key);
            _activityEvents.Log(username, "achievement", new Dictionary<string, object>
            {
                ["key"] = key,
                ["title"] = def?.Title ?? key
            });
        }

        return newly;
    }

    private static List<string> CollectStreakCandidates(UserProgressService.UserProgressData data, int streak)
    {
        var best = Math.Max(data.BestStreak, streak);
        return ThresholdKeys(AchievementCatalogBuilder.StreakKeys, best);
    }

    private static List<string> CollectVolumeCandidates(int totalAnswered, int xp)
    {
        var candidates = ThresholdKeys(AchievementCatalogBuilder.QuestionKeys, totalAnswered);
        candidates.AddRange(ThresholdKeys(AchievementCatalogBuilder.LevelKeys, QuizGamification.LevelFromXp(xp)));
        return candidates;
    }

    private static List<string> CollectWeeklyCandidates(UserProgressService.UserProgressData data) =>
        ThresholdKeys(AchievementCatalogBuilder.WeeklyKeys, data.WeeklyCorrect);

    private static List<string> CollectModeCandidates(UserProgressService.UserProgressData data)
    {
        var candidates = ThresholdKeys(AchievementCatalogBuilder.HardKeys, data.HardCorrectCount);
        candidates.AddRange(ThresholdKeys(AchievementCatalogBuilder.WeakKeys, data.WeakModeCorrectCount));
        return candidates;
    }

    private static List<string> CollectReviewCandidates(int reviewClearCount) =>
        ThresholdKeys(AchievementCatalogBuilder.ReviewKeys, reviewClearCount);

    private async Task<List<string>> CollectAccuracyCandidatesAsync(string username)
    {
        var (accuracy, distinct) = await _progress.GetOverallAccuracyStatsAsync(username);
        var candidates = new List<string>();
        foreach (var tier in AchievementCatalogBuilder.AccuracyKeys)
        {
            if (distinct >= tier.MinQuestions && accuracy >= tier.MinAccuracy)
                candidates.Add(tier.Key);
        }
        return candidates;
    }

    private static List<string> CollectDailyCandidates(UserProgressService.UserProgressData data, bool forCompletion)
    {
        var candidates = new List<string>();
        if (forCompletion || data.DailyChallengesCompleted > 0)
            candidates.AddRange(ThresholdKeys(AchievementCatalogBuilder.DailyCompletionKeys, data.DailyChallengesCompleted));
        candidates.AddRange(ThresholdKeys(AchievementCatalogBuilder.DailyStreakKeys, data.DailyStreakDays));
        if (forCompletion || data.DailyPerfectCount > 0)
            candidates.AddRange(ThresholdKeys(AchievementCatalogBuilder.DailyPerfectKeys, data.DailyPerfectCount));
        return candidates;
    }

    private static List<string> CollectExamCountCandidates(int examsCompleted) =>
        ThresholdKeys(AchievementCatalogBuilder.ExamCountKeys, examsCompleted);

    private static List<string> CollectExamBestCandidates(int bestCorrect) =>
        ThresholdKeys(AchievementCatalogBuilder.ExamBestKeys, bestCorrect);

    private static List<string> CollectPerfectExamCandidates(int perfectCount) =>
        ThresholdKeys(AchievementCatalogBuilder.PerfectExamKeys, perfectCount);

    private static List<string> CollectExamImproveCandidates(int improvement) =>
        ThresholdKeys(AchievementCatalogBuilder.ExamImproveKeys, improvement);

    private static List<string> ThresholdKeys(IReadOnlyList<(int Value, string Key)> tiers, int actual)
    {
        var candidates = new List<string>();
        foreach (var tier in tiers)
        {
            if (actual >= tier.Value)
                candidates.Add(tier.Key);
        }
        return candidates;
    }
}
