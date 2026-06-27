using System;
using System.Linq;

namespace NoodlesSimulator.Models;

public static class GitHubStarPrompt
{
    public const string RepoUrl = "https://github.com/yanivbahalul/noodles-simulator";
    public const int MilestoneInterval = 100;
    public const string OptedInNoticeId = "github-star-opted-in";

    public const string Title = "נהנים מ-Noodles Simulator?";
    public const string Subtitle = "אם הפרויקט עוזר לך — כוכב ב-GitHub יעזור לנו המון!";
    public const string AcceptButton = "יאללה";
    public const string LaterButton = "אולי אחר כך";

    public static string NoticeIdForMilestone(int milestone) => $"github-star-{milestone}";

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

    public static bool IsGitHubStarNotice(string? noticeId)
    {
        if (string.IsNullOrWhiteSpace(noticeId))
            return false;

        return noticeId.StartsWith("github-star-", StringComparison.Ordinal);
    }

    public static bool ShouldPrompt(User? user)
    {
        if (user == null)
            return false;

        var milestone = GetActiveMilestone(user.TotalAnswered);
        if (milestone == 0)
            return false;

        var dismissed = user.DismissedNotices;
        if (dismissed != null)
        {
            if (dismissed.Contains(OptedInNoticeId, StringComparer.Ordinal))
                return false;

            if (dismissed.Contains(NoticeIdForMilestone(milestone), StringComparer.Ordinal))
                return false;
        }

        return true;
    }
}
