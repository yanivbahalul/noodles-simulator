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
    private readonly object _cacheLock = new();
    private DashboardSnapshot _cached;
    private DateTime _cachedAt = DateTime.MinValue;

    public DashboardDataService(
        AuthService auth,
        UserProgressStore progressStore = null,
        UserProgressService progressService = null,
        UserQuestionStatsStore questionStatsStore = null,
        TestSessionService testSessions = null,
        ActivityEventService activityEvents = null)
    {
        _auth = auth;
        _progressStore = progressStore;
        _progressService = progressService;
        _questionStatsStore = questionStatsStore;
        _testSessions = testSessions;
        _activityEvents = activityEvents;
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

    private async Task<DashboardSnapshot> BuildSnapshotAsync()
    {
        var allUsers = await _auth.GetAllUsersLightAsync();
        var todayKey = UserProgressService.TodayKey();
        var weekKey = UserProgressService.GetWeekKey();
        var todayStartUtc = DateTime.UtcNow.Date;
        var weekStartUtc = todayStartUtc.AddDays(-(int)DateTime.UtcNow.DayOfWeek);

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

        var activeToday = allUsers.Count(u =>
            u.DayKey == todayKey && u.DailyCorrect > 0 && !u.IsBanned && !u.IsCheater);

        var answersToday = allUsers
            .Where(u => u.DayKey == todayKey)
            .Sum(u => u.DailyCorrect);

        var activeThisWeek = allUsers.Count(u =>
            u.WeekKey == weekKey && u.WeeklyCorrect > 0 && !u.IsBanned && !u.IsCheater);

        var answersThisWeek = allUsers
            .Where(u => u.WeekKey == weekKey)
            .Sum(u => u.WeeklyCorrect);

        var (dailyTotal, dailyCorrect) = _activityEvents != null
            ? await _activityEvents.GetAnswerStatsSinceAsync(todayStartUtc)
            : (0, 0);
        var (weeklyTotal, weeklyCorrect) = _activityEvents != null
            ? await _activityEvents.GetAnswerStatsSinceAsync(weekStartUtc)
            : (0, 0);

        var dailySuccessRate = dailyTotal > 0
            ? Math.Round((double)dailyCorrect / dailyTotal * 100, 1)
            : 0;
        var weeklySuccessRate = weeklyTotal > 0
            ? Math.Round((double)weeklyCorrect / weeklyTotal * 100, 1)
            : 0;

        var activeExams = await BuildActiveExamsAsync();
        var recentActivity = await BuildDerivedActivityAsync();
        var liveActivity = await BuildLiveActivityAsync();

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
                WeeklySuccessRate = weeklySuccessRate
            },
            AllUsersList = userRows,
            OnlineUsersList = online,
            TopUsersList = top,
            ActiveExams = activeExams,
            RecentActivity = recentActivity,
            LiveActivity = liveActivity
        };
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

    private async Task<List<ActivityRow>> BuildDerivedActivityAsync()
    {
        var items = new List<ActivityRow>();

        if (_progressStore?.IsEnabled == true)
        {
            foreach (var row in await _progressStore.FetchRecentProgressUpdatesAsync(25))
            {
                items.Add(new ActivityRow
                {
                    Kind = "progress",
                    Username = row.Username,
                    Message = "עדכן התקדמות",
                    TimestampIso = row.UpdatedAt.ToUniversalTime().ToString("o")
                });
            }

            foreach (var row in await _progressStore.FetchRecentAchievementsAsync(25))
            {
                var def = AchievementCatalog.Find(row.AchievementKey);
                var title = def?.Title ?? row.AchievementKey;
                items.Add(new ActivityRow
                {
                    Kind = "achievement",
                    Username = row.Username,
                    Message = $"פתח הישג: {title}",
                    TimestampIso = row.UnlockedAt.ToUniversalTime().ToString("o")
                });
            }
        }

        if (_testSessions != null)
        {
            foreach (var s in await _testSessions.GetRecentCompletedSessionsAsync(25))
            {
                var when = s.CompletedUtc ?? s.UpdatedAt;
                items.Add(new ActivityRow
                {
                    Kind = "exam_complete",
                    Username = s.Username,
                    Message = $"סיים מבחן {s.Score}/{s.MaxScore}",
                    TimestampIso = when.ToUniversalTime().ToString("o")
                });
            }
        }

        return items
            .Where(a => !string.IsNullOrWhiteSpace(a.TimestampIso))
            .OrderByDescending(a => a.TimestampIso, StringComparer.Ordinal)
            .Take(40)
            .ToList();
    }

    private async Task<List<ActivityRow>> BuildLiveActivityAsync()
    {
        if (_activityEvents == null || !_activityEvents.IsEnabled)
            return new List<ActivityRow>();

        var events = await _activityEvents.GetRecentAsync(50);
        return events.Select(e => new ActivityRow
        {
            Kind = e.EventType,
            Username = e.Username,
            Message = FormatLiveMessage(e),
            TimestampIso = e.CreatedAt.ToUniversalTime().ToString("o")
        }).ToList();
    }

    private static string FormatLiveMessage(ActivityEventService.ActivityEvent e)
    {
        var payload = e.Payload ?? new Dictionary<string, object>();
        return e.EventType switch
        {
            "answer" => FormatAnswerMessage(payload),
            "exam_start" => "התחיל מבחן",
            "exam_complete" => FormatExamCompleteMessage(payload),
            "achievement" => FormatAchievementMessage(payload),
            "login" => "התחבר",
            _ => e.EventType
        };
    }

    private static string FormatAnswerMessage(Dictionary<string, object> payload)
    {
        var correct = payload.TryGetValue("correct", out var c) && c is bool b && b;
        var questionId = payload.TryGetValue("questionId", out var q) ? q?.ToString() : "";
        var mode = payload.TryGetValue("mode", out var m) ? m?.ToString() : "normal";
        var modeLabel = mode switch
        {
            "weak" => "חולשות",
            "review" => "חזרה",
            "daily" => "אתגר יומי",
            _ => "רגיל"
        };
        var shortQ = string.IsNullOrWhiteSpace(questionId)
            ? "שאלה"
            : (questionId.Length > 28 ? questionId[..25] + "…" : questionId);
        return correct
            ? $"ענה נכון על {shortQ} ({modeLabel})"
            : $"ענה שגוי על {shortQ} ({modeLabel})";
    }

    private static string FormatExamCompleteMessage(Dictionary<string, object> payload)
    {
        var score = payload.TryGetValue("score", out var s) ? s?.ToString() : "?";
        var max = payload.TryGetValue("maxScore", out var m) ? m?.ToString() : "?";
        return $"סיים מבחן {score}/{max}";
    }

    private static string FormatAchievementMessage(Dictionary<string, object> payload)
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

    public async Task<UserDetailSnapshot> GetUserDetailAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        var user = await _auth.GetUserAsync(username);
        if (user == null) return null;

        var progress = _progressService?.Load(username);

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

        List<string> achievementKeys;
        if (_progressStore?.IsEnabled == true)
            achievementKeys = await Task.Run(() => _progressStore.TryLoadAchievementKeys(username));
        else
            achievementKeys = progress?.Achievements ?? new List<string>();

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
            allUsersList = snapshot.AllUsersList.Select(ToApiUser).ToList(),
            onlineUsersList = snapshot.OnlineUsersList.Select(ToApiUser).ToList(),
            topUsersList = snapshot.TopUsersList.Select(ToApiUser).ToList(),
            activeExams = snapshot.ActiveExams.Select(ToApiExam).ToList(),
            recentActivity = snapshot.RecentActivity.Select(ToApiActivity).ToList(),
            liveActivity = snapshot.LiveActivity.Select(ToApiActivity).ToList()
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
        username = a.Username,
        message = a.Message,
        timestampIso = a.TimestampIso
    };
}
