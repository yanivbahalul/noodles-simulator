using System;
using Microsoft.AspNetCore.Http;

namespace NoodlesSimulator.Models;

public static class WelcomePrompt
{
    public const string CourseUrl = "https://cs24.co.il/courses/16?utm_source=noodles&utm_medium=welcome";
    public const string NoticeId = "welcome-cs24";
    public const string SessionKey = "WelcomePending";

    public const string Title = "ברוכים הבאים ל-Noodles Simulator";
    public const string Subtitle = "כאן מתרגלים שאלות ורואים איך מתקדמים.";
    public const string RecommendTitle = "לפני שמתחילים לתרגל";
    public const string CourseName = "תכנות מונחה עצמים";
    public const string InstructorName = "דוד עזרן";
    public const string RecommendBody =
        "כדי שהתרגול כאן יהיה שווה, כדאי קודם להכיר את יסודות OOP. " +
        "אני ממליץ על הקורס של דוד עזרן ב-CS24 — תכנות מונחה עצמים.";
    public const string PersonalTestimonial =
        "המלצה אישית: בזכות הקורס הזה, לי ולעוד רבים, הביאה הצטיינות במבחן.";
    public const string AcceptButton = "לקורס של דוד עזרן ב-CS24";
    public const string SkipButton = "התחיל/י לתרגל";

    public static readonly string[] PlatformFeatures =
    {
        "תרגול — חופשי, חולשות, חזרה על טעויות, או אתגר יומי",
        "מבחן — סימולציה עם קפיצה בין שאלות",
        "XP, רמות, רצף נכון והישגים",
        "טבלת מובילים — לראות איפה אתם עומדים"
    };

    public static bool ShouldPrompt(HttpContext http) =>
        http.Session.GetString(SessionKey) == "1";

    public static bool IsValidNoticeId(string? noticeId) =>
        string.Equals(noticeId, NoticeId, StringComparison.Ordinal);

    public static void ClearPending(HttpContext http) =>
        http.Session.Remove(SessionKey);
}
