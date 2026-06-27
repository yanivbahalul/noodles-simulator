using System;

namespace NoodlesSimulator.Models;

public static class FeedbackCampaigns
{
    public const string MilestoneCampaignPrefix = "feedback-milestone-";
    public const string LegacyCampaignId = "feedback-2026-06-28";

    public const string Title = "איך אתה מרגיש עם Noodles Simulator?";
    public const string Subtitle = "הדירוג והמשוב שלך יעזרו לנו לשפר";

    public const int MilestoneInterval = 5;

    /// <summary>When true, only Admin sees the feedback modal (for testing).</summary>
    public const bool AdminPreviewOnly = false;

    private static readonly TimeZoneInfo IsraelTimeZone = ResolveIsraelTimeZone();

    private static readonly DateTime CampaignStartsAtUtc = TimeZoneInfo.ConvertTimeToUtc(
        new DateTime(2026, 6, 27, 16, 0, 0, DateTimeKind.Unspecified),
        IsraelTimeZone);

    public static bool IsActive(DateTime utcNow) => utcNow >= CampaignStartsAtUtc;

    /// <summary>
    /// Achievement bucket for the current prompt (5 for 5–9, 10 for 10–14, …).
    /// Returns 0 if the user has not yet reached the first milestone.
    /// </summary>
    public static int GetActiveMilestone(int unlockedAchievementCount)
    {
        if (unlockedAchievementCount < MilestoneInterval)
            return 0;

        return unlockedAchievementCount / MilestoneInterval * MilestoneInterval;
    }

    public static string CampaignIdForMilestone(int milestone) => $"{MilestoneCampaignPrefix}{milestone}";

    public static bool IsMilestoneCampaignId(string? campaignId)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
            return false;

        if (!campaignId.StartsWith(MilestoneCampaignPrefix, StringComparison.Ordinal))
            return false;

        return int.TryParse(campaignId.AsSpan(MilestoneCampaignPrefix.Length), out var milestone) &&
               milestone >= MilestoneInterval &&
               milestone % MilestoneInterval == 0;
    }

    public static int ParseMilestoneFromCampaignId(string? campaignId)
    {
        if (!IsMilestoneCampaignId(campaignId))
            return 0;

        return int.Parse(campaignId![MilestoneCampaignPrefix.Length..]);
    }

    public static string? GetActiveCampaignIdForUser(DateTime utcNow, bool isAdmin, int unlockedAchievementCount)
    {
        if (AdminPreviewOnly)
        {
            if (!isAdmin)
                return null;

            var previewMilestone = GetActiveMilestone(unlockedAchievementCount);
            return previewMilestone > 0
                ? CampaignIdForMilestone(previewMilestone)
                : CampaignIdForMilestone(MilestoneInterval);
        }

        if (!IsActive(utcNow))
            return null;

        var milestone = GetActiveMilestone(unlockedAchievementCount);
        return milestone > 0 ? CampaignIdForMilestone(milestone) : null;
    }

    /// <summary>Whether the admin dashboard should load feedback rows.</summary>
    public static bool IsDashboardFeedbackActive(DateTime utcNow) =>
        IsActive(utcNow) || AdminPreviewOnly;

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
