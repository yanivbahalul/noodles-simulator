using System.Collections.Generic;
using System.Linq;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public sealed class PracticeNextQuestionSnapshot
{
    public string QuestionImage { get; init; } = "";
    public string QuestionImageUrl { get; init; } = "";
    public string QuestionImageOriginalName { get; init; } = "";
    public Dictionary<string, string> ShuffledAnswers { get; init; } = new();
    public Dictionary<string, string>? AnswerImageUrls { get; init; }
    public Dictionary<string, string>? AnswerImageOriginalNames { get; init; }
    public string PracticeMode { get; init; } = "normal";
    public string PracticeDifficulty { get; init; } = "";
    public int DailyProgress { get; init; }
    public int DailyTotal { get; init; }
    public int CurrentStreak { get; init; }
}

public sealed class PracticeSubmitAnswerSnapshot
{
    public bool IsCorrect { get; init; }
    public string SelectedAnswer { get; init; } = "";
    public string CorrectAnswerKey { get; init; } = "";
    public Dictionary<string, string> ShuffledAnswers { get; init; } = new();
    public Dictionary<string, string>? AnswerImageUrls { get; init; }
    public Dictionary<string, string>? AnswerImageOriginalNames { get; init; }
    public int UserCorrect { get; init; }
    public int UserTotal { get; init; }
    public int UserSuccessRate { get; init; }
    public int CurrentStreak { get; init; }
    public int UserXp { get; init; }
    public int UserLevel { get; init; }
    public int XpProgressPercent { get; init; }
    public int XpGain { get; init; }
    public int LevelUpTo { get; init; }
    public int BrokenStreakAt { get; init; }
    public bool DailyJustCompleted { get; init; }
    public int DailyFinalScore { get; init; }
    public int DailyTotal { get; init; }
    public List<string> NewlyUnlockedAchievements { get; init; } = new();
    public bool HasExplanation { get; init; }
}

public static class PracticeQuizApiResponses
{
    public static object BuildNextQuestion(PracticeNextQuestionSnapshot s) => new
    {
        questionImage = s.QuestionImage,
        questionImageUrl = s.QuestionImageUrl,
        questionImageOriginalName = s.QuestionImageOriginalName ?? s.QuestionImage,
        answers = s.ShuffledAnswers
            .Select(kv => new
            {
                key = kv.Key,
                imageUrl = s.AnswerImageUrls?.TryGetValue(kv.Key, out var url) == true ? url : "",
                fileName = s.AnswerImageOriginalNames?.TryGetValue(kv.Key, out var fn) == true ? fn : kv.Value
            })
            .ToList(),
        practiceModeLabel = PracticeQuizService.GetPracticeModeLabel(s.PracticeMode, s.PracticeDifficulty),
        practiceMode = s.PracticeMode,
        dailyProgress = s.DailyProgress,
        dailyTotal = s.DailyTotal,
        streak = s.CurrentStreak
    };

    public static object BuildSubmitAnswer(PracticeSubmitAnswerSnapshot s)
    {
        var achievements = s.NewlyUnlockedAchievements
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
            isCorrect = s.IsCorrect,
            selectedKey = s.SelectedAnswer,
            correctKey = s.CorrectAnswerKey,
            correctAnswerFile = !string.IsNullOrEmpty(s.CorrectAnswerKey)
                && s.ShuffledAnswers.TryGetValue(s.CorrectAnswerKey, out var correctFile)
                ? correctFile
                : "",
            correctAnswerUrl = !string.IsNullOrEmpty(s.CorrectAnswerKey)
                && s.AnswerImageUrls?.TryGetValue(s.CorrectAnswerKey, out var correctUrl) == true
                ? correctUrl
                : "",
            answers = s.ShuffledAnswers
                .Select(kv => new
                {
                    key = kv.Key,
                    fileName = s.AnswerImageOriginalNames?.TryGetValue(kv.Key, out var fn) == true ? fn : kv.Value
                })
                .ToList(),
            stats = new
            {
                correct = s.UserCorrect,
                total = s.UserTotal,
                successRate = s.UserSuccessRate,
                streak = s.CurrentStreak,
                xp = s.UserXp,
                level = s.UserLevel,
                xpProgressPercent = s.XpProgressPercent,
                xpToNextLevel = QuizGamification.XpToNextLevel(s.UserXp)
            },
            feedback = new
            {
                xpGain = s.XpGain,
                levelUpTo = s.LevelUpTo > 0 ? s.LevelUpTo : (int?)null,
                brokenStreak = s.BrokenStreakAt,
                dailyComplete = s.DailyJustCompleted,
                dailyScore = s.DailyFinalScore,
                dailyTotal = s.DailyTotal
            },
            achievements,
            redirect = (string)null,
            hasExplanation = s.HasExplanation
        };
    }
}
