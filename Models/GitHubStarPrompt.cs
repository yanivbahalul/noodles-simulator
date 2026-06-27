using System;
using System.Collections.Generic;
using System.Linq;

namespace NoodlesSimulator.Models;

public static class GitHubStarPrompt
{
    public sealed class DashboardEntry
    {
        public string Username { get; init; } = "";
        public int Milestone { get; init; }
        public string Response { get; init; } = "";
    }

    public const string RepoUrl = "https://github.com/yanivbahalul/noodles-simulator";
    public const int MilestoneInterval = 100;
    public const string OptedInNoticeId = "github-star-opted-in";

    public const string Title = "נהנים מ-Noodles Simulator?";
    public const string Subtitle = "אם הפרויקט עוזר לך — כוכב ב-GitHub יעזור לנו המון!";
    public const string AcceptButton = "יאללה";
    public const string LaterButton = "אולי אחר כך";

    public static string NoticeIdForMilestone(int milestone) => $"github-star-{milestone}";

    public static bool HasAccepted(User? user) =>
        user?.DismissedNotices?.Contains(OptedInNoticeId, StringComparer.Ordinal) == true;

    public static bool HasDismissedMilestone(User? user, int milestone) =>
        milestone > 0 &&
        user?.DismissedNotices?.Contains(NoticeIdForMilestone(milestone), StringComparer.Ordinal) == true;

    public static bool IsValidDismissNoticeId(string? noticeId)
    {
        if (string.IsNullOrWhiteSpace(noticeId))
            return false;

        if (string.Equals(noticeId, OptedInNoticeId, StringComparison.Ordinal))
            return true;

        if (!noticeId.StartsWith("github-star-", StringComparison.Ordinal))
            return false;

        return int.TryParse(noticeId.AsSpan("github-star-".Length), out var milestone) &&
               milestone >= MilestoneInterval &&
               milestone % MilestoneInterval == 0;
    }

    /// <summary>
    /// The milestone bucket for the user's current answer count (100 for 100–199, 200 for 200–299, …).
    /// Returns 0 if the user has not yet reached the first milestone.
    /// </summary>
    public static int GetActiveMilestone(int totalAnswered)
    {
        if (totalAnswered < MilestoneInterval)
            return 0;

        return totalAnswered / MilestoneInterval * MilestoneInterval;
    }

    public static bool IsGitHubStarNotice(string? noticeId) => IsValidDismissNoticeId(noticeId);

    public static bool ShouldPrompt(User? user)
    {
        if (user == null)
            return false;

        if (HasAccepted(user))
            return false;

        var milestone = GetActiveMilestone(user.TotalAnswered);
        if (milestone == 0)
            return false;

        if (HasDismissedMilestone(user, milestone))
            return false;

        return true;
    }

    public static IEnumerable<DashboardEntry> GetDashboardEntries(User? user)
    {
        if (user == null || user.DismissedNotices == null || user.DismissedNotices.Count == 0)
            yield break;

        var prefix = "github-star-";
        foreach (var noticeId in user.DismissedNotices)
        {
            if (string.Equals(noticeId, OptedInNoticeId, StringComparison.Ordinal))
            {
                yield return new DashboardEntry
                {
                    Username = user.Username,
                    Milestone = 0,
                    Response = "accepted"
                };
                continue;
            }

            if (!noticeId.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            if (!int.TryParse(noticeId.AsSpan(prefix.Length), out var milestone) || milestone < MilestoneInterval)
                continue;

            yield return new DashboardEntry
            {
                Username = user.Username,
                Milestone = milestone,
                Response = "later"
            };
        }
    }

    public static List<DashboardEntry> GetAllDashboardEntries(IEnumerable<User> users) =>
        users.SelectMany(GetDashboardEntries)
            .OrderByDescending(e => e.Milestone)
            .ThenBy(e => e.Username, StringComparer.Ordinal)
            .ToList();
}
