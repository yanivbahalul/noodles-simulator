using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NoodlesSimulator.Services
{
    public class QuestionStatsService
    {
        private readonly string _statsPath;
        private readonly object _lock = new object();
        private Dictionary<string, Stat> _cache = new Dictionary<string, Stat>(StringComparer.OrdinalIgnoreCase);
        private DateTime _lastLoad = DateTime.MinValue;
        private readonly TimeSpan _ttl = TimeSpan.FromSeconds(10);

        public class Stat
        {
            public int Attempts { get; set; }
            public int Correct { get; set; }
            public DateTime LastAnsweredUtc { get; set; }
        }

        public class StatView
        {
            public string QuestionId { get; set; }
            public int Attempts { get; set; }
            public int Correct { get; set; }
            public double SuccessRate { get; set; }
        }

        public QuestionStatsService(string statsPath)
        {
            _statsPath = statsPath;
            EnsureFile();
        }

        private void EnsureFile()
        {
            var dir = Path.GetDirectoryName(_statsPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (!File.Exists(_statsPath)) File.WriteAllText(_statsPath, "{}", Encoding.UTF8);
        }

        private void LoadIfStale()
        {
            lock (_lock)
            {
                if ((DateTime.UtcNow - _lastLoad) < _ttl && _cache.Count > 0) return;
                try
                {
                    var json = File.ReadAllText(_statsPath, Encoding.UTF8);
                    _cache = string.IsNullOrWhiteSpace(json)
                        ? new Dictionary<string, Stat>(StringComparer.OrdinalIgnoreCase)
                        : JsonConvert.DeserializeObject<Dictionary<string, Stat>>(json)
                          ?? new Dictionary<string, Stat>(StringComparer.OrdinalIgnoreCase);
                }
                catch
                {
                    _cache = new Dictionary<string, Stat>(StringComparer.OrdinalIgnoreCase);
                }
                _lastLoad = DateTime.UtcNow;
            }
        }

        private void Persist()
        {
            lock (_lock)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(_cache);
                    var tmp = _statsPath + ".tmp";
                    File.WriteAllText(tmp, json, Encoding.UTF8);
                    File.Copy(tmp, _statsPath, overwrite: true);
                    File.Delete(tmp);
                }
                catch { }
            }
        }

        public void Record(string questionId, bool isCorrect)
        {
            if (string.IsNullOrWhiteSpace(questionId)) return;
            LoadIfStale();
            lock (_lock)
            {
                if (!_cache.TryGetValue(questionId, out var stat))
                {
                    stat = new Stat();
                    _cache[questionId] = stat;
                }
                stat.Attempts++;
                if (isCorrect) stat.Correct++;
                stat.LastAnsweredUtc = DateTime.UtcNow;
            }
            // Write-through (simple)
            Persist();
        }

        public double GetSuccessRate(string questionId)
        {
            LoadIfStale();
            lock (_lock)
            {
                if (!_cache.TryGetValue(questionId, out var stat) || stat.Attempts == 0) return 0.0;
                return (double)stat.Correct / stat.Attempts;
            }
        }

        public List<StatView> GetAll()
        {
            LoadIfStale();
            lock (_lock)
            {
                return _cache.Select(kv => new StatView
                {
                    QuestionId = kv.Key,
                    Attempts = kv.Value.Attempts,
                    Correct = kv.Value.Correct,
                    SuccessRate = kv.Value.Attempts > 0 ? (double)kv.Value.Correct / kv.Value.Attempts : 0.0
                })
                .OrderBy(v => v.SuccessRate)
                .ThenByDescending(v => v.Attempts)
                .ToList();
            }
        }
    }
}


