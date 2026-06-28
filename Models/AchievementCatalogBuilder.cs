using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NoodlesSimulator.Models;

public static class AchievementCatalogBuilder
{
    private static readonly (int Value, string Key, string Title)[] StreakTiers =
    {
        (3, "streak_3", "ניצוץ"),
        (5, "streak_5", "באש!"),
        (7, "streak_7", "בהתלהבות"),
        (10, "streak_10", "לוהט!"),
        (12, "streak_12", "על גלים"),
        (15, "streak_15", "בלתי ניתן לעצור"),
        (18, "streak_18", "מכונת אש"),
        (20, "streak_20", "בלתי ניתן לכיבוי"),
        (25, "streak_25", "אש וגברש"),
        (30, "streak_30", "להבה"),
        (35, "streak_35", "שריפה"),
        (40, "streak_40", "אש קדושה"),
        (45, "streak_45", "מלך הרצף"),
        (50, "streak_50", "חמישים ברצף"),
        (55, "streak_55", "בלתי ניתן לעצירה"),
        (60, "streak_60", "שישים ברצף"),
        (70, "streak_70", "שבעים ברצף"),
        (80, "streak_80", "שמונים ברצף"),
        (100, "streak_100", "מאה ברצף"),
        (150, "streak_150", "אגדת רצף"),
    };

    private static readonly (int Value, string Key, string Title)[] QuestionTiers =
    {
        (10, "questions_10", "התחלה טובה"),
        (25, "questions_25", "על הגל"),
        (50, "questions_50", "חמישים שאלות"),
        (75, "questions_75", "שבעים וחמש"),
        (100, "questions_100", "מאה!"),
        (150, "questions_150", "מאה וחמישים"),
        (200, "questions_200", "מאתיים"),
        (300, "questions_300", "שלוש מאות"),
        (400, "questions_400", "ארבע מאות"),
        (500, "questions_500", "מכונה!"),
        (750, "questions_750", "שבע מאות וחמישים"),
        (1000, "questions_1000", "אגדה"),
        (1500, "questions_1500", "אלף וחמש מאות"),
        (2000, "questions_2000", "אלפיים"),
        (3000, "questions_3000", "שלושת אלפים"),
        (4000, "questions_4000", "ארבעת אלפים"),
        (5000, "questions_5000", "חמישת אלפים"),
        (7500, "questions_7500", "שבעת אלפים וחמש מאות"),
        (10000, "questions_10000", "עשרת אלפים"),
        (15000, "questions_15000", "חמש עשרה אלף"),
    };

    private static readonly (int Value, string Key, string Title)[] LevelTiers =
    {
        (2, "level_2", "רמה 2"),
        (3, "level_3", "רמה 3"),
        (4, "level_4", "רמה 4"),
        (5, "level_5", "רמה 5"),
        (6, "level_6", "רמה 6"),
        (7, "level_7", "רמה 7"),
        (8, "level_8", "רמה 8"),
        (10, "level_10", "מומחה"),
        (12, "level_12", "רמה 12"),
        (14, "level_14", "רמה 14"),
        (15, "level_15", "מאסטר"),
        (17, "level_17", "רמה 17"),
        (18, "level_18", "רמה 18"),
        (20, "level_20", "גרנד מאסטר"),
        (22, "level_22", "רמה 22"),
        (25, "level_25", "רמה 25"),
        (28, "level_28", "רמה 28"),
        (30, "level_30", "רמה 30"),
        (35, "level_35", "רמה 35"),
        (40, "level_40", "רמה 40"),
    };

    private static readonly (int Value, string Key, string Title)[] ExamCountTiers =
    {
        (1, "first_exam", "מבחן ראשון"),
        (2, "exams_2", "שני מבחנים"),
        (3, "exams_3", "שלושה מבחנים"),
        (5, "exams_5", "ותיק"),
        (7, "exams_7", "שבעה מבחנים"),
        (10, "exams_10", "מקצוען מבחנים"),
        (15, "exams_15", "חמש עשרה מבחנים"),
        (20, "exams_20", "עשרים מבחנים"),
        (30, "exams_30", "שלושים מבחנים"),
        (50, "exams_50", "חמישים מבחנים"),
        (75, "exams_75", "שבעים וחמישה מבחנים"),
        (100, "exams_100", "מאה מבחנים"),
        (150, "exams_150", "מאה וחמישים מבחנים"),
        (200, "exams_200", "מאתיים מבחנים"),
    };

