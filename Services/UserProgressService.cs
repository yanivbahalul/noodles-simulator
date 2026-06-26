using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class UserProgressService
{
    private readonly string _progressDir;
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
        public int DailyChallengeScore { get; set; }
        public string DailyChallengeDate { get; set; } = "";
        public int ExamsCompleted { get; set; }
        public int BestExamScore { get; set; }
        public int BestExamCorrect { get; set; }
    }

    public UserProgressService(string progressDir)
    {
        _progressDir = progressDir;
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
            if (!File.Exists(path))
                return new UserProgressData { WeekKey = GetWeekKey() };

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var data = JsonSerializer.Deserialize<UserProgressData>(json, AppJson.Options) ?? new UserProgressData();
                EnsureWeek(data);
                return data;
            }
            catch
            {
                return new UserProgressData { WeekKey = GetWeekKey() };
            }
        }
    }

    private void Save(string username, UserProgressData data)
    {
        lock (_lock)
        {
            var path = PathFor(username);
            var tmp = path + ".tmp";
            var json = JsonSerializer.Serialize(data, AppJson.Options);
            File.WriteAllText(tmp, json, Encoding.UTF8);
            File.Copy(tmp, path, overwrite: true);
            File.Delete(tmp);
        }
    }

    public void RecordAnswer(string username, string questionId, bool isCorrect, int xpGained)
    {
        if (string.IsNullOrWhiteSpace(username)) return;
        var data = Load(username);
        EnsureWeek(data);

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
            data.Xp += Math.Max(0, xpGained);
        }

        Save(username, data);
    }

    public void RecordExamComplete(string username, int correctCount, int totalQuestions, int score)
    {
        var data = Load(username);
        data.ExamsCompleted++;
        if (correctCount > data.BestExamCorrect)
        {
            data.BestExamCorrect = correctCount;
            data.BestExamScore = score;
        }
        data.Xp += score;
        Save(username, data);
    }

    public void RecordDailyChallengeAnswer(string username, bool isCorrect)
    {
        var data = Load(username);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (data.DailyChallengeDate != today)
        {
            data.DailyChallengeDate = today;
            data.DailyChallengeScore = 0;
        }
        if (isCorrect) data.DailyChallengeScore++;
        Save(username, data);
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
                if (data?.DailyChallengeDate == date && data.DailyChallengeScore > 0)
                {
                    var username = System.IO.Path.GetFileNameWithoutExtension(file);
                    results.Add((username, data.DailyChallengeScore));
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

    public List<(string Username, int BestExamScore, int BestExamCorrect)> GetExamLeaderboard(int limit = 50)
    {
        var results = new List<(string, int, int)>();
        if (!Directory.Exists(_progressDir)) return results;

        foreach (var file in Directory.GetFiles(_progressDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var data = JsonSerializer.Deserialize<UserProgressData>(json, AppJson.Options);
                if (data != null && data.BestExamScore > 0)
                {
                    var username = System.IO.Path.GetFileNameWithoutExtension(file);
                    results.Add((username, data.BestExamScore, data.BestExamCorrect));
                }
            }
            catch { /* skip */ }
        }

        return results
            .OrderByDescending(r => r.Item2)
            .ThenByDescending(r => r.Item3)
            .Take(limit)
            .ToList();
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
