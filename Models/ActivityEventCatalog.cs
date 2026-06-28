using System.Collections.Generic;

namespace NoodlesSimulator.Models;

public static class ActivityEventCatalog
{
    public const string CategoryAnswer = "answer";
    public const string CategoryExam = "exam";
    public const string CategoryAchievement = "achievement";
    public const string CategoryProgress = "progress";
    public const string CategoryPrompts = "prompts";
    public const string CategoryReport = "report";
    public const string CategoryAdmin = "admin";
    public const string CategoryLogin = "login";

    public const string Register = "register";
    public const string DailyComplete = "daily_complete";
    public const string QuestionReport = "question_report";
    public const string AdminAction = "admin_action";
    public const string LevelUp = "level_up";
    public const string PracticeStart = "practice_start";
    public const string ExamExpired = "exam_expired";
    public const string ProgressReset = "progress_reset";
    public const string Answer = "answer";
    public const string ExamStart = "exam_start";
    public const string ExamComplete = "exam_complete";
    public const string Achievement = "achievement";
    public const string Login = "login";

    public const string FeedbackPrompt = "feedback_prompt";
    public const string FeedbackSubmit = "feedback_submit";
    public const string FeedbackLater = "feedback_later";
    public const string GitHubStarPrompt = "github_star_prompt";
    public const string GitHubStarAccept = "github_star_accept";
    public const string GitHubStarLater = "github_star_later";
    public const string AppNoticePrompt = "app_notice_prompt";
    public const string AppNoticeDismiss = "app_notice_dismiss";
    public const string WelcomeCs24Prompt = "welcome_cs24_prompt";
    public const string WelcomeCs24Click = "welcome_cs24_click";
    public const string WelcomeCs24Dismiss = "welcome_cs24_dismiss";

    public static string GetCategory(string eventType) => eventType switch
    {
        Answer => CategoryAnswer,
        ExamStart or ExamComplete or ExamExpired => CategoryExam,
        Achievement => CategoryAchievement,
        Register or LevelUp or DailyComplete or PracticeStart or ProgressReset => CategoryProgress,
        FeedbackPrompt or FeedbackSubmit or FeedbackLater
            or GitHubStarPrompt or GitHubStarAccept or GitHubStarLater
            or AppNoticePrompt or AppNoticeDismiss
            or WelcomeCs24Prompt or WelcomeCs24Click or WelcomeCs24Dismiss => CategoryPrompts,
        QuestionReport => CategoryReport,
        AdminAction => CategoryAdmin,
        Login => CategoryLogin,
        _ => "other"
    };

    public static string KindLabel(string eventType) => eventType switch
    {
        Register => "הרשמה",
        DailyComplete => "אתגר יומי",
        QuestionReport => "דיווח",
        AdminAction => "ניהול",
        LevelUp => "רמה",
        PracticeStart => "תרגול",
        ExamExpired => "מבחן",
        ProgressReset => "איפוס",
        FeedbackPrompt or FeedbackSubmit or FeedbackLater => "משוב",
        GitHubStarPrompt or GitHubStarAccept or GitHubStarLater => "כוכב",
        AppNoticePrompt or AppNoticeDismiss => "הודעה",
        WelcomeCs24Prompt or WelcomeCs24Click or WelcomeCs24Dismiss => "CS24",
        Answer => "תשובה",
        ExamStart or ExamComplete => "מבחן",
        Achievement => "הישג",
        Login => "התחברות",
        _ => eventType
    };

    public static string FormatMessage(string eventType, Dictionary<string, object> payload)
    {
        payload ??= new Dictionary<string, object>();

        return eventType switch
        {
            Register => "נרשם לראשונה",
            DailyComplete => FormatDailyComplete(payload),
            QuestionReport => FormatQuestionReport(payload),
            AdminAction => FormatAdminAction(payload),
            LevelUp => FormatLevelUp(payload),
            PracticeStart => FormatPracticeStart(payload),
            ExamExpired => FormatExamExpired(payload),
            ProgressReset => "איפס התקדמות",
            FeedbackPrompt => FormatFeedbackPrompt(payload),
            FeedbackSubmit => FormatFeedbackSubmit(payload),
            FeedbackLater => FormatFeedbackLater(payload),
            GitHubStarPrompt => FormatGitHubPrompt(payload),
            GitHubStarAccept => "לחץ יאללה על בקשת כוכב GitHub",
            GitHubStarLater => FormatGitHubLater(payload),
            AppNoticePrompt => $"קיבל הודעה ניהולית: {NoticeTitle(payload)}",
            AppNoticeDismiss => $"סגר הודעה ניהולית: {NoticeTitle(payload)}",
            WelcomeCs24Prompt => "קיבל הודעת ברוך הבא CS24",
            WelcomeCs24Click => "לחץ על קישור CS24 — דוד עזרן",
            WelcomeCs24Dismiss => "דילג על המלצת CS24",
            Answer => FormatAnswer(payload),
            ExamStart => "התחיל מבחן",
            ExamComplete => FormatExamComplete(payload),
            Achievement => FormatAchievement(payload),
            Login => "התחבר",
            _ => eventType
        };
    }

    private static string FormatDailyComplete(Dictionary<string, object> payload)
    {
        var score = GetInt(payload, "score");
        var total = GetInt(payload, "total");
        return total > 0 ? $"סיים אתגר יומי {score}/{total}" : "סיים אתגר יומי";
    }

    private static string FormatQuestionReport(Dictionary<string, object> payload)
    {
        var questionId = payload.TryGetValue("questionId", out var q) ? q?.ToString() : "";
        var shortQ = ShortQuestionId(questionId);
        return string.IsNullOrWhiteSpace(shortQ) ? "דיווח על טעות בשאלה" : $"דיווח על טעות: {shortQ}";
    }