    private static readonly (int Value, string Key, string Title)[] ExamBestTiers =
    {
        (12, "exam_best_12", "12 נכונות במבחן"),
        (13, "exam_best_13", "13 נכונות במבחן"),
        (14, "exam_pass", "עברת!"),
        (15, "exam_best_15", "15 נכונות במבחן"),
        (16, "exam_best_16", "16 נכונות במבחן"),
        (17, "perfect_exam", "מושלם!"),
    };

    private static readonly (int Value, string Key, string Title)[] PerfectExamTiers =
    {
        (2, "perfect_exams_2", "שני מבחנים מושלמים"),
        (3, "perfectionist", "פרפקציוניסט"),
        (5, "perfect_exams_5", "חמישה מושלמים"),
        (10, "perfect_exams_10", "עשרה מושלמים"),
    };

    private static readonly (int Value, string Key, string Title)[] ExamImproveTiers =
    {
        (3, "exam_improve", "עלית בדרגה"),
        (5, "exam_improve_5", "קפיצת ענק"),
        (7, "exam_improve_7", "שיפור דרמטי"),
        (10, "exam_improve_10", "מסע התקדמות"),
    };

    private static readonly (int Value, string Key, string Title)[] DailyCompletionTiers =
    {
        (1, "daily_complete", "אתגר יומי"),
        (2, "daily_done_2", "שני אתגרים"),
        (3, "daily_done_3", "שלושה אתגרים"),
        (5, "daily_done_5", "חמישה אתגרים"),
        (7, "daily_done_7", "שבעה אתגרים"),
        (10, "daily_done_10", "עשרה אתגרים"),
        (15, "daily_done_15", "חמש עשרה אתגרים"),
    };

    private static readonly (int Value, string Key, string Title)[] DailyStreakTiers =
    {
        (2, "daily_streak_2", "יומיים ברצף"),
        (3, "daily_streak_3", "שלושה ימים"),
        (5, "daily_streak_5", "חמישה ימים"),
        (7, "daily_streak_7", "שבוע שלם"),
        (14, "daily_streak_14", "שבועיים"),
        (30, "daily_streak_30", "חודש שלם"),
        (60, "daily_streak_60", "שישים ימים"),
        (90, "daily_streak_90", "תשעים ימים"),
        (180, "daily_streak_180", "חצי שנה"),
        (365, "daily_streak_365", "שנה שלמה"),
        (500, "daily_streak_500", "חמש מאות ימים"),
        (730, "daily_streak_730", "שנתיים"),
        (1000, "daily_streak_1000", "אלף ימים"),
    };

    private static readonly (int Value, string Key, string Title)[] DailyPerfectTiers =
    {
        (1, "daily_perfect", "יום מושלם"),
        (3, "daily_perfect_3", "שלושה ימים מושלמים"),
        (5, "daily_perfect_5", "חמישה ימים מושלמים"),
        (10, "daily_perfect_10", "עשרה ימים מושלמים"),
        (25, "daily_perfect_25", "עשרים וחמישה מושלמים"),
        (50, "daily_perfect_50", "חמישים מושלמים"),
        (100, "daily_perfect_100", "מאה מושלמים"),
        (250, "daily_perfect_250", "מאתיים וחמישים מושלמים"),
        (500, "daily_perfect_500", "חמש מאות מושלמים"),
        (1000, "daily_perfect_1000", "אלף מושלמים"),
    };

    private static readonly (int Value, string Key, string Title)[] PracticeWeekly =
    {
        (10, "weekly_10", "עשר נכונות השבוע"),
        (20, "weekly_20", "עשרים השבוע"),
        (25, "weekly_25", "שבוע חזק"),
        (30, "weekly_30", "שלושים השבוע"),
        (50, "weekly_50", "שבוע מטורף"),
    };

    private static readonly (int Value, string Key, string Title)[] PracticeHard =
    {
        (10, "hard_10", "עשר קשות"),
        (25, "hard_25", "קשה זה קל"),
        (50, "hard_50", "חמישים קשות"),
        (75, "hard_75", "שבעים וחמש קשות"),
        (100, "hard_100", "מאה קשות"),
        (150, "hard_150", "מאה וחמישים קשות"),
        (200, "hard_200", "מאתיים קשות"),
    };

