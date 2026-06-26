using System;
using System.Collections.Generic;
using System.Linq;

namespace NoodlesSimulator.Models;

public static class QuizGamification
{
    public const int BaseXpPerCorrect = 10;
    public const int MediumXpPerCorrect = 15;
    public const int HardXpPerCorrect = 25;

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
}

public class AchievementDefinition
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Emoji { get; set; } = "🏅";
}

public static class AchievementCatalog
{
    public static readonly IReadOnlyList<AchievementDefinition> All = new List<AchievementDefinition>
    {
        new() { Key = "streak_5", Title = "באש!", Description = "5 תשובות נכונות ברצף", Emoji = "🔥" },
        new() { Key = "streak_10", Title = "לוהט!", Description = "10 תשובות נכונות ברצף", Emoji = "🔥" },
        new() { Key = "first_exam", Title = "מבחן ראשון", Description = "השלמת מבחן ראשון", Emoji = "📝" },
        new() { Key = "perfect_exam", Title = "מושלם!", Description = "17/17 במבחן", Emoji = "💯" },
        new() { Key = "questions_100", Title = "מאה!", Description = "100 שאלות נענו", Emoji = "💪" },
        new() { Key = "questions_500", Title = "מכונה!", Description = "500 שאלות נענו", Emoji = "⚡" },
        new() { Key = "daily_complete", Title = "אתגר יומי", Description = "השלמת אתגר יומי", Emoji = "📅" },
        new() { Key = "level_5", Title = "רמה 5", Description = "הגעת לרמה 5", Emoji = "⭐" },
    };

    public static AchievementDefinition? Find(string key) =>
        All.FirstOrDefault(a => a.Key == key);
}
