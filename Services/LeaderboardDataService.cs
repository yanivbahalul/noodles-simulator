using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class LeaderboardDataService
{
    private const int LeaderboardSize = 50;
    private static readonly TimeSpan RowsCacheTtl = TimeSpan.FromSeconds(3);

    private readonly AuthService _auth;
    private readonly UserProgressService _progress;
    private readonly UserProgressStore _progressStore;
    private readonly TestSessionService _testSessions;
    private readonly object _cacheLock = new();
    private string _cachedTab;
    private List<Row> _cachedRows;
    private string _cachedHint;
    private DateTime _cachedAt = DateTime.MinValue;

    public LeaderboardDataService(
        AuthService auth,
        UserProgressService progress = null,
        UserProgressStore progressStore = null,
        TestSessionService testSessions = null)
    {
        _auth = auth;
        _progress = progress;
        _progressStore = progressStore;
        _testSessions = testSessions;
    }

    public class Row
    {
        public string Username { get; set; } = "";
        public string ScoreDisplay { get; set; } = "";
        public bool IsOnline { get; set; }
    }

    public async Task<(List<Row> Rows, string Hint)> GetRowsAsync(string tab)
    {
        tab = string.IsNullOrWhiteSpace(tab) ? "total" : tab;
        if (tab == "daily") tab = "level";

        lock (_cacheLock)
        {
            if (_cachedRows != null &&
                string.Equals(_cachedTab, tab, StringComparison.OrdinalIgnoreCase) &&
                DateTime.UtcNow - _cachedAt < RowsCacheTtl)
            {
                return (_cachedRows, _cachedHint);
            }
        }

        var (rows, hint) = tab switch
        {
            "rate" => (await BuildSuccessRateAsync(), "דירוג לפי אחוז הצלחה"),
            "weekly" => (await BuildWeeklyAsync(), "דירוג לפי נכונות השבוע"),
            "exam" => (await BuildExamAsync(), "דירוג לפי מבחנים שהושלמו"),
            "achievement" or "achievements" => (await BuildAchievementsAsync(), "דירוג לפי הישגים"),
            "level" or "daily" => (await BuildLevelAsync(), "דירוג לפי רמה"),
            _ => (await BuildTotalAsync(), "דירוג לפי סה\"כ תשובות נכונות")
        };

        lock (_cacheLock)
        {
            _cachedTab = tab;
            _cachedRows = rows;
            _cachedHint = hint;
            _cachedAt = DateTime.UtcNow;
        }

        return (rows, hint);
    }

    private bool UserIsOnline(User u) => AuthService.UserIsOnline(u);

    private async Task<List<Row>> BuildTotalAsync()
    {
        var users = await _auth.GetTopUsersAsync(LeaderboardSize);
        return users.Select(u => new Row
        {
            Username = u.Username,
            ScoreDisplay = u.CorrectAnswers.ToString(),
            IsOnline = UserIsOnline(u)
        }).ToList();
    }

    private static IEnumerable<User> Eligible(IEnumerable<User> users) =>
        users.Where(u => !u.IsBanned && !u.IsCheater);

    private static IEnumerable<KeyValuePair<string, int>> TopScores(
        Dictionary<string, int> scores,
        IEnumerable<User> eligibleUsers,
        int limit = LeaderboardSize)
    {
        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in eligibleUsers)
        {
            if (string.IsNullOrWhiteSpace(u.Username)) continue;
            if (!totals.TryGetValue(u.Username, out var existing) || u.CorrectAnswers > existing)
                totals[u.Username] = u.CorrectAnswers;
        }

        return scores
            .OrderByDescending(kv => kv.Value)
            .ThenByDescending(kv => totals.TryGetValue(kv.Key, out var total) ? total : 0)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Take(limit);
    }

    private static double SuccessRate(User u) =>
        u.TotalAnswered > 0 ? (double)u.CorrectAnswers / u.TotalAnswered : -1;

    private static string FormatSuccessRate(User u)
    {
        if (u.TotalAnswered <= 0) return "0%";
        var pct = (int)Math.Round(Math.Min(1.0, SuccessRate(u)) * 100);
        return $"{pct}%";
    }

    private async Task<List<User>> GetLeaderboardPoolAsync()
    {
        return Eligible(await _auth.GetTopUsersAsync(LeaderboardSize)).ToList();
    }

    private async Task<List<Row>> BuildSuccessRateAsync()
    {
        var users = Eligible(await _auth.GetTopUsersBySuccessRateAsync(LeaderboardSize))
            .Take(LeaderboardSize)
            .ToList();

        return users.Select(u => new Row
        {
            Username = u.Username,
            ScoreDisplay = FormatSuccessRate(u),
            IsOnline = UserIsOnline(u)
        }).ToList();
    }

    private async Task<List<Row>> BuildWeeklyAsync()
    {
        var weekKey = UserProgressService.GetWeekKey();
        var pool = await GetLeaderboardPoolAsync();
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var u in pool)
            scores[u.Username] = u.WeekKey == weekKey ? u.WeeklyCorrect : 0;

        if (_progress != null)
        {
            foreach (var (username, weeklyCorrect) in _progress.GetWeeklyLeaderboard(LeaderboardSize))
            {
                if (!scores.ContainsKey(username))
                    scores[username] = weeklyCorrect;
                else if (weeklyCorrect > scores[username])
                    scores[username] = weeklyCorrect;
            }
        }

        return ToRows(TopScores(scores, pool), pool, kv => kv.Value.ToString());
    }

    private async Task<List<Row>> BuildExamAsync()
    {
        var pool = await GetLeaderboardPoolAsync();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var u in pool)
            counts[u.Username] = 0;

        if (_testSessions != null)
        {
            foreach (var (username, examCount) in await _testSessions.GetExamCountLeaderboardAsync(LeaderboardSize))
            {
                if (!counts.ContainsKey(username))
                    counts[username] = examCount;
                else if (examCount > counts[username])
                    counts[username] = examCount;
            }
        }

        if (_progress != null)
        {
            foreach (var (username, examCount) in _progress.GetExamCountLeaderboard(LeaderboardSize))
            {
                if (!counts.ContainsKey(username))
                    counts[username] = examCount;
                else if (examCount > counts[username])
                    counts[username] = examCount;
            }
        }

        return ToRows(TopScores(counts, pool), pool, kv => kv.Value.ToString());
    }

    private async Task<List<Row>> BuildAchievementsAsync()
    {
        var pool = await GetLeaderboardPoolAsync();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var u in pool)
            counts[u.Username] = 0;

        if (_progressStore?.IsEnabled == true)
        {
            foreach (var (username, achievementCount) in await _progressStore.GetAchievementCountLeaderboardAsync(LeaderboardSize))
            {
                if (!counts.ContainsKey(username))
                    counts[username] = achievementCount;
                else if (achievementCount > counts[username])
                    counts[username] = achievementCount;
            }
        }

        if (_progress != null)
        {
            foreach (var (username, achievementCount) in _progress.GetAchievementCountLeaderboard(LeaderboardSize))
            {
                if (!counts.ContainsKey(username))
                    counts[username] = achievementCount;
                else if (achievementCount > counts[username])
                    counts[username] = achievementCount;
            }
        }

        return ToRows(TopScores(counts, pool), pool, kv => kv.Value.ToString());
    }

    private List<Row> ToRows(IEnumerable<KeyValuePair<string, int>> ordered, List<User> onlineLookup, Func<KeyValuePair<string, int>, string> format)
    {
        return ordered.Select(kv => new Row
        {
            Username = kv.Key,
            ScoreDisplay = format(kv),
            IsOnline = onlineLookup.Any(u =>
                string.Equals(u.Username, kv.Key, StringComparison.OrdinalIgnoreCase) && UserIsOnline(u))
        }).ToList();
    }

    private async Task<List<Row>> BuildLevelAsync()
    {
        var pool = await GetLeaderboardPoolAsync();
        var xpMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var u in pool)
            xpMap[u.Username] = Math.Max(0, u.Xp);

        if (_progress != null)
        {
            foreach (var (username, xp) in _progress.GetXpByUsername())
            {
                if (string.IsNullOrWhiteSpace(username) || xp <= 0) continue;
                if (!xpMap.TryGetValue(username, out var current) || xp > current)
                    xpMap[username] = xp;
            }
        }

        if (_progressStore?.IsEnabled == true)
        {
            foreach (var (username, xp) in _progressStore.GetAllXpCached())
            {
                if (string.IsNullOrWhiteSpace(username) || xp <= 0) continue;
                if (!xpMap.TryGetValue(username, out var current) || xp > current)
                    xpMap[username] = xp;
            }
        }

        return xpMap
            .Select(kv => new
            {
                kv.Key,
                kv.Value,
                Level = QuizGamification.LevelFromXp(kv.Value)
            })
            .OrderByDescending(x => x.Level)
            .ThenByDescending(x => x.Value)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Take(LeaderboardSize)
            .Select(x => new Row
            {
                Username = x.Key,
                ScoreDisplay = x.Level.ToString(),
                IsOnline = pool.Any(u =>
                    string.Equals(u.Username, x.Key, StringComparison.OrdinalIgnoreCase) && UserIsOnline(u))
            })
            .ToList();
    }
}