    private static readonly (int Value, string Key, string Title)[] PracticeWeak =
    {
        (10, "weak_10", "עשר חולשות"),
        (20, "weak_20", "לוחם חולשות"),
        (50, "weak_50", "חמישים חולשות"),
        (100, "weak_100", "מאה חולשות"),
    };

    private static readonly (int Value, string Key, string Title)[] PracticeReview =
    {
        (1, "review_clear", "ניקוי טעויות"),
        (3, "review_clear_3", "שלושה ניקויים"),
        (5, "review_clear_5", "חמישה ניקויים"),
        (10, "review_clear_10", "עשרה ניקויים"),
    };

    private static readonly (int MinQuestions, double MinAccuracy, string Key, string Title)[] AccuracyTiers =
    {
        (25, 0.70, "accuracy_70_25", "70% על 25 שאלות"),
        (25, 0.75, "accuracy_75_25", "75% על 25 שאלות"),
        (50, 0.70, "accuracy_70_50", "70% על 50 שאלות"),
        (50, 0.75, "accuracy_75_50", "75% על 50 שאלות"),
        (50, 0.80, "accuracy_80", "יציב"),
        (75, 0.80, "accuracy_80_75", "80% על 75 שאלות"),
        (100, 0.75, "accuracy_75_100", "75% על 100 שאלות"),
        (100, 0.80, "accuracy_80_100", "80% על 100 שאלות"),
        (100, 0.85, "accuracy_85_100", "85% על 100 שאלות"),
        (100, 0.90, "accuracy_90", "כמעט מושלם"),
        (150, 0.85, "accuracy_85_150", "85% על 150 שאלות"),
        (150, 0.90, "accuracy_90_150", "90% על 150 שאלות"),
        (200, 0.90, "accuracy_90_200", "90% על 200 שאלות"),
        (200, 0.92, "accuracy_92_200", "92% על 200 שאלות"),
        (250, 0.90, "accuracy_90_250", "90% על 250 שאלות"),
        (300, 0.90, "accuracy_90_300", "90% על 300 שאלות"),
        (100, 0.95, "accuracy_95_100", "95% על 100 שאלות"),
        (200, 0.95, "accuracy_95_200", "95% על 200 שאלות"),
        (300, 0.95, "accuracy_95_300", "95% על 300 שאלות"),
        (500, 0.90, "accuracy_90_500", "90% על 500 שאלות"),
    };

    public static IReadOnlyList<AchievementDefinition> BuildAll()
    {
        var list = new List<AchievementDefinition>();
        list.AddRange(BuildStreak());
        list.AddRange(BuildVolume());
        list.AddRange(BuildExam());
        list.AddRange(BuildDaily());
        list.AddRange(BuildPractice());
        list.AddRange(BuildAccuracy());
        return list;
    }

    public static IReadOnlyList<(int Value, string Key)> StreakKeys { get; } = StreakTiers.Select(t => (t.Value, t.Key)).ToList();
    public static IReadOnlyList<(int Value, string Key)> QuestionKeys { get; } = QuestionTiers.Select(t => (t.Value, t.Key)).ToList();
    public static IReadOnlyList<(int Value, string Key)> LevelKeys { get; } = LevelTiers.Select(t => (t.Value, t.Key)).ToList();
    public static IReadOnlyList<(int Value, string Key)> ExamCountKeys { get; } = ExamCountTiers.Select(t => (t.Value, t.Key)).ToList();
    public static IReadOnlyList<(int Value, string Key)> ExamBestKeys { get; } = ExamBestTiers.Select(t => (t.Value, t.Key)).ToList();
    public static IReadOnlyList<(int Value, string Key)> PerfectExamKeys { get; } = PerfectExamTiers.Select(t => (t.Value, t.Key)).ToList();
    public static IReadOnlyList<(int Value, string Key)> ExamImproveKeys { get; } = ExamImproveTiers.Select(t => (t.Value, t.Key)).ToList();
    public static IReadOnlyList<(int Value, string Key)> DailyCompletionKeys { get; } = DailyCompletionTiers.Select(t => (t.Value, t.Key)).ToList();
    public static IReadOnlyList<(int Value, string Key)> DailyStreakKeys { get; } = DailyStreakTiers.Select(t => (t.Value, t.Key)).ToList();
    public static IReadOnlyList<(int Value, string Key)> DailyPerfectKeys { get; } = DailyPerfectTiers.Select(t => (t.Value, t.Key)).ToList();
    public static IReadOnlyList<(int Value, string Key)> WeeklyKeys { get; } = PracticeWeekly.Select(t => (t.Value, t.Key)).ToList();
    public static IReadOnlyList<(int Value, string Key)> HardKeys { get; } = PracticeHard.Select(t => (t.Value, t.Key)).ToList();
    public static IReadOnlyList<(int Value, string Key)> WeakKeys { get; } = PracticeWeak.Select(t => (t.Value, t.Key)).ToList();
    public static IReadOnlyList<(int Value, string Key)> ReviewKeys { get; } = PracticeReview.Select(t => (t.Value, t.Key)).ToList();
    public static IReadOnlyList<(int MinQuestions, double MinAccuracy, string Key)> AccuracyKeys { get; } =
        AccuracyTiers.Select(t => (t.MinQuestions, t.MinAccuracy, t.Key)).ToList();

