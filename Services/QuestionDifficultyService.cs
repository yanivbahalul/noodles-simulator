using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

#nullable enable

namespace NoodlesSimulator.Services
{
    public class QuestionDifficulty
    {
        public string QuestionFile { get; set; } = string.Empty;
        public string Difficulty { get; set; } = "medium";
        public decimal SuccessRate { get; set; } = 0;
        public int TotalAttempts { get; set; } = 0;
        public int CorrectAttempts { get; set; } = 0;
        public DateTime LastUpdated { get; set; }
        public bool ManualOverride { get; set; } = false;
        public DateTime CreatedAt { get; set; }
    }

    public class QuestionDifficultyService
    {
        private readonly HttpClient _client;
        private readonly string _url;
        private readonly string _apiKey;
        
        // Cache for performance
        private Dictionary<string, string>? _difficultyCache;
        private DateTime _cacheExpiry = DateTime.MinValue;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);

        public QuestionDifficultyService(IConfiguration config)
        {
            _url = config["SUPABASE_URL"]!;
            _apiKey = config["SUPABASE_KEY"]!;

            if (string.IsNullOrWhiteSpace(_url) || string.IsNullOrWhiteSpace(_apiKey))
                throw new Exception("Missing Supabase ENV vars for QuestionDifficultyService.");

            _client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            _client.DefaultRequestHeaders.Add("apikey", _apiKey);
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<List<string>> GetQuestionsByDifficulty(string difficulty)
        {
            try
            {
                var res = await _client.GetAsync(
                    $"{_url}/rest/v1/question_difficulties?Difficulty=eq.{difficulty}&select=QuestionFile"
                );
                
                if (!res.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[QuestionDifficultyService] Error getting questions: {res.StatusCode}");
                    return new List<string>();
                }

                var json = await res.Content.ReadAsStringAsync();
                var items = JsonSerializer.Deserialize<List<QuestionDifficulty>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return items?.Select(q => q.QuestionFile).ToList() ?? new List<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QuestionDifficultyService] Exception: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<Dictionary<string, string>> GetAllDifficultiesMap()
        {
            // Check cache first
            if (_difficultyCache != null && DateTime.UtcNow < _cacheExpiry)
            {
                Console.WriteLine($"[QuestionDifficultyService] Using cached difficulties ({_difficultyCache.Count} entries)");
                return _difficultyCache;
            }

            try
            {
                var res = await _client.GetAsync(
                    $"{_url}/rest/v1/question_difficulties?select=QuestionFile,Difficulty"
                );
                
                if (!res.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[QuestionDifficultyService] Error getting all difficulties: {res.StatusCode}");
                    return new Dictionary<string, string>();
                }

                var json = await res.Content.ReadAsStringAsync();
                var items = JsonSerializer.Deserialize<List<QuestionDifficulty>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                _difficultyCache = items?.ToDictionary(q => q.QuestionFile, q => q.Difficulty) 
                                   ?? new Dictionary<string, string>();
                _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);
                
                Console.WriteLine($"[QuestionDifficultyService] Loaded {_difficultyCache.Count} difficulties into cache");
                return _difficultyCache;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QuestionDifficultyService] Exception: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }

        public async Task<bool> UpdateQuestionStats(string questionFile, bool isCorrect)
        {
            try
            {
                // Get or create the record
                var existing = await GetQuestionDifficulty(questionFile);
                
                if (existing == null)
                {
                    // Create new record
                    var newRecord = new QuestionDifficulty
                    {
                        QuestionFile = questionFile,
                        TotalAttempts = 1,
                        CorrectAttempts = isCorrect ? 1 : 0,
                        SuccessRate = isCorrect ? 100 : 0,
                        Difficulty = "medium",
                        ManualOverride = false,
                        CreatedAt = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };
                    
                    return await CreateQuestionDifficulty(newRecord);
                }
                else
                {
                    // Update existing record
                    existing.TotalAttempts++;
                    if (isCorrect) existing.CorrectAttempts++;
                    
                    existing.SuccessRate = existing.TotalAttempts > 0 
                        ? Math.Round((decimal)existing.CorrectAttempts / existing.TotalAttempts * 100, 2)
                        : 0;
                    
                    return await UpdateQuestionDifficulty(existing);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QuestionDifficultyService] Error updating stats: {ex.Message}");
                return false;
            }
        }

        public async Task<QuestionDifficulty?> GetQuestionDifficulty(string questionFile)
        {
            try
            {
                var res = await _client.GetAsync(
                    $"{_url}/rest/v1/question_difficulties?QuestionFile=eq.{Uri.EscapeDataString(questionFile)}&select=*"
                );
                
                if (!res.IsSuccessStatusCode)
                    return null;

                var json = await res.Content.ReadAsStringAsync();
                var items = JsonSerializer.Deserialize<List<QuestionDifficulty>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return items?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QuestionDifficultyService] Exception getting question: {ex.Message}");
                return null;
            }
        }

