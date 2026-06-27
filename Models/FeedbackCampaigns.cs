using System;

namespace NoodlesSimulator.Models;

public static class FeedbackCampaigns
{
    public const string June2026 = "feedback-2026-06-28";

    public const string Title = "איך אתה מרגיש עם Noodles Simulator?";
    public const string Subtitle = "הדירוג והמשוב שלך יעזרו לנו לשפר";

    /// <summary>When true, only Admin sees the feedback modal (for testing). Set false before launch.</summary>
    public const bool AdminPreviewOnly = true;

    private static readonly TimeZoneInfo IsraelTimeZone = ResolveIsraelTimeZone();

    private static readonly DateTime June2026StartsAtUtc = TimeZoneInfo.ConvertTimeToUtc(
        new DateTime(2026, 6, 28, 16, 0, 0, DateTimeKind.Unspecified),
        IsraelTimeZone);

    public static bool IsActive(DateTime utcNow) => utcNow >= June2026StartsAtUtc;

    public static string GetActiveCampaignId(DateTime utcNow) =>
        IsActive(utcNow) ? June2026 : "";

    public static string GetCampaignIdForUser(DateTime utcNow, bool isAdmin)
    {
        if (AdminPreviewOnly)
            return isAdmin ? June2026 : "";

        return GetActiveCampaignId(utcNow);
    }

    private static TimeZoneInfo ResolveIsraelTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Israel Standard Time");
        }
    }
}
