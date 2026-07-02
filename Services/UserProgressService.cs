using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class UserProgressService
{
    private static readonly AsyncLocal<Dictionary<string, UserProgressData>> RequestCache = new();
    private static readonly TimeSpan MemCacheTtl = TimeSpan.FromMinutes(3);

    private readonly string _progressDir;
    private readonly AuthService _auth;
    private readonly UserProgressStore _store;
    private readonly UserStatsService _stats;
    private readonly UserQuestionStatsStore _questionStats;
    private readonly IMemoryCache _memCache;
    private readonly object _lock = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> MutationLocks = new();

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
        UserQuestionStatsStore questionStats = null,
        IMemoryCache memCache = null)
    {
        _progressDir = progressDir;
        _auth = auth;
        _store = store;
        _stats = stats;
        _questionStats = questionStats;
        _memCache = memCache;
        Directory.CreateDirectory(_progressDir);
    }

    public void ClearRequestCache() => RequestCache.Value = null;

    public void DeleteLocal(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return;

        var cache = RequestCache.Value;
        cache?.Remove(username);
        _memCache?.Remove(MemKey(username));

        try
        {
            var path = PathFor(username);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserProgressService] DeleteLocal failed for {username}: {ex.Message}");
        }
    }

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

    public bool TryGetCached(string username, out UserProgressData data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(username))
            return false;

        if (GetRequestCache().TryGetValue(username, out var cached))
        {
            EnsureWeek(cached);
            EnsureDay(cached);
            data = cached;
            return true;
        }

        if (TryLoadMemCache(username, out var mem))
        {
            GetRequestCache()[username] = mem;
            data = mem;
            return true;
        }

        return false;
    }

    public bool TryGetProgressTotals(string username, out int correct, out int total, out int xp)
    {
        correct = total = xp = 0;
        if (!TryGetCached(username, out var data) || data == null)
            return false;

        (total, correct) = SumQuestionStats(data);
        xp = data.Xp;
        return true;
    }

    private static string MemKey(string username) => "progress:" + username;

    private void StoreMemCache(string username, UserProgressData data)
    {
        if (_memCache == null || string.IsNullOrWhiteSpace(username) || data == null) return;
        try
        {
            var json = JsonSerializer.Serialize(data, AppJson.Options);
            _memCache.Set(MemKey(username), json, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = MemCacheTtl,
                Size = json.Length
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserProgressService] MemCache store failed for {username}: {ex.Message}");
        }
    }

    private bool TryLoadMemCache(string username, out UserProgressData data)
    {
        data = null;
        if (_memCache == null || string.IsNullOrWhiteSpace(username))
            return false;

        if (!_memCache.TryGetValue(MemKey(username), out string json) || string.IsNullOrEmpty(json))
            return false;

        try
        {
            data = JsonSerializer.Deserialize<UserProgressData>(json, AppJson.Options);
            if (data == null) return false;
            EnsureWeek(data);
            EnsureDay(data);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<UserProgressData> LoadAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return new UserProgressData { WeekKey = GetWeekKey(), DayKey = TodayKey() };

        if (TryGetCached(username, out var cached))
            return cached;

        var data = await LoadFromStorageAsync(username);
        EnsureWeek(data);
        EnsureDay(data);
        GetRequestCache()[username] = data;
        StoreMemCache(username, data);
        return data;
    }

    private async Task<UserProgressData> LoadFromStorageAsync(string username)
    {
        var storeEnabled = _store?.IsEnabled == true;
        string? localJson = null;
        ProgressDataDocument doc = null;
        UserProgressData data;

        if (storeEnabled)
        {
            var docTask = Task.Run(() => _store.TryLoadDocumentWithMeta(username));
            var statsTask = _stats?.IsEnabled == true
                ? _stats.GetAsync(username)
                : Task.FromResult<UserStatsRow?>(null);
            var qStatsTask = _questionStats?.IsEnabled == true
                ? _questionStats.LoadForUserAsync(username)
                : Task.FromResult(new Dictionary<string, UserQuestionStat>());
            var achievementsTask = Task.Run(() => _store.TryLoadAchievementKeys(username) ?? new List<string>());

            await Task.WhenAll(docTask, statsTask, qStatsTask, achievementsTask);

            var (fromDb, _) = await docTask;
            doc = fromDb;
            data = doc != null
                ? FromDocument(doc)
                : new UserProgressData { WeekKey = GetWeekKey(), DayKey = TodayKey() };

            var statsRow = await statsTask;
            if (statsRow != null)
                UserStatsService.ApplyToProgress(statsRow, data);

            var qStats = await qStatsTask;
            if (qStats.Count > 0)
                data.QuestionStats = qStats;

            data.Achievements = await achievementsTask;
            return data;
        }

        localJson = TryReadLocalProgressJson(username);
        if (localJson != null)
            doc = TryDeserializeDocument(localJson);

        data = doc != null
            ? FromDocument(doc)
            : new UserProgressData { WeekKey = GetWeekKey(), DayKey = TodayKey() };

        if (_stats?.IsEnabled == true)
        {
            var statsRow = await _stats.GetAsync(username);
            if (statsRow != null)
                UserStatsService.ApplyToProgress(statsRow, data);
        }

        if (_questionStats?.IsEnabled == true)
        {
            var qStats = await _questionStats.LoadForUserAsync(username);
            if (qStats.Count > 0)
                data.QuestionStats = qStats;
        }
        else if (doc == null && localJson != null)
        {
            TryMergeLegacyQuestionStats(localJson, data);
        }

        if (localJson != null)
            TryMergeLegacyAchievements(localJson, data);

        if (doc == null && localJson == null)
            WriteFile(username, data);

        return data;
    }

    private string? TryReadLocalProgressJson(string username)
    {
        var path = PathFor(username);
        if (!File.Exists(path))
            return null;

        try
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }
        catch
        {
            return null;
        }
    }

    private static void TryMergeLegacyQuestionStats(string json, UserProgressData data)
    {
        try
        {
            var legacy = JsonSerializer.Deserialize<UserProgressData>(json, AppJson.Options);
            if (legacy?.QuestionStats?.Count > 0)
                data.QuestionStats = legacy.QuestionStats;
        }
        catch { /* ignore */ }
    }

    private static void TryMergeLegacyAchievements(string json, UserProgressData data)
    {
        try
        {
            using var legacyDoc = JsonDocument.Parse(json);
            if (!legacyDoc.RootElement.TryGetProperty("Achievements", out var achEl) ||
                achEl.ValueKind != JsonValueKind.Array)
                return;

            foreach (var item in achEl.EnumerateArray())
            {
                var key = item.GetString();
                if (!string.IsNullOrWhiteSpace(key) &&
                    !data.Achievements.Contains(key, StringComparer.OrdinalIgnoreCase))
                    data.Achievements.Add(key);
            }
        }
        catch { /* ignore */ }
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

    private void Save(string username, UserProgressData data) =>
        SaveAsync(username, data).GetAwaiter().GetResult();

    private async Task SaveAsync(string username, UserProgressData data)
    {
        GetRequestCache()[username] = data;
        StoreMemCache(username, data);

        if (_store?.IsEnabled != true)
        {
            lock (_lock)
            {
                WriteFile(username, data);
            }
        }

        try
        {
            if (_stats?.IsEnabled == true)
                await _stats.UpsertAsync(UserStatsService.FromProgress(username, data));

            if (_store?.IsEnabled == true)
                await _store.SaveDocumentAsync(username, ToDocument(data));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserProgressService] Supabase save failed for {username}: {ex.Message}");
            // ponytail: local file fallback when remote upsert fails — upgrade path: retry queue.
            lock (_lock)
            {
                WriteFile(username, data);
            }
        }

        QueueDbSync(username, data);
    }

    private async Task MutateAsync(string username, Action<UserProgressData> mutate)
    {
        if (string.IsNullOrWhiteSpace(username)) return;
        var gate = MutationLocks.GetOrAdd(username, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            var data = await LoadForMutationAsync(username);
            mutate(data);
            await SaveAsync(username, data);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<T> MutateAsync<T>(string username, Func<UserProgressData, T> mutate)
    {
        if (string.IsNullOrWhiteSpace(username))
            return mutate(new UserProgressData());

        var gate = MutationLocks.GetOrAdd(username, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            var data = await LoadForMutationAsync(username);
            var result = mutate(data);
            await SaveAsync(username, data);
            return result;
        }
        finally
        {
            gate.Release();
        }
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

    private async Task<UserProgressData> LoadForMutationAsync(string username)
    {
        var data = await LoadAsync(username);
        EnsureWeek(data);
        EnsureDay(data);
        return data;
    }

    public Task RecordAnswerAsync(string username, string questionId, bool isCorrect, int xpGained, int currentStreak = 0)
    {
        if (string.IsNullOrWhiteSpace(username)) return Task.CompletedTask;

        return MutateAsync(username, data =>
        {
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

            if (currentStreak > data.BestStreak)
                data.BestStreak = currentStreak;
        });
    }

    public async Task ResetAllAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return;

        var fresh = new UserProgressData
        {
            WeekKey = GetWeekKey(),
            DayKey = TodayKey()
        };

        GetRequestCache()[username] = fresh;
        StoreMemCache(username, fresh);

        if (_store?.IsEnabled != true)
        {
            lock (_lock)
            {
                WriteFile(username, fresh);
            }
        }

        try
        {
            _store?.SaveDocument(username, ToDocument(fresh));
            _store?.ClearAchievements(username);
            if (_questionStats?.IsEnabled == true)
                await _questionStats.ClearForUserAsync(username);
            if (_stats?.IsEnabled == true)
                await _stats.UpsertAsync(UserStatsService.FromProgress(username, fresh));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserProgressService] ResetAll Supabase failed for {username}: {ex.Message}");
        }

        QueueDbSync(username, fresh);
    }

    public Task<int> RecordExamCompleteAsync(string username, int correctCount, int totalQuestions, int score) =>
        MutateAsync(username, data =>
        {
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
            return previousExamCorrect;
        });

    public Task RecordDailyChallengeAnswerAsync(string username, bool isCorrect) =>
        MutateAsync(username, data =>
        {
            var today = TodayKey();
            if (data.DailyChallengeDate != today)
            {
                data.DailyChallengeDate = today;
                data.DailyChallengeScore = 0;
            }
            if (isCorrect) data.DailyChallengeScore++;
        });

    public Task RecordDailyChallengeCompleteAsync(string username, bool isPerfect) =>
        MutateAsync(username, data =>
        {
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
        });

    public Task IncrementReviewClearAsync(string username) =>
        MutateAsync(username, data => data.ReviewClearCount++);

    public Task IncrementWeakCorrectAsync(string username) =>
        MutateAsync(username, data => data.WeakModeCorrectCount++);

    public Task IncrementHardCorrectAsync(string username) =>
        MutateAsync(username, data => data.HardCorrectCount++);

    public Task<bool> RemoveSessionMistakeAsync(string username, string questionId)
    {
        if (string.IsNullOrWhiteSpace(questionId)) return Task.FromResult(false);

        return MutateAsync(username, data =>
        {
            var hadMistakes = data.SessionMistakes.Count > 0;
            data.SessionMistakes.RemoveAll(q => string.Equals(q, questionId, StringComparison.OrdinalIgnoreCase));
            return hadMistakes && data.SessionMistakes.Count == 0;
        });
    }

    public async Task<(double Accuracy, int DistinctQuestions)> GetOverallAccuracyStatsAsync(string username) =>
        GetOverallAccuracyStatsFromData(await LoadAsync(username));

    private static (double Accuracy, int DistinctQuestions) GetOverallAccuracyStatsFromData(UserProgressData data)
    {
        var stats = data.QuestionStats.Values.Where(s => s.Attempts > 0).ToList();
        var distinct = stats.Count;
        if (distinct == 0) return (0, 0);
        var totalAttempts = stats.Sum(s => s.Attempts);
        var totalCorrect = stats.Sum(s => s.Correct);
        if (totalAttempts == 0) return (0, distinct);
        return (totalCorrect / (double)totalAttempts, distinct);
    }

    public Task AddSessionMistakesAsync(string username, IEnumerable<string> questionIds) =>
        MutateAsync(username, data =>
        {
            foreach (var q in questionIds)
            {
                if (string.IsNullOrWhiteSpace(q)) continue;
                if (!data.SessionMistakes.Contains(q, StringComparer.OrdinalIgnoreCase))
                    data.SessionMistakes.Add(q);
            }
        });

    public async Task<List<string>> UnlockAchievementsAsync(string username, IEnumerable<string> keys)
    {
        var data = await LoadAsync(username);
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

    public async Task<List<string>> GetWeakQuestionsAsync(string username, double threshold = 0.5)
    {
        var data = await LoadAsync(username);
        return data.QuestionStats
            .Where(kv => kv.Value.Attempts >= 1 && (double)kv.Value.Correct / kv.Value.Attempts < threshold)
            .Select(kv => kv.Key)
            .ToList();
    }

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

    public Task UpdateBestStreakAsync(string username, int streak)
    {
        if (streak <= 0) return Task.CompletedTask;

        return MutateAsync(username, data =>
        {
            if (streak > data.BestStreak)
                data.BestStreak = streak;
        });
    }

    public async Task<Dictionary<string, int>> GetXpByUsernameAsync()
    {
        if (_stats?.IsEnabled == true)
        {
            var all = await _stats.GetAllCachedAsync();
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
                var username = System.IO.Path.GetFileNameWithoutExtension(file);
                var xp = (await LoadAsync(username)).Xp;
                if (!results.ContainsKey(username))
                    results[username] = xp;
            }
            catch { /* skip */ }
        }

        return results;
    }

    public async Task<List<(string Username, int WeeklyCorrect)>> GetWeeklyLeaderboardAsync(int limit = 50)
    {
        var weekKey = GetWeekKey();
        if (_stats?.IsEnabled == true)
        {
            var all = await _stats.GetAllCachedAsync();
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
        if (_store?.IsEnabled == true)
            return new List<(string, int)>();

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

    public async Task<(int TotalAnswered, int CorrectAnswers)> GetAnswerTotalsAsync(string username) =>
        SumQuestionStats(await LoadAsync(username));

    public async Task<QuizStatsSnapshot> GetQuizStatsSnapshotAsync(string username)
    {
        var data = await LoadAsync(username);
        var (total, correct) = SumQuestionStats(data);
        var xp = data?.Xp ?? 0;
        var level = QuizGamification.LevelFromXp(xp);
        return new QuizStatsSnapshot
        {
            CorrectAnswers = correct,
            TotalAnswered = total,
            Xp = xp,
            Level = level,
            XpProgressPercent = QuizGamification.XpProgressPercent(xp),
            XpToNextLevel = QuizGamification.XpToNextLevel(xp)
        };
    }

    public static (int TotalAnswered, int CorrectAnswers) SumQuestionStats(UserProgressData data)
    {
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

    public sealed class QuizStatsSnapshot
    {
        public int CorrectAnswers { get; set; }
        public int TotalAnswered { get; set; }
        public int Xp { get; set; }
        public int Level { get; set; }
        public int XpProgressPercent { get; set; }
        public int XpToNextLevel { get; set; }
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
