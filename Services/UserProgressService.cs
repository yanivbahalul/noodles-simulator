using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class UserProgressService
{
    private static readonly AsyncLocal<Dictionary<string, UserProgressData>> RequestCache = new();

    private readonly string _progressDir;
    private readonly AuthService _auth;
    private readonly UserProgressStore _store;
    private readonly UserStatsService _stats;
    private readonly UserQuestionStatsStore _questionStats;
    private readonly object _lock = new();

    public class UserQuestionStat
    {
        public int Attempts { get; set; }
        public int Correct { get; set; }
        public DateTime LastAnsweredUtc { get; set; }
        public bool LastWasCorrect { get; set; }
    }

    /// <summary>In-memory aggregate; persisted stats live in user_stats / user_question_stats / user_achievements.</summary>
    public class UserProgressData
    {
        public Dictionary<string, UserQuestionStat> QuestionStats { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Achievements { get; set; } = new();
        public int Xp { get; set; }
        public int BestStreak { get; set; }
        public List<string> SessionMistakes { get; set; } = new();
        public int WeeklyCorrect { get; set; }
        public string WeekKey { get; set; } = "";
        public int DailyCorrect { get; set; }
        public string DayKey { get; set; } = "";
        public int DailyChallengeScore { get; set; }
        public string DailyChallengeDate { get; set; } = "";
        public int DailyChallengesCompleted { get; set; }
        public int DailyPerfectCount { get; set; }
        public int DailyStreakDays { get; set; }
        public string LastDailyCompleteDate { get; set; } = "";
        public int ExamsCompleted { get; set; }
        public int BestExamScore { get; set; }
        public int BestExamCorrect { get; set; }
        public int HardCorrectCount { get; set; }
        public int WeakModeCorrectCount { get; set; }
        public int LastExamCorrect { get; set; }
        public int PerfectExamsCount { get; set; }
        public int MaxExamImprovement { get; set; }
        public int ReviewClearCount { get; set; }
    }

    /// <summary>Slim document stored in user_progress.ProgressData (no duplicated stats).</summary>
    public class ProgressDataDocument
    {
        public int BestStreak { get; set; }
        public List<string> SessionMistakes { get; set; } = new();
        public int DailyChallengesCompleted { get; set; }
        public int DailyPerfectCount { get; set; }
        public int DailyStreakDays { get; set; }
        public string LastDailyCompleteDate { get; set; } = "";
        public int ExamsCompleted { get; set; }
        public int LastExamCorrect { get; set; }
        public int PerfectExamsCount { get; set; }
        public int MaxExamImprovement { get; set; }
        public int ReviewClearCount { get; set; }
        public int HardCorrectCount { get; set; }
        public int WeakModeCorrectCount { get; set; }
    }

    public UserProgressService(
        string progressDir,
        AuthService auth = null,
        UserProgressStore store = null,
        UserStatsService stats = null,
        UserQuestionStatsStore questionStats = null)
    {
        _progressDir = progressDir;
        _auth = auth;
        _store = store;
        _stats = stats;
        _questionStats = questionStats;
        Directory.CreateDirectory(_progressDir);
    }

    public void ClearRequestCache() => RequestCache.Value = null;

    private Dictionary<string, UserProgressData> GetRequestCache()
    {
        var cache = RequestCache.Value;
        if (cache != null) return cache;
        cache = new Dictionary<string, UserProgressData>(StringComparer.OrdinalIgnoreCase);
        RequestCache.Value = cache;
        return cache;
    }

    private string PathFor(string username) =>
        System.IO.Path.Combine(_progressDir, $"{Sanitize(username)}.json");

    private static string Sanitize(string username) =>
        string.Join("_", username.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

    public UserProgressData Load(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return new UserProgressData { WeekKey = GetWeekKey(), DayKey = TodayKey() };

        var cache = GetRequestCache();
        if (cache.TryGetValue(username, out var cached))
        {
            EnsureWeek(cached);
            EnsureDay(cached);
            return cached;
        }

        lock (_lock)
        {
            if (cache.TryGetValue(username, out cached))
            {
                EnsureWeek(cached);
                EnsureDay(cached);
                return cached;
            }

            var data = LoadFromStorage(username);
            EnsureWeek(data);
            EnsureDay(data);
            cache[username] = data;
            return data;
        }
    }

    private UserProgressData LoadFromStorage(string username)
    {
        ProgressDataDocument doc = null;

        var path = PathFor(username);
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                doc = TryDeserializeDocument(json);
            }
            catch { /* fall through */ }
        }

        if (doc == null && _store?.IsEnabled == true)
        {
            var (fromDb, _) = _store.TryLoadDocumentWithMeta(username);
            doc = fromDb;
        }

        var data = doc != null ? FromDocument(doc) : new UserProgressData { WeekKey = GetWeekKey(), DayKey = TodayKey() };

        if (_stats?.IsEnabled == true)
        {
            var statsRow = _stats.GetAsync(username).GetAwaiter().GetResult();
            if (statsRow != null)
                UserStatsService.ApplyToProgress(statsRow, data);
        }

        if (_questionStats?.IsEnabled == true)
        {
            var qStats = _questionStats.LoadForUserAsync(username).GetAwaiter().GetResult();
            if (qStats.Count > 0)
                data.QuestionStats = qStats;
        }
        else if (doc == null && File.Exists(path))
        {
            try
            {
                var legacyJson = File.ReadAllText(path, Encoding.UTF8);
                var legacy = JsonSerializer.Deserialize<UserProgressData>(legacyJson, AppJson.Options);
                if (legacy?.QuestionStats?.Count > 0)
                    data.QuestionStats = legacy.QuestionStats;
            }
            catch { /* ignore */ }
        }

        if (_store?.IsEnabled == true)
        {
            var keys = _store.TryLoadAchievementKeys(username);
            data.Achievements = keys ?? new List<string>();
        }
        else if (File.Exists(path))
        {
            try
            {
                var legacyJson = File.ReadAllText(path, Encoding.UTF8);
                using var legacyDoc = JsonDocument.Parse(legacyJson);
                if (legacyDoc.RootElement.TryGetProperty("Achievements", out var achEl) &&
                    achEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in achEl.EnumerateArray())
                    {
                        var key = item.GetString();
                        if (!string.IsNullOrWhiteSpace(key) &&
                            !data.Achievements.Contains(key, StringComparer.OrdinalIgnoreCase))
                            data.Achievements.Add(key);
                    }
                }
            }
            catch { /* ignore */ }
        }

        if (doc == null && !File.Exists(path))
            WriteFile(username, data);

        return data;
    }

    private static ProgressDataDocument TryDeserializeDocument(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("QuestionStats", out _) ||
                doc.RootElement.TryGetProperty("Achievements", out _) ||
                doc.RootElement.TryGetProperty("Xp", out _))
            {
                var legacy = JsonSerializer.Deserialize<UserProgressData>(json, AppJson.Options);
                return legacy != null ? ToDocument(legacy) : null;
            }

            return JsonSerializer.Deserialize<ProgressDataDocument>(json, AppJson.Options);
        }
        catch
        {
            return null;
        }
    }

    private static UserProgressData FromDocument(ProgressDataDocument doc)
    {
        return new UserProgressData
        {
            BestStreak = doc.BestStreak,
            SessionMistakes = doc.SessionMistakes ?? new List<string>(),
            DailyChallengesCompleted = doc.DailyChallengesCompleted,
            DailyPerfectCount = doc.DailyPerfectCount,
            DailyStreakDays = doc.DailyStreakDays,
            LastDailyCompleteDate = doc.LastDailyCompleteDate ?? "",
            ExamsCompleted = doc.ExamsCompleted,
            LastExamCorrect = doc.LastExamCorrect,
            PerfectExamsCount = doc.PerfectExamsCount,
            MaxExamImprovement = doc.MaxExamImprovement,
            ReviewClearCount = doc.ReviewClearCount,
            HardCorrectCount = doc.HardCorrectCount,
            WeakModeCorrectCount = doc.WeakModeCorrectCount,
            WeekKey = GetWeekKey(),
            DayKey = TodayKey()
        };
    }

    public static ProgressDataDocument ToDocument(UserProgressData data)
    {
        if (data == null) return new ProgressDataDocument();
        return new ProgressDataDocument
        {
            BestStreak = data.BestStreak,
            SessionMistakes = data.SessionMistakes ?? new List<string>(),
            DailyChallengesCompleted = data.DailyChallengesCompleted,
            DailyPerfectCount = data.DailyPerfectCount,
            DailyStreakDays = data.DailyStreakDays,
            LastDailyCompleteDate = data.LastDailyCompleteDate ?? "",
            ExamsCompleted = data.ExamsCompleted,
            LastExamCorrect = data.LastExamCorrect,
            PerfectExamsCount = data.PerfectExamsCount,
            MaxExamImprovement = data.MaxExamImprovement,
            ReviewClearCount = data.ReviewClearCount,
            HardCorrectCount = data.HardCorrectCount,
            WeakModeCorrectCount = data.WeakModeCorrectCount
        };
    }

    private void WriteFile(string username, UserProgressData data)
    {
        var path = PathFor(username);
        var tmp = path + ".tmp";
        var json = JsonSerializer.Serialize(ToDocument(data), AppJson.Options);
        File.WriteAllText(tmp, json, Encoding.UTF8);
        File.Copy(tmp, path, overwrite: true);
        File.Delete(tmp);
    }

    private void Save(string username, UserProgressData data)
    {
        GetRequestCache()[username] = data;

        lock (_lock)
        {
            WriteFile(username, data);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                if (_stats?.IsEnabled == true)
                    await _stats.UpsertAsync(UserStatsService.FromProgress(username, data));

                if (_store?.IsEnabled == true)
                    _store.SaveDocument(username, ToDocument(data));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserProgressService] Supabase save failed for {username}: {ex.Message}");
            }
        });

        QueueDbSync(username, data);
    }

    private void QueueDbSync(string username, UserProgressData data)
    {
        if (_auth == null || string.IsNullOrWhiteSpace(username)) return;
        _ = _auth.SyncLeaderboardStatsAsync(
            username,
            data.WeeklyCorrect,
            data.WeekKey,
            data.DailyCorrect,
            data.DayKey,
            data.DailyChallengeScore,
            data.DailyChallengeDate,
            data.BestExamScore,
            data.BestExamCorrect);
    }

    public void RecordAnswer(string username, string questionId, bool isCorrect, int xpGained)
    {
        if (string.IsNullOrWhiteSpace(username)) return;
        var data = Load(username);
        EnsureWeek(data);
        EnsureDay(data);

        if (!string.IsNullOrWhiteSpace(questionId))
        {
            if (!data.QuestionStats.TryGetValue(questionId, out var stat))
            {
                stat = new UserQuestionStat();
                data.QuestionStats[questionId] = stat;
            }
            stat.Attempts++;
            if (isCorrect) stat.Correct++;
            stat.LastAnsweredUtc = DateTime.UtcNow;
            stat.LastWasCorrect = isCorrect;

            if (_questionStats?.IsEnabled == true)
                _ = _questionStats.RecordAnswerAsync(username, questionId, isCorrect);

            if (!isCorrect && !data.SessionMistakes.Contains(questionId, StringComparer.OrdinalIgnoreCase))
                data.SessionMistakes.Add(questionId);
        }

        if (isCorrect)
        {
            data.WeeklyCorrect++;
            data.DailyCorrect++;
            data.Xp += Math.Max(0, xpGained);
        }

        Save(username, data);
    }

    public void ResetAll(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return;

        var fresh = new UserProgressData
        {
            WeekKey = GetWeekKey(),
            DayKey = TodayKey()
        };

        GetRequestCache()[username] = fresh;

        lock (_lock)
        {
            WriteFile(username, fresh);
        }

        try
        {
            _store?.SaveDocument(username, ToDocument(fresh));
            _store?.ClearAchievements(username);
            if (_questionStats?.IsEnabled == true)
                _questionStats.ClearForUserAsync(username).GetAwaiter().GetResult();
            if (_stats?.IsEnabled == true)
                _stats.UpsertAsync(UserStatsService.FromProgress(username, fresh)).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserProgressService] ResetAll Supabase failed for {username}: {ex.Message}");
        }

        QueueDbSync(username, fresh);
    }

    public int RecordExamComplete(string username, int correctCount, int totalQuestions, int score)
    {
        var data = Load(username);
        var previousExamCorrect = data.LastExamCorrect;
        var hadPreviousExam = data.ExamsCompleted > 0;
        data.ExamsCompleted++;
        if (correctCount == totalQuestions && totalQuestions > 0)
            data.PerfectExamsCount++;
        if (hadPreviousExam)
        {
            var improvement = correctCount - previousExamCorrect;
            if (improvement > data.MaxExamImprovement)
                data.MaxExamImprovement = improvement;
        }
        data.LastExamCorrect = correctCount;
        if (correctCount > data.BestExamCorrect)
        {
            data.BestExamCorrect = correctCount;
            data.BestExamScore = score;
        }
        data.Xp += score;
        Save(username, data);
        return previousExamCorrect;
    }

    public void RecordDailyChallengeAnswer(string username, bool isCorrect)
    {
        var data = Load(username);
        var today = TodayKey();
        if (data.DailyChallengeDate != today)
        {
            data.DailyChallengeDate = today;
            data.DailyChallengeScore = 0;
        }
        if (isCorrect) data.DailyChallengeScore++;
        Save(username, data);
    }

    public void RecordDailyChallengeComplete(string username, bool isPerfect)
    {
        var data = Load(username);
        var today = TodayKey();
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        if (data.LastDailyCompleteDate == today)
            return;

        if (data.LastDailyCompleteDate == yesterday)
            data.DailyStreakDays++;
        else
            data.DailyStreakDays = 1;

        data.LastDailyCompleteDate = today;
        data.DailyChallengesCompleted++;
        if (isPerfect)
            data.DailyPerfectCount++;

        data.Xp += QuizGamification.DailyChallengeCompletionXp(data.DailyChallengeScore);
        Save(username, data);
    }

    public void IncrementReviewClear(string username)
    {
        var data = Load(username);
        data.ReviewClearCount++;
        Save(username, data);
    }

    public void IncrementWeakCorrect(string username)
    {
        var data = Load(username);
        data.WeakModeCorrectCount++;
        Save(username, data);
    }

    public void IncrementHardCorrect(string username)
    {
        var data = Load(username);
        data.HardCorrectCount++;
        Save(username, data);
    }

    public bool RemoveSessionMistake(string username, string questionId)
    {
        if (string.IsNullOrWhiteSpace(questionId)) return false;
        var data = Load(username);
        var hadMistakes = data.SessionMistakes.Count > 0;
        data.SessionMistakes.RemoveAll(q => string.Equals(q, questionId, StringComparison.OrdinalIgnoreCase));
        var cleared = hadMistakes && data.SessionMistakes.Count == 0;
        Save(username, data);
        return cleared;
    }

    public (double Accuracy, int DistinctQuestions) GetOverallAccuracyStats(string username)
    {
        var data = Load(username);
        var stats = data.QuestionStats.Values.Where(s => s.Attempts > 0).ToList();
        var distinct = stats.Count;
        if (distinct == 0) return (0, 0);
        var totalAttempts = stats.Sum(s => s.Attempts);
        var totalCorrect = stats.Sum(s => s.Correct);
        if (totalAttempts == 0) return (0, distinct);
        return (totalCorrect / (double)totalAttempts, distinct);
    }

    public void ClearSessionMistakes(string username)
    {
        var data = Load(username);
        data.SessionMistakes.Clear();
        Save(username, data);
    }

    public void AddSessionMistakes(string username, IEnumerable<string> questionIds)
    {
        var data = Load(username);
        foreach (var q in questionIds)
        {
            if (string.IsNullOrWhiteSpace(q)) continue;
            if (!data.SessionMistakes.Contains(q, StringComparer.OrdinalIgnoreCase))
                data.SessionMistakes.Add(q);
        }
        Save(username, data);
    }

    public List<string> UnlockAchievements(string username, IEnumerable<string> keys)
    {
        var data = Load(username);
        var newly = new List<string>();
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (!data.Achievements.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                data.Achievements.Add(key);
                newly.Add(key);
            }
        }

        if (newly.Count > 0)
        {
            GetRequestCache()[username] = data;
            if (_store?.IsEnabled == true)
                _store.SyncAchievements(username, newly);
        }

        return newly;
    }

    public bool HasAchievement(string username, string key)
    {
        var data = Load(username);
        return data.Achievements.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

    public double GetUserSuccessRate(string username, string questionId)
    {
        var data = Load(username);
        if (!data.QuestionStats.TryGetValue(questionId, out var stat) || stat.Attempts == 0)
            return 1.0;
        return (double)stat.Correct / stat.Attempts;
    }

    public List<string> GetWeakQuestions(string username, double threshold = 0.5)
    {
        var data = Load(username);
        return data.QuestionStats
            .Where(kv => kv.Value.Attempts >= 1 && (double)kv.Value.Correct / kv.Value.Attempts < threshold)
            .Select(kv => kv.Key)
            .ToList();
    }

    public int GetSpacedPriority(string username, string questionId) =>
        GetSpacedPriority(Load(username), questionId);

    public int GetSpacedPriority(UserProgressData data, string questionId)
    {
        if (data == null) return 2;
        if (!data.QuestionStats.TryGetValue(questionId, out var stat))
            return 2;

        if (!stat.LastWasCorrect && (DateTime.UtcNow - stat.LastAnsweredUtc).TotalHours < 24)
            return 3;

        if ((DateTime.UtcNow - stat.LastAnsweredUtc).TotalDays > 7)
            return 2;

        return 1;
    }

    public void UpdateBestStreak(string username, int streak)
    {
        if (streak <= 0) return;
        var data = Load(username);
        if (streak > data.BestStreak)
        {
            data.BestStreak = streak;
            Save(username, data);
        }
    }

    public Dictionary<string, int> GetXpByUsername()
    {
        if (_stats?.IsEnabled == true)
        {
            var all = _stats.GetAllCachedAsync().GetAwaiter().GetResult();
            return all.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Xp,
                StringComparer.OrdinalIgnoreCase);
        }

        var results = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(_progressDir)) return results;

        foreach (var file in Directory.GetFiles(_progressDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var doc = TryDeserializeDocument(json);
                if (doc == null) continue;
                var username = System.IO.Path.GetFileNameWithoutExtension(file);
                if (!results.ContainsKey(username))
                    results[username] = 0;
            }
            catch { /* skip */ }
        }

        return results;
    }

    public List<(string Username, int Score)> GetDailyLeaderboard(string date, int limit = 50)
    {
        if (_stats?.IsEnabled == true)
        {
            var all = _stats.GetAllCachedAsync().GetAwaiter().GetResult();
            return all.Values
                .Where(s => s.DayKey == date && s.DailyCorrect > 0)
                .OrderByDescending(s => s.DailyCorrect)
                .Take(limit)
                .Select(s => (s.Username, s.DailyCorrect))
                .ToList();
        }

        return new List<(string, int)>();
    }

    public List<(string Username, int WeeklyCorrect)> GetWeeklyLeaderboard(int limit = 50)
    {
        var weekKey = GetWeekKey();
        if (_stats?.IsEnabled == true)
        {
            var all = _stats.GetAllCachedAsync().GetAwaiter().GetResult();
            return all.Values
                .Where(s => s.WeekKey == weekKey && s.WeeklyCorrect > 0)
                .OrderByDescending(s => s.WeeklyCorrect)
                .Take(limit)
                .Select(s => (s.Username, s.WeeklyCorrect))
                .ToList();
        }

        return new List<(string, int)>();
    }

    public List<(string Username, int ExamCount)> GetExamCountLeaderboard(int limit = 50)
    {
        var results = new List<(string, int)>();
        if (!Directory.Exists(_progressDir)) return results;

        foreach (var file in Directory.GetFiles(_progressDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var doc = JsonSerializer.Deserialize<ProgressDataDocument>(json, AppJson.Options);
                if (doc is { ExamsCompleted: > 0 })
                {
                    var username = System.IO.Path.GetFileNameWithoutExtension(file);
                    results.Add((username, doc.ExamsCompleted));
                }
            }
            catch { /* skip */ }
        }

        return results.OrderByDescending(r => r.Item2).Take(limit).ToList();
    }

    public List<(string Username, int AchievementCount)> GetAchievementCountLeaderboard(int limit = 50)
    {
        return new List<(string, int)>();
    }

    public (int TotalAnswered, int CorrectAnswers) GetAnswerTotals(string username)
    {
        var data = Load(username);
        if (data?.QuestionStats == null || data.QuestionStats.Count == 0)
            return (0, 0);

        var total = 0;
        var correct = 0;
        foreach (var stat in data.QuestionStats.Values)
        {
            total += stat.Attempts;
            correct += stat.Correct;
        }

        return (total, correct);
    }

    private static void EnsureDay(UserProgressData data)
    {
        var dayKey = TodayKey();
        if (data.DayKey != dayKey)
        {
            data.DayKey = dayKey;
            data.DailyCorrect = 0;
        }
    }

    private static void EnsureWeek(UserProgressData data)
    {
        var weekKey = GetWeekKey();
        if (data.WeekKey != weekKey)
        {
            data.WeekKey = weekKey;
            data.WeeklyCorrect = 0;
        }
    }

    public static string GetWeekKey()
    {
        var now = DateTime.UtcNow;
        var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        var week = cal.GetWeekOfYear(now, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Sunday);
        return $"{now.Year}-W{week:D2}";
    }

    public static string TodayKey() => DateTime.UtcNow.ToString("yyyy-MM-dd");
}