    private static IEnumerable<AchievementDefinition> BuildStreak()
    {
        foreach (var t in StreakTiers)
            yield return Def(t.Key, t.Title, $"{t.Value} תשובות נכונות ברצף", "🔥", "streak");
    }

    private static IEnumerable<AchievementDefinition> BuildVolume()
    {
        foreach (var t in QuestionTiers.Take(10))
            yield return Def(t.Key, t.Title, $"{Fmt(t.Value)} שאלות נענו", "🌊", "volume");
        foreach (var t in LevelTiers.Take(10))
            yield return Def(t.Key, t.Title, $"הגעת לרמה {t.Value}", "⭐", "volume");
    }

    private static IEnumerable<AchievementDefinition> BuildExam()
    {
        foreach (var t in ExamCountTiers.Take(10))
        {
            var desc = t.Value == 1 ? "השלמת מבחן ראשון" : $"{t.Value} מבחנים הושלמו";
            yield return Def(t.Key, t.Title, desc, "📝", "exam");
        }
        foreach (var t in ExamBestTiers)
            yield return Def(t.Key, t.Title, $"{t.Value}+ נכונות במבחן (שיא אישי)", "✅", "exam");
        foreach (var t in PerfectExamTiers)
            yield return Def(t.Key, t.Title, $"{t.Value} מבחנים מושלמים (17/17)", "💎", "exam");
    }

    private static IEnumerable<AchievementDefinition> BuildDaily()
    {
        foreach (var t in DailyCompletionTiers)
            yield return Def(t.Key, t.Title, $"{t.Value} השלמות אתגר יומי", "📅", "daily");
        foreach (var t in DailyStreakTiers.Take(7))
            yield return Def(t.Key, t.Title, $"{t.Value} ימים רצופים של אתגר יומי", "📆", "daily");
        foreach (var t in DailyPerfectTiers.Take(6))
            yield return Def(t.Key, t.Title, $"{t.Value} ימים מושלמים (10/10)", "🌟", "daily");
    }

    private static IEnumerable<AchievementDefinition> BuildPractice()
    {
        foreach (var t in PracticeWeekly)
            yield return Def(t.Key, t.Title, $"{t.Value}+ נכונות השבוע", "💥", "practice");
        foreach (var t in PracticeHard)
            yield return Def(t.Key, t.Title, $"{t.Value} נכונות ברמת קושי קשה", "🔴", "practice");
        foreach (var t in PracticeWeak)
            yield return Def(t.Key, t.Title, $"{t.Value} נכונות במצב חולשות", "🛡️", "practice");
        foreach (var t in PracticeReview)
        {
            var desc = t.Value == 1 ? "ריקון כל הטעויות בסקירה" : $"{t.Value} ניקויי טעויות";
            yield return Def(t.Key, t.Title, desc, "🧹", "practice");
        }
    }

    private static IEnumerable<AchievementDefinition> BuildAccuracy()
    {
        foreach (var t in AccuracyTiers)
        {
            var pct = (int)(t.MinAccuracy * 100);
            yield return Def(t.Key, t.Title, $"{pct}%+ דיוק על לפחות {t.MinQuestions} שאלות", "🎯", "accuracy");
        }
    }

    private static AchievementDefinition Def(string key, string title, string desc, string emoji, string category) =>
        new() { Key = key, Title = title, Description = desc, Emoji = emoji, Category = category };

    private static string Fmt(int n) => n.ToString("N0", CultureInfo.GetCultureInfo("he-IL"));
}
