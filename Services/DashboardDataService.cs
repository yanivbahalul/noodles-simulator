using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public partial class DashboardDataService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);

    private readonly AuthService _auth;
    private readonly UserProgressStore _progressStore;
    private readonly UserProgressService _progressService;
    private readonly UserQuestionStatsStore _questionStatsStore;
    private readonly TestSessionService _testSessions;
    private readonly ActivityEventService _activityEvents;
    private readonly QuestionReportService _questionReports;
    private readonly QuestionDifficultyService _difficulty;
    private readonly SystemHealthService _systemHealth;
    private readonly object _cacheLock = new();
    private DashboardSnapshot _cached;
    private DateTime _cachedAt = DateTime.MinValue;
    private SystemHealthSummary _healthCached;
    private DateTime _healthCachedAt = DateTime.MinValue;
    private static readonly TimeSpan HealthCacheTtl = TimeSpan.FromSeconds(60);

    public DashboardDataService(
        AuthService auth,
        UserProgressStore progressStore = null,
        UserProgressService progressService = null,
        UserQuestionStatsStore questionStatsStore = null,
        TestSessionService testSessions = null,
        ActivityEventService activityEvents = null,
        QuestionReportService questionReports = null,
        QuestionDifficultyService difficulty = null,
        SystemHealthService systemHealth = null)
    {
        _auth = auth;
        _progressStore = progressStore;
        _progressService = progressService;
        _questionStatsStore = questionStatsStore;
        _testSessions = testSessions;
        _activityEvents = activityEvents;
        _questionReports = questionReports;
        _difficulty = difficulty;
        _systemHealth = systemHealth;
    }

    public class UserRow
    {
        public string Username { get; set; } = "";
        public bool IsOnline { get; set; }
        public string LastSeenIso { get; set; }
        public int Level { get; set; }
        public int Xp { get; set; }
        public int DailyCorrect { get; set; }
        public int WeeklyCorrect { get; set; }
        public int TotalAnswered { get; set; }
        public int CorrectAnswers { get; set; }
        public double SuccessRate { get; set; }
        public int BestExamCorrect { get; set; }
        public int BestExamScore { get; set; }
        public bool IsCheater { get; set; }
        public bool IsBanned { get; set; }
    }

    public class ActiveExamRow
    {
        public string Username { get; set; } = "";
        public string Token { get; set; } = "";
        public string StartedIso { get; set; }
        public string UpdatedIso { get; set; }
        public int CurrentIndex { get; set; }
        public int TotalQuestions { get; set; }
        public int Score { get; set; }
        public int MaxScore { get; set; }
        public int RemainingMinutes { get; set; }
    }

    public class ActivityRow
    {
        public string Kind { get; set; } = "";
        public string KindLabel { get; set; } = "";
        public string Category { get; set; } = "";
        public string Username { get; set; } = "";
        public string Message { get; set; } = "";
        public string TimestampIso { get; set; }
    }

    public class Summary
    {
        public int AllUsersCount { get; set; }
        public int OnlineUsersCount { get; set; }
        public int CheatersCount { get; set; }
        public int BannedUsersCount { get; set; }
        public double AverageSuccessRate { get; set; }
        public int ActiveToday { get; set; }
        public int AnswersToday { get; set; }
        public double DailySuccessRate { get; set; }
        public int ActiveThisWeek { get; set; }
        public int AnswersThisWeek { get; set; }
        public double WeeklySuccessRate { get; set; }
        public int NewUsersToday { get; set; }
        public int NewUsersThisWeek { get; set; }
        public int Inactive7Days { get; set; }
        public int Inactive30Days { get; set; }
        public int OpenQuestionReports { get; set; }
    }

    public class SystemHealthSummary
    {
        public bool AllOk { get; set; }
        public string CheckedAtIso { get; set; }
        public List<SystemHealthCheckRow> Checks { get; set; } = new();
    }

    public class SystemHealthCheckRow
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Ok { get; set; }
        public string Detail { get; set; } = "";
    }

    public class QuestionReportRow
    {
        public string Id { get; set; } = "";
        public string Username { get; set; } = "";
        public string QuestionId { get; set; } = "";
        public string Explanation { get; set; } = "";
        public string Status { get; set; } = "";
        public string CreatedAtIso { get; set; }
        public string ResolvedAtIso { get; set; }
    }

    public class ProblematicQuestionRow
    {
        public string QuestionId { get; set; } = "";
        public string Difficulty { get; set; } = "";
        public double SuccessRate { get; set; }
        public int TotalAttempts { get; set; }
        public int OpenReports { get; set; }
        public string Reason { get; set; } = "";
    }

    public class DashboardSnapshot
    {
        public Summary Summary { get; set; } = new();
        public List<UserRow> AllUsersList { get; set; } = new();
        public List<UserRow> OnlineUsersList { get; set; } = new();
        public List<UserRow> TopUsersList { get; set; } = new();
        public List<ActiveExamRow> ActiveExams { get; set; } = new();
        public List<ActivityRow> RecentActivity { get; set; } = new();
        public List<ActivityRow> LiveActivity { get; set; } = new();
        public SystemHealthSummary Health { get; set; } = new();
        public List<QuestionReportRow> QuestionReports { get; set; } = new();
        public List<ProblematicQuestionRow> ProblematicQuestions { get; set; } = new();
    }

    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cached = null;
            _cachedAt = DateTime.MinValue;
        }
    }

    public async Task<DashboardSnapshot> GetSnapshotAsync(bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            lock (_cacheLock)
            {
                if (_cached != null && DateTime.UtcNow - _cachedAt < CacheTtl)
                    return _cached;
            }
        }

        var snapshot = await BuildSnapshotAsync();

        lock (_cacheLock)
        {
            _cached = snapshot;
            _cachedAt = DateTime.UtcNow;
        }

        return snapshot;
    }

    public object ToWidgetPayload(DashboardSnapshot snapshot) => DashboardApiPayload.ToWidget(snapshot);

    public object ToApiPayload(DashboardSnapshot snapshot) => DashboardApiPayload.ToApi(snapshot);

    public object ToApiUserDetail(UserDetailSnapshot detail) => DashboardApiPayload.ToUserDetail(detail);
}
