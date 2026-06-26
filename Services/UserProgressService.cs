using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class UserProgressService
{
    private readonly string _progressDir;
    private readonly AuthService _auth;
    private readonly UserProgressStore _store;
    private readonly object _lock = new();

    public class UserQuestionStat
    {
        public int Attempts { get; set; }
        public int Correct { get; set; }
        public DateTime LastAnsweredUtc { get; set; }
        public bool LastWasCorrect { get; set; }
    }

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

    public UserProgressService(string progressDir, AuthService auth = null, UserProgressStore store = null)
    {
        _progressDir = progressDir;
        _auth = auth;
        _store = store;
        Directory.CreateDirectory(_progressDir);
    }

    private string PathFor(string username) =>
        System.IO.Path.Combine(_progressDir, $"{Sanitize(username)}.json");

    private static string Sanitize(string username) =>
        string.Join("_", username.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

    public UserProgressData Load(string username)
    {
        lock (_lock)
        {
            var path = PathFor(username);
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path, Encoding.UTF8);
                    var localData = JsonSerializer.Deserialize<UserProgressData>(json, AppJson.Options);
                    if (localData != null)
                    {
                        EnsureWeek(localData);
                        EnsureDay(localData);
                        return localData;
                    }
                }
                catch
                {
                    /* fall through */
                }
            }

            UserProgressData data = null;
            if (_store?.IsEnabled == true)
            {
                var (fromDb, _) = _store.TryLoadWithMeta(username);
                if (fromDb != null)
                {
                    var dbAchievementKeys = _store.TryLoadAchievementKeys(username);
                    UserProgressStore.MergeAchievementKeys(fromDb, dbAchievementKeys);
                    data = CloneProgress(fromDb);
                }
            }

            data ??= new UserProgressData { WeekKey = GetWeekKey() };
            EnsureWeek(data);
            EnsureDay(data);
            WriteFile(username, data);
            return data;
        }
    }

    private static UserProgressData CloneProgress(UserProgressData source)
    {
        if (source == null) return null;
        var json = JsonSerializer.Serialize(source, AppJson.Options);
        return JsonSerializer.Deserialize<UserProgressData>(json, AppJson.Options);
    }

    private void WriteFile(string username, UserProgressData data)
    {
        var path = PathFor(username);
        var tmp = path + ".tmp";
        var json = JsonSerializer.Serialize(data, AppJson.Options);
        File.WriteAllText(tmp, json, Encoding.UTF8);
        File.Copy(tmp, path, overwrite: true);
        File.Delete(tmp);
    }

    private void Save(string username, UserProgressData data)
    {
        lock (_lock)
        {
            WriteFile(username, data);
        }
        _ = Task.Run(() =>
        {
            try { _store?.Save(username, data); }
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
        if (newly.Count > 0) Save(username, data);
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

    public int GetSpacedPriority(string username, string questionId)
    {
        var data = Load(username);
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
        var results = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(_progressDir)) return results;

        foreach (var file in Directory.GetFiles(_progressDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var data = JsonSerializer.Deserialize<UserProgressData>(json, AppJson.Options);
                if (data == null || data.Xp <= 0) continue;
                var username = System.IO.Path.GetFileNameWithoutExtension(file);
                if (!results.ContainsKey(username) || data.Xp > results[username])
                    results[username] = data.Xp;
            }
            catch { /* skip */ }
        }

        return results;
    }

    public List<(string Username, int Score)> GetDailyLeaderboard(string date, int limit = 50)
    {
        var results = new List<(string, int)>();
        if (!Directory.Exists(_progressDir)) return results;

        foreach (var file in Directory.GetFiles(_progressDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var data = JsonSerializer.Deserialize<UserProgressData>(json, AppJson.Options);
                if (data?.DayKey == date && data.DailyCorrect > 0)
                {
                    var username = System.IO.Path.GetFileNameWithoutExtension(file);
                    results.Add((username, data.DailyCorrect));
                }
            }
            catch { /* skip */ }
        }

        return results.OrderByDescending(r => r.Item2).Take(limit).ToList();
    }

    public List<(string Username, int WeeklyCorrect)> GetWeeklyLeaderboard(int limit = 50)
    {
        var weekKey = GetWeekKey();
        var results = new List<(string, int)>();
        if (!Directory.Exists(_progressDir)) return results;

        foreach (var file in Directory.GetFiles(_progressDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var data = JsonSerializer.Deserialize<UserProgressData>(json, AppJson.Options);
                if (data != null && data.WeekKey == weekKey && data.WeeklyCorrect > 0)
                {
                    var username = System.IO.Path.GetFileNameWithoutExtension(file);
                    results.Add((username, data.WeeklyCorrect));
                }
            }
            catch { /* skip */ }
        }

        return results.OrderByDescending(r => r.Item2).Take(limit).ToList();
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
                var data = JsonSerializer.Deserialize<UserProgressData>(json, AppJson.Options);
                if (data != null && data.ExamsCompleted > 0)
                {
                    var username = System.IO.Path.GetFileNameWithoutExtension(file);
                    results.Add((username, data.ExamsCompleted));
                }
            }
            catch { /* skip */ }
        }

        return results.OrderByDescending(r => r.Item2).Take(limit).ToList();
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