    private static string FormatAdminAction(Dictionary<string, object> payload)
    {
        var action = payload.TryGetValue("action", out var a) ? a?.ToString() : "";
        var admin = payload.TryGetValue("admin", out var ad) ? ad?.ToString() : "Admin";
        return action switch
        {
            "cheater_mark" => $"Admin ({admin}) סימן כ-cheater",
            "cheater_unmark" => $"Admin ({admin}) ביטל סימון cheater",
            "ban" => $"Admin ({admin}) חסם משתמש",
            "unban" => $"Admin ({admin}) שחרר מחסימה",
            _ => $"פעולת Admin ({admin})"
        };
    }

    private static string FormatLevelUp(Dictionary<string, object> payload)
    {
        var level = GetInt(payload, "level");
        return level > 0 ? $"עלה לרמה {level}" : "עלה רמה";
    }

    private static string FormatPracticeStart(Dictionary<string, object> payload)
    {
        var mode = payload.TryGetValue("mode", out var m) ? m?.ToString() : "normal";
        var label = mode switch
        {
            "daily" => "אתגר יומי",
            "weak" => "תרגול חולשות",
            "review" => "חזרה על טעויות",
            _ => mode
        };
        return $"התחיל {label}";
    }

    private static string FormatExamExpired(Dictionary<string, object> payload)
    {
        var score = payload.TryGetValue("score", out var s) ? s?.ToString() : "0";
        var max = payload.TryGetValue("maxScore", out var m) ? m?.ToString() : "?";
        var idx = GetInt(payload, "currentIndex");
        return idx > 0
            ? $"מבחן פג תוקף — {score}/{max} (שאלה {idx + 1})"
            : $"מבחן פג תוקף — {score}/{max}";
    }

    private static string FormatAnswer(Dictionary<string, object> payload)
    {
        var correct = payload.TryGetValue("correct", out var c) && c is bool b && b;
        var questionId = payload.TryGetValue("questionId", out var q) ? q?.ToString() : "";
        var mode = payload.TryGetValue("mode", out var m) ? m?.ToString() : "normal";
        var modeLabel = mode switch
        {
            "weak" => "חולשות",
            "review" => "חזרה",
            _ => "רגיל"
        };
        var shortQ = ShortQuestionId(questionId);
        if (string.IsNullOrWhiteSpace(shortQ))
            shortQ = "שאלה";
        return correct
            ? $"ענה נכון על {shortQ} ({modeLabel})"
            : $"ענה שגוי על {shortQ} ({modeLabel})";
    }

    private static string FormatExamComplete(Dictionary<string, object> payload)
    {
        var score = payload.TryGetValue("score", out var s) ? s?.ToString() : "?";
        var max = payload.TryGetValue("maxScore", out var m) ? m?.ToString() : "?";
        return $"סיים מבחן {score}/{max}";
    }

    private static string FormatAchievement(Dictionary<string, object> payload)
    {
        if (payload.TryGetValue("title", out var t) && t != null)
            return $"פתח הישג: {t}";
        if (payload.TryGetValue("key", out var k) && k != null)
        {
            var def = AchievementCatalog.Find(k.ToString());
            if (def != null) return $"פתח הישג: {def.Title}";
        }
        return "פתח הישג חדש";
    }

    private static string FormatFeedbackPrompt(Dictionary<string, object> payload)
    {
        var milestone = GetInt(payload, "milestone");
        return milestone > 0
            ? $"קיבל בקשת משוב (חוות דעת) — {milestone} הישגים"
            : "קיבל בקשת משוב (חוות דעת)";
    }

    private static string FormatFeedbackSubmit(Dictionary<string, object> payload)
    {
        var rating = GetInt(payload, "rating");
        return rating > 0 ? $"שלח משוב ★{rating}" : "שלח משוב";
    }

    private static string FormatFeedbackLater(Dictionary<string, object> payload)
    {
        var milestone = GetInt(payload, "milestone");
        return milestone > 0
            ? $"דחה משוב (אולי אחר כך) — {milestone} הישגים"
            : "דחה משוב (אולי אחר כך)";
    }

    private static string FormatGitHubPrompt(Dictionary<string, object> payload)
    {
        var milestone = GetInt(payload, "milestone");
        return milestone > 0
            ? $"קיבל בקשת כוכב GitHub — {milestone} שאלות"
            : "קיבל בקשת כוכב GitHub";
    }

    private static string FormatGitHubLater(Dictionary<string, object> payload)
    {
        var milestone = GetInt(payload, "milestone");
        return milestone > 0
            ? $"דחה כוכב GitHub (אולי אחר כך) — {milestone} שאלות"
            : "דחה כוכב GitHub (אולי אחר כך)";
    }

    private static string NoticeTitle(Dictionary<string, object> payload)
    {
        var noticeId = payload.TryGetValue("noticeId", out var id) ? id?.ToString() : "";
        return noticeId switch
        {
            AppNotices.ExamNavigation => "ניווט במבחן",
            AppNotices.June2026Update => "עדכון יוני 2026",
            AppNotices.ExamFix => "תיקון מבחן",
            _ => string.IsNullOrWhiteSpace(noticeId) ? "הודעה" : noticeId
        };
    }

    private static string ShortQuestionId(string questionId)
    {
        if (string.IsNullOrWhiteSpace(questionId)) return "";
        return questionId.Length > 28 ? questionId[..25] + "…" : questionId;
    }

    private static int GetInt(Dictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value == null)
            return 0;

        return value switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, out var n) => n,
            _ => 0
        };
    }
}
