using System;

namespace NoodlesSimulator.Models;

public static class FeedbackCampaigns
{
    public const string June2026 = "feedback-2026-06-28";

    public const string Title = "איך אתה מרגיש עם Noodles Simulator?";
    public const string Subtitle = "הדירוג והמשוב שלך יעזרו לנו לשפר";

    public const int MinAchievementsRequired = 5;

    /// <summary>When true, only Admin sees the feedback modal (for testing).</summary>
    public const bool AdminPreviewOnly = false;

    private static readonly TimeZoneInfo IsraelTimeZone = ResolveIsraelTimeZone();

    private static readonly DateTime CampaignStartsAtUtc = TimeZoneInfo.ConvertTimeToUtc(
        new DateTime(2026, 6, 27, 16, 0, 0, DateTimeKind.Unspecified),
        IsraelTimeZone);

    public static bool IsActive(DateTime utcNow) => utcNow >= CampaignStartsAtUtc;

    public static bool IsEligible(int unlockedAchievementCount) =>
        unlockedAchievementCount >= MinAchievementsRequired;

    public static string GetActiveCampaignId(DateTime utcNow) =>
        IsActive(utcNow) ? June2026 : "";

    public static string GetCampaignIdForUser(DateTime utcNow, bool isAdmin)
    {
        if (AdminPreviewOnly)
            return isAdmin ? June2026 : "";

        return GetActiveCampaignId(utcNow);
    }

    /// <summary>Campaign id for admin dashboard / feedback list (includes preview submissions).</summary>
    public static string GetDashboardCampaignId(DateTime utcNow) =>
        IsActive(utcNow) || AdminPreviewOnly ? June2026 : "";

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
