using System;
using System.Collections.Generic;
using System.Linq;

namespace NoodlesSimulator.Models;

public static class QuizGamification
{
    public const int BaseXpPerCorrect = 10;
    public const int MediumXpPerCorrect = 15;
    public const int HardXpPerCorrect = 25;
    public const int ExamPassThreshold = 14;
    public const int DailyChallengeXpPerCorrect = 12;

    public static int DailyChallengeCompletionXp(int score) => 20 + score * 5;

    public static int LevelFromXp(int xp) => Math.Max(1, (int)Math.Floor(Math.Sqrt(xp / 100.0)));

    public static int XpForLevel(int level) => level * level * 100;

    public static int XpToNextLevel(int xp)
    {
        var current = LevelFromXp(xp);
        return XpForLevel(current + 1) - xp;
    }

    public static int XpProgressPercent(int xp)
    {
        var level = LevelFromXp(xp);
        var currentLevelXp = XpForLevel(level);
        var nextLevelXp = XpForLevel(level + 1);
        if (nextLevelXp <= currentLevelXp) return 100;
        return (int)Math.Round((double)(xp - currentLevelXp) / (nextLevelXp - currentLevelXp) * 100);
    }

    public static int XpForDifficulty(string difficulty) => difficulty switch
    {
        "hard" => HardXpPerCorrect,
        "medium" => MediumXpPerCorrect,
        _ => BaseXpPerCorrect
    };

    /// <summary>
    /// XP multiplier by streak: +0.1× per step within each block of 5,
    /// then ×2 at streak 5, 10, 15… (2×, 4×, 8×…).
    /// </summary>
    public static double StreakXpMultiplier(int streak)
    {
        if (streak <= 0) return 1.0;

        if (streak % 5 == 0)
            return Math.Pow(2, streak / 5);

        var tier = streak / 5;
        var baseMultiplier = Math.Pow(2, tier);
        var stepsInTier = streak - tier * 5;
        var increment = tier == 0 ? stepsInTier - 1 : stepsInTier;
        return baseMultiplier + 0.1 * increment;
    }

    public static int XpForCorrectAnswer(string practiceMode, string practiceDifficulty, int streak)
    {
        var baseXp = practiceMode == "daily"
            ? DailyChallengeXpPerCorrect
            : XpForDifficulty(practiceDifficulty);
        return (int)Math.Round(baseXp * StreakXpMultiplier(streak));
    }
}

public class AchievementDefinition
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Emoji { get; set; } = "🏅";
    public string Category { get; set; } = "";
}

public static class AchievementCatalog
{
    public static readonly IReadOnlyList<AchievementDefinition> All = AchievementCatalogBuilder.BuildAll();

    public static readonly IReadOnlyDictionary<string, string> CategoryTitles = new Dictionary<string, string>
    {
        ["streak"] = "רצף תרגול",
        ["volume"] = "נפח ורמות",
        ["exam"] = "מבחנים",
        ["daily"] = "אתגר יומי",
        ["practice"] = "שבועי ותרגול",
        ["accuracy"] = "דיוק",
    };

    public static AchievementDefinition? Find(string key) =>
        All.FirstOrDefault(a => a.Key == key);
}
