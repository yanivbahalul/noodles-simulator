using System.Collections.Generic;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Models;

public static class PromptActivityEvents
{
    public const string FeedbackPrompt = ActivityEventCatalog.FeedbackPrompt;
    public const string FeedbackSubmit = ActivityEventCatalog.FeedbackSubmit;
    public const string FeedbackLater = ActivityEventCatalog.FeedbackLater;
    public const string GitHubStarPrompt = ActivityEventCatalog.GitHubStarPrompt;
    public const string GitHubStarAccept = ActivityEventCatalog.GitHubStarAccept;
    public const string GitHubStarLater = ActivityEventCatalog.GitHubStarLater;
    public const string AppNoticePrompt = ActivityEventCatalog.AppNoticePrompt;
    public const string AppNoticeDismiss = ActivityEventCatalog.AppNoticeDismiss;

    public static string KindLabel(string eventType) => ActivityEventCatalog.KindLabel(eventType);

    public static string FormatMessage(string eventType, Dictionary<string, object> payload) =>
        ActivityEventCatalog.FormatMessage(eventType, payload);

    public static void LogFeedbackPrompt(ActivityEventService svc, string username, int milestone, string campaignId) =>
        ActivityEventCatalog.LogFeedbackPrompt(svc, username, milestone, campaignId);

    public static void LogFeedbackSubmit(ActivityEventService svc, string username, int rating, string campaignId) =>
        ActivityEventCatalog.LogFeedbackSubmit(svc, username, rating, campaignId);

    public static void LogFeedbackLater(ActivityEventService svc, string username, string campaignId, int milestone) =>
        ActivityEventCatalog.LogFeedbackLater(svc, username, campaignId, milestone);

    public static void LogGitHubStarPrompt(ActivityEventService svc, string username, int milestone) =>
        ActivityEventCatalog.LogGitHubStarPrompt(svc, username, milestone);

    public static void LogGitHubStarAccept(ActivityEventService svc, string username) =>
        ActivityEventCatalog.LogGitHubStarAccept(svc, username);

    public static void LogGitHubStarLater(ActivityEventService svc, string username, int milestone) =>
        ActivityEventCatalog.LogGitHubStarLater(svc, username, milestone);

    public static void LogAppNoticePrompt(ActivityEventService svc, string username, string noticeId) =>
        ActivityEventCatalog.LogAppNoticePrompt(svc, username, noticeId);

    public static void LogAppNoticeDismiss(ActivityEventService svc, string username, string noticeId) =>
        ActivityEventCatalog.LogAppNoticeDismiss(svc, username, noticeId);
}
