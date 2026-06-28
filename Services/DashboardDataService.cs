using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class DashboardDataService
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

    private static void NormalizeUserPeriodStats(User user, string todayKey, string weekKey)
    {
        if (user == null) return;
        if (!string.Equals(user.DayKey, todayKey, StringComparison.Ordinal))
            user.DailyCorrect = 0;
        if (!string.Equals(user.WeekKey, weekKey, StringComparison.Ordinal))
            user.WeeklyCorrect = 0;
    }

    private static int CountAllowedUsers(HashSet<string> usernames, IEnumerable<User> allUsers)
    {
        if (usernames == null || usernames.Count == 0) return 0;
        return allUsers.Count(u =>
            !u.IsBanned && !u.IsCheater &&
            usernames.Contains(u.Username));
    }

    private async Task<DashboardSnapshot> BuildSnapshotAsync()
    {
        var allUsers = await _auth.GetAllUsersLightAsync();
        var todayKey = UserProgressService.TodayKey();
        var weekKey = UserProgressService.GetWeekKey();
        var todayStartUtc = DateTime.UtcNow.Date;
        var weekStartUtc = todayStartUtc.AddDays(-(int)DateTime.UtcNow.DayOfWeek);

        foreach (var user in allUsers)
            NormalizeUserPeriodStats(user, todayKey, weekKey);

        var userRows = allUsers
            .Select(MapUserRow)
            .OrderByDescending(u => u.LastSeenIso ?? "")
            .ToList();

        var online = userRows.Where(u => u.IsOnline && !u.IsCheater && !u.IsBanned).ToList();
        var top = allUsers
            .OrderByDescending(u => u.CorrectAnswers)
            .Take(5)
            .Select(MapUserRow)
            .ToList();

        var averageSuccessRate = allUsers.Where(u => u.TotalAnswered > 0)
            .Select(u => (double)u.CorrectAnswers / u.TotalAnswered)
            .DefaultIfEmpty(0)
            .Average() * 100;

        int activeToday;
        int answersToday;
        int activeThisWeek;
        int answersThisWeek;
        int dailyTotal;
        int dailyCorrect;
        int weeklyTotal;
        int weeklyCorrect;

        if (_activityEvents != null && _activityEvents.IsEnabled)
        {
            var daily = await _activityEvents.GetAnswerActivitySinceAsync(todayStartUtc);
            dailyTotal = daily.Total;
            dailyCorrect = daily.Correct;
            answersToday = daily.Correct;
            activeToday = CountAllowedUsers(daily.UsersWithCorrect, allUsers);

            var weekly = await _activityEvents.GetAnswerActivitySinceAsync(weekStartUtc);
            weeklyTotal = weekly.Total;
            weeklyCorrect = weekly.Correct;
            answersThisWeek = weekly.Correct;
            activeThisWeek = CountAllowedUsers(weekly.UsersWithCorrect, allUsers);
        }
        else
        {
            activeToday = allUsers.Count(u =>
                u.DailyCorrect > 0 && !u.IsBanned && !u.IsCheater);
            answersToday = allUsers.Sum(u => u.DailyCorrect);
            activeThisWeek = allUsers.Count(u =>
                u.WeeklyCorrect > 0 && !u.IsBanned && !u.IsCheater);
            answersThisWeek = allUsers.Sum(u => u.WeeklyCorrect);

            dailyTotal = 0;
            dailyCorrect = 0;
            weeklyTotal = 0;
            weeklyCorrect = 0;
        }

        var dailySuccessRate = dailyTotal > 0
            ? Math.Round((double)dailyCorrect / dailyTotal * 100, 1)
            : 0;
        var weeklySuccessRate = weeklyTotal > 0
            ? Math.Round((double)weeklyCorrect / weeklyTotal * 100, 1)
            : 0;

        var activeExams = await BuildActiveExamsAsync();
        var recentActivity = await BuildActivityFeedAsync(100);
        var liveActivity = recentActivity.Take(50).ToList();
        var retention = await BuildRetentionAsync(allUsers, todayStartUtc, weekStartUtc);
        var health = await GetHealthSummaryAsync();
        var questionReports = BuildQuestionReports(50);
        var problematic = await GetProblematicQuestionsAsync();

        return new DashboardSnapshot
        {
            Summary = new Summary
            {
                AllUsersCount = allUsers.Count,
                OnlineUsersCount = online.Count,
                CheatersCount = allUsers.Count(u => u.IsCheater),
                BannedUsersCount = allUsers.Count(u => u.IsBanned),
                AverageSuccessRate = Math.Round(averageSuccessRate, 1),
                ActiveToday = activeToday,
                AnswersToday = answersToday,
                DailySuccessRate = dailySuccessRate,
                ActiveThisWeek = activeThisWeek,
                AnswersThisWeek = answersThisWeek,
                WeeklySuccessRate = weeklySuccessRate,
                NewUsersToday = retention.NewUsersToday,
                NewUsersThisWeek = retention.NewUsersThisWeek,
                Inactive7Days = retention.Inactive7Days,
                Inactive30Days = retention.Inactive30Days,
                OpenQuestionReports = _questionReports?.OpenCount ?? 0
            },
            AllUsersList = userRows,
            OnlineUsersList = online,
            TopUsersList = top,
            ActiveExams = activeExams,
            RecentActivity = recentActivity,
            LiveActivity = liveActivity,
            Health = health,
            QuestionReports = questionReports,
            ProblematicQuestions = problematic
        };
    }

    private async Task<(int NewUsersToday, int NewUsersThisWeek, int Inactive7Days, int Inactive30Days)> BuildRetentionAsync(
        List<User> allUsers, DateTime todayStartUtc, DateTime weekStartUtc)
    {
        var inactive7Cutoff = DateTime.UtcNow.AddDays(-7);
        var inactive30Cutoff = DateTime.UtcNow.AddDays(-30);

        var inactive7 = allUsers.Count(u =>
            !u.IsBanned && !u.IsCheater &&
            u.TotalAnswered > 0 &&
            u.LastSeen.HasValue &&
            u.LastSeen.Value.ToUniversalTime() < inactive7Cutoff);

        var inactive30 = allUsers.Count(u =>
            !u.IsBanned && !u.IsCheater &&
            u.TotalAnswered > 0 &&
            u.LastSeen.HasValue &&
            u.LastSeen.Value.ToUniversalTime() < inactive30Cutoff);

        var newToday = 0;
        var newWeek = 0;
        if (_activityEvents != null && _activityEvents.IsEnabled)
        {
            newToday = await _activityEvents.CountEventsSinceAsync(ActivityEventCatalog.Register, todayStartUtc);
            newWeek = await _activityEvents.CountEventsSinceAsync(ActivityEventCatalog.Register, weekStartUtc);
        }

        return (newToday, newWeek, inactive7, inactive30);
    }

    private async Task<SystemHealthSummary> GetHealthSummaryAsync()
    {
        lock (_cacheLock)
        {
            if (_healthCached != null && DateTime.UtcNow - _healthCachedAt < HealthCacheTtl)
                return _healthCached;
        }

        if (_systemHealth == null)
        {
            return new SystemHealthSummary
            {
                AllOk = false,
                CheckedAtIso = DateTime.UtcNow.ToString("o"),
                Checks = new List<SystemHealthCheckRow>()
            };
        }

        try
        {
            var report = await _systemHealth.RunAsync();
            var summary = new SystemHealthSummary
            {
                AllOk = report.AllOk,
                CheckedAtIso = report.CheckedAtUtc.ToUniversalTime().ToString("o"),
                Checks = report.Checks.Select(c => new SystemHealthCheckRow
                {
                    Id = c.Id,
                    Name = c.Name,
                    Ok = c.Ok,
                    Detail = c.Detail
                }).ToList()
            };

            lock (_cacheLock)
            {
                _healthCached = summary;
                _healthCachedAt = DateTime.UtcNow;
            }

            return summary;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DashboardDataService] Health check failed: {ex.Message}");
            return new SystemHealthSummary
            {
                AllOk = false,
                CheckedAtIso = DateTime.UtcNow.ToString("o"),
                Checks = new List<SystemHealthCheckRow>
                {
                    new() { Id = "health", Name = "בדיקת מערכת", Ok = false, Detail = ex.Message }
                }
            };
        }
    }

    private List<QuestionReportRow> BuildQuestionReports(int limit)
    {
        if (_questionReports == null) return new List<QuestionReportRow>();

        return _questionReports.GetAll(limit).Select(r => new QuestionReportRow
        {
            Id = r.Id,
            Username = r.Username,
            QuestionId = r.QuestionId,
            Explanation = r.Explanation,
            Status = r.Status,
            CreatedAtIso = r.CreatedAtUtc.ToUniversalTime().ToString("o"),
            ResolvedAtIso = r.ResolvedAtUtc?.ToUniversalTime().ToString("o")
        }).ToList();
    }

    public async Task<List<ProblematicQuestionRow>> GetProblematicQuestionsAsync()
    {
        if (_difficulty == null) return new List<ProblematicQuestionRow>();

        var openReports = _questionReports?.GetOpenCountsByQuestion()
                        ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        List<QuestionDifficulty> questions;
        try
        {
            questions = await _difficulty.GetAllQuestionsAsync(500);
        }
        catch
        {
            return new List<ProblematicQuestionRow>();
        }

        var rows = new List<ProblematicQuestionRow>();
        foreach (var q in questions)
        {
            openReports.TryGetValue(q.QuestionFile, out var reportCount);
            var reason = BuildProblemReason(q, reportCount);
            if (reason == null) continue;

            rows.Add(new ProblematicQuestionRow
            {
                QuestionId = q.QuestionFile,
                Difficulty = q.Difficulty,
                SuccessRate = Math.Round((double)q.SuccessRate, 1),
                TotalAttempts = q.TotalAttempts,
                OpenReports = reportCount,
                Reason = reason
            });
        }

        return rows
            .OrderByDescending(r => r.OpenReports)
            .ThenBy(r => r.SuccessRate)
            .ThenByDescending(r => r.TotalAttempts)
            .Take(30)
            .ToList();
    }

    private static string BuildProblemReason(QuestionDifficulty q, int openReports)
    {
        if (openReports >= 2)
            return $"{openReports} דיווחים פתוחים";
        if (openReports >= 1 && q.SuccessRate < 50)
            return "דיווח פתוח + הצלחה נמוכה";
        if (q.TotalAttempts >= 5 && q.SuccessRate < 25)
            return "אחוז הצלחה נמוך מאוד";
        if (q.TotalAttempts >= 8 && q.SuccessRate < 35)
            return "קושי גבוה במדגם";
        return null;
    }

    private static UserRow MapUserRow(User u)
    {
        var level = u.Level > 0 ? u.Level : QuizGamification.LevelFromXp(u.Xp);
        return new UserRow
        {
            Username = u.Username,
            IsOnline = AuthService.UserIsOnline(u),
            LastSeenIso = u.LastSeen?.ToUniversalTime().ToString("o"),
            Level = level,
            Xp = u.Xp,
            DailyCorrect = u.DailyCorrect,
            WeeklyCorrect = u.WeeklyCorrect,
            TotalAnswered = u.TotalAnswered,
            CorrectAnswers = u.CorrectAnswers,
            SuccessRate = u.TotalAnswered > 0
                ? Math.Round((double)u.CorrectAnswers / u.TotalAnswered * 100, 1)
                : 0,
            BestExamCorrect = u.BestExamCorrect,
            BestExamScore = u.BestExamScore,
            IsCheater = u.IsCheater,
            IsBanned = u.IsBanned
        };
    }

    private async Task<List<ActiveExamRow>> BuildActiveExamsAsync()
    {
        if (_testSessions == null) return new List<ActiveExamRow>();

        var sessions = await _testSessions.GetActiveSessionsAsync(20);
        return sessions.Select(s =>
        {
            var remaining = _testSessions.GetRemainingTime(s);
            var totalQuestions = s.QuestionCount;
            if (totalQuestions <= 0 && !string.IsNullOrWhiteSpace(s.QuestionsJson))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(s.QuestionsJson);
                    if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        totalQuestions = doc.RootElement.GetArrayLength();
                }
                catch { /* ignore */ }
            }

            return new ActiveExamRow
            {
                Username = s.Username,
                Token = s.Token,
                StartedIso = s.StartedUtc.ToUniversalTime().ToString("o"),
                UpdatedIso = s.UpdatedAt.ToUniversalTime().ToString("o"),
                CurrentIndex = s.CurrentIndex,
                TotalQuestions = totalQuestions,
                Score = s.Score,
                MaxScore = s.MaxScore,
                RemainingMinutes = (int)Math.Ceiling(remaining.TotalMinutes)
            };
        }).ToList();
    }

    private async Task<List<ActivityRow>> BuildActivityFeedAsync(int limit)
    {
        if (_activityEvents == null || !_activityEvents.IsEnabled)
            return new List<ActivityRow>();

        var events = await _activityEvents.GetRecentAsync(limit);
        return events.Select(MapActivityEvent).ToList();
    }

    private static ActivityRow MapActivityEvent(ActivityEventService.ActivityEvent e)
    {
        var payload = e.Payload ?? new Dictionary<string, object>();
        return new ActivityRow
        {
            Kind = e.EventType,
            KindLabel = ActivityEventCatalog.KindLabel(e.EventType),
            Category = ActivityEventCatalog.GetCategory(e.EventType),
            Username = e.Username,
            Message = ActivityEventCatalog.FormatMessage(e.EventType, payload),
            TimestampIso = e.CreatedAt.ToUniversalTime().ToString("o")
        };
    }

    public async Task<UserDetailSnapshot> GetUserDetailAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        var user = await _auth.GetUserAsync(username);
        if (user == null) return null;

        var progress = _progressService != null
            ? await _progressService.LoadAsync(username)
            : null;

        var recentQuestions = new List<UserQuestionActivity>();
        if (_questionStatsStore?.IsEnabled == true)
        {
            var recent = await _questionStatsStore.GetRecentAsync(username, 10);
            recentQuestions = recent.Select(r => new UserQuestionActivity
            {
                QuestionId = r.QuestionId,
                Attempts = r.Stat.Attempts,
                Correct = r.Stat.Correct,
                LastAnsweredIso = r.Stat.LastAnsweredUtc.ToUniversalTime().ToString("o"),
                LastWasCorrect = r.Stat.LastWasCorrect
            }).ToList();
        }
        else if (progress?.QuestionStats != null)
        {
            recentQuestions = progress.QuestionStats
                .Where(kv => kv.Value.LastAnsweredUtc > DateTime.MinValue)
                .OrderByDescending(kv => kv.Value.LastAnsweredUtc)
                .Take(10)
                .Select(kv => new UserQuestionActivity
                {
                    QuestionId = kv.Key,
                    Attempts = kv.Value.Attempts,
                    Correct = kv.Value.Correct,
                    LastAnsweredIso = kv.Value.LastAnsweredUtc.ToUniversalTime().ToString("o"),
                    LastWasCorrect = kv.Value.LastWasCorrect
                })
                .ToList();
        }

        var achievementKeys = _progressStore?.IsEnabled == true
            ? await Task.Run(() => _progressStore.TryLoadAchievementKeys(username))
            : progress?.Achievements ?? new List<string>();

        var achievements = AchievementCatalog.All
            .Where(a => achievementKeys.Contains(a.Key, StringComparer.OrdinalIgnoreCase))
            .Select(a => new UserAchievementRow { Key = a.Key, Title = a.Title, Emoji = a.Emoji })
            .ToList();

        var exams = _testSessions != null
            ? await _testSessions.GetUserSessionsAsync(username, 15)
            : new List<TestSession>();

        return new UserDetailSnapshot
        {
            User = MapUserRow(user),
            RecentQuestions = recentQuestions,
            Achievements = achievements,
            Exams = exams.Select(e => new UserExamRow
            {
                Token = e.Token,
                Status = e.Status,
                Score = e.Score,
                MaxScore = e.MaxScore,
                StartedIso = e.StartedUtc.ToUniversalTime().ToString("o"),
                CompletedIso = e.CompletedUtc?.ToUniversalTime().ToString("o")
            }).ToList()
        };
    }

    public class UserQuestionActivity
    {
        public string QuestionId { get; set; } = "";
        public int Attempts { get; set; }
        public int Correct { get; set; }
        public string LastAnsweredIso { get; set; }
        public bool LastWasCorrect { get; set; }
    }

    public class UserAchievementRow
    {
        public string Key { get; set; } = "";
        public string Title { get; set; } = "";
        public string Emoji { get; set; } = "";
    }

    public class UserExamRow
    {
        public string Token { get; set; } = "";
        public string Status { get; set; } = "";
        public int Score { get; set; }
        public int MaxScore { get; set; }
        public string StartedIso { get; set; }
        public string CompletedIso { get; set; }
    }

    public class UserDetailSnapshot
    {
        public UserRow User { get; set; }
        public List<UserQuestionActivity> RecentQuestions { get; set; } = new();
        public List<UserAchievementRow> Achievements { get; set; } = new();
        public List<UserExamRow> Exams { get; set; } = new();
    }

    public object ToApiPayload(DashboardSnapshot snapshot)
    {
        var s = snapshot.Summary;
        return new
        {
            allUsersCount = s.AllUsersCount,
            onlineUsersCount = s.OnlineUsersCount,
            cheatersCount = s.CheatersCount,
            bannedUsersCount = s.BannedUsersCount,
            averageSuccessRate = s.AverageSuccessRate,
            activeToday = s.ActiveToday,
            answersToday = s.AnswersToday,
            dailySuccessRate = s.DailySuccessRate,
            activeThisWeek = s.ActiveThisWeek,
            answersThisWeek = s.AnswersThisWeek,
            weeklySuccessRate = s.WeeklySuccessRate,
            newUsersToday = s.NewUsersToday,
            newUsersThisWeek = s.NewUsersThisWeek,
            inactive7Days = s.Inactive7Days,
            inactive30Days = s.Inactive30Days,
            openQuestionReports = s.OpenQuestionReports,
            allUsersList = snapshot.AllUsersList.Select(ToApiUser).ToList(),
            onlineUsersList = snapshot.OnlineUsersList.Select(ToApiUser).ToList(),
            topUsersList = snapshot.TopUsersList.Select(ToApiUser).ToList(),
            activeExams = snapshot.ActiveExams.Select(ToApiExam).ToList(),
            recentActivity = snapshot.RecentActivity.Select(ToApiActivity).ToList(),
            liveActivity = snapshot.LiveActivity.Select(ToApiActivity).ToList(),
            health = new
            {
                allOk = snapshot.Health.AllOk,
                checkedAtIso = snapshot.Health.CheckedAtIso,
                checks = snapshot.Health.Checks.Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    ok = c.Ok,
                    detail = c.Detail
                })
            },
            questionReports = snapshot.QuestionReports.Select(r => new
            {
                id = r.Id,
                username = r.Username,
                questionId = r.QuestionId,
                explanation = r.Explanation,
                status = r.Status,
                createdAtIso = r.CreatedAtIso,
                resolvedAtIso = r.ResolvedAtIso
            }),
            problematicQuestions = snapshot.ProblematicQuestions.Select(q => new
            {
                questionId = q.QuestionId,
                difficulty = q.Difficulty,
                successRate = q.SuccessRate,
                totalAttempts = q.TotalAttempts,
                openReports = q.OpenReports,
                reason = q.Reason
            })
        };
    }

    public object ToApiUserDetail(UserDetailSnapshot detail) => new
    {
        user = ToApiUser(detail.User),
        recentQuestions = detail.RecentQuestions.Select(q => new
        {
            questionId = q.QuestionId,
            attempts = q.Attempts,
            correct = q.Correct,
            lastAnsweredIso = q.LastAnsweredIso,
            lastWasCorrect = q.LastWasCorrect
        }),
        achievements = detail.Achievements.Select(a => new
        {
            key = a.Key,
            title = a.Title,
            emoji = a.Emoji
        }),
        exams = detail.Exams.Select(ToApiExamRow)
    };

    private static object ToApiUser(UserRow u) => new
    {
        username = u.Username,
        isOnline = u.IsOnline,
        lastSeenIso = u.LastSeenIso,
        level = u.Level,
        xp = u.Xp,
        dailyCorrect = u.DailyCorrect,
        weeklyCorrect = u.WeeklyCorrect,
        totalAnswered = u.TotalAnswered,
        correctAnswers = u.CorrectAnswers,
        successRate = u.SuccessRate,
        bestExamCorrect = u.BestExamCorrect,
        bestExamScore = u.BestExamScore,
        isCheater = u.IsCheater,
        isBanned = u.IsBanned
    };

    private static object ToApiExam(ActiveExamRow e) => new
    {
        username = e.Username,
        token = e.Token,
        startedIso = e.StartedIso,
        updatedIso = e.UpdatedIso,
        currentIndex = e.CurrentIndex,
        totalQuestions = e.TotalQuestions,
        score = e.Score,
        maxScore = e.MaxScore,
        remainingMinutes = e.RemainingMinutes
    };

    private static object ToApiExamRow(UserExamRow e) => new
    {
        token = e.Token,
        status = e.Status,
        score = e.Score,
        maxScore = e.MaxScore,
        startedIso = e.StartedIso,
        completedIso = e.CompletedIso
    };

    private static object ToApiActivity(ActivityRow a) => new
    {
        kind = a.Kind,
        kindLabel = string.IsNullOrWhiteSpace(a.KindLabel) ? a.Kind : a.KindLabel,
        category = string.IsNullOrWhiteSpace(a.Category) ? ActivityEventCatalog.GetCategory(a.Kind) : a.Category,
        username = a.Username,
        message = a.Message,
        timestampIso = a.TimestampIso
    };
}