        private async Task<bool> CreateQuestionDifficulty(QuestionDifficulty record)
        {
            try
            {
                var payload = new[]
                {
                    new
                    {
                        QuestionFile = record.QuestionFile,
                        Difficulty = record.Difficulty,
                        SuccessRate = record.SuccessRate,
                        TotalAttempts = record.TotalAttempts,
                        CorrectAttempts = record.CorrectAttempts,
                        ManualOverride = record.ManualOverride,
                        LastUpdated = record.LastUpdated.ToString("o"),
                        CreatedAt = record.CreatedAt.ToString("o")
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var res = await _client.PostAsync($"{_url}/rest/v1/question_difficulties", content);
                
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QuestionDifficultyService] Error creating: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> UpdateQuestionDifficulty(QuestionDifficulty record)
        {
            try
            {
                var patch = new
                {
                    TotalAttempts = record.TotalAttempts,
                    CorrectAttempts = record.CorrectAttempts,
                    SuccessRate = record.SuccessRate,
                    LastUpdated = DateTime.UtcNow.ToString("o")
                };

                var content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), 
                    $"{_url}/rest/v1/question_difficulties?QuestionFile=eq.{Uri.EscapeDataString(record.QuestionFile)}")
                {
                    Content = content
                };
                request.Headers.Add("Prefer", "return=minimal");

                var response = await _client.SendAsync(request);
                
                // Invalidate cache when data changes
                if (response.IsSuccessStatusCode)
                {
                    _difficultyCache = null;
                }
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QuestionDifficultyService] Error updating: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetManualDifficulty(string questionFile, string difficulty)
        {
            try
            {
                var patch = new
                {
                    Difficulty = difficulty,
                    ManualOverride = true,
                    LastUpdated = DateTime.UtcNow.ToString("o")
                };

                var content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), 
                    $"{_url}/rest/v1/question_difficulties?QuestionFile=eq.{Uri.EscapeDataString(questionFile)}")
                {
                    Content = content
                };
                request.Headers.Add("Prefer", "return=minimal");

                var response = await _client.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    _difficultyCache = null; // Invalidate cache
                    Console.WriteLine($"[QuestionDifficultyService] Manually set '{questionFile}' to '{difficulty}'");
                }
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QuestionDifficultyService] Error setting manual difficulty: {ex.Message}");
                return false;
            }
        }

        public async Task<int> RecalculateAllDifficulties()
        {
            try
            {
                // Call the stored procedure
                var res = await _client.PostAsync(
                    $"{_url}/rest/v1/rpc/recalculate_all_difficulties", 
                    new StringContent("{}", Encoding.UTF8, "application/json")
                );
                
                if (res.IsSuccessStatusCode)
                {
                    _difficultyCache = null; // Invalidate cache
                    var result = await res.Content.ReadAsStringAsync();
                    return int.TryParse(result, out var count) ? count : 0;
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QuestionDifficultyService] Error recalculating: {ex.Message}");
                return 0;
            }
        }

        public async Task<List<QuestionDifficulty>> GetAllQuestions(int limit = 1000)
        {
            try
            {
                var res = await _client.GetAsync(
                    $"{_url}/rest/v1/question_difficulties?select=*&order=LastUpdated.desc&limit={limit}"
                );
                
                if (!res.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[QuestionDifficultyService] Error getting all: {res.StatusCode}");
                    return new List<QuestionDifficulty>();
                }

                var json = await res.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<QuestionDifficulty>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<QuestionDifficulty>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QuestionDifficultyService] Exception: {ex.Message}");
                return new List<QuestionDifficulty>();
            }
        }

        public void ClearCache()
        {
            _difficultyCache = null;
            Console.WriteLine($"[QuestionDifficultyService] Cache cleared");
        }
    }
}

