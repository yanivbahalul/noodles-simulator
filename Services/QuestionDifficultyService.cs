using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NoodlesSimulator.Models;

#nullable enable

namespace NoodlesSimulator.Services;

public class QuestionDifficultyService
{
        private readonly HttpClient _client;
        private readonly string _url;

        public QuestionDifficultyService(IConfiguration config)
        {
            var rest = SupabaseRestClient.Create(config, required: true);
            _url = rest.Url;
            _client = rest.Client!;
        }

        public async Task<List<string>> GetQuestionsByDifficultyAsync(string difficulty)
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
                var items = JsonSerializer.Deserialize<List<QuestionDifficulty>>(json, AppJson.Options);
                
                return items?.Select(q => q.QuestionFile).ToList() ?? new List<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QuestionDifficultyService] Exception: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<bool> UpdateQuestionStatsAsync(string questionFile, bool isCorrect)
        {
            try
            {
                // Get or create the record
                var existing = await GetQuestionDifficultyAsync(questionFile);
                
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
                    
                    return await CreateQuestionDifficultyAsync(newRecord);
                }

                existing.TotalAttempts++;
                if (isCorrect) existing.CorrectAttempts++;

                existing.SuccessRate = existing.TotalAttempts > 0
                    ? Math.Round((decimal)existing.CorrectAttempts / existing.TotalAttempts * 100, 2)
                    : 0;

                return await UpdateQuestionDifficultyAsync(existing);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QuestionDifficultyService] Error updating stats: {ex.Message}");
                return false;
            }
        }

        public async Task<QuestionDifficulty?> GetQuestionDifficultyAsync(string questionFile)
        {
            try
            {
                var res = await _client.GetAsync(
                    $"{_url}/rest/v1/question_difficulties?QuestionFile=eq.{Uri.EscapeDataString(questionFile)}&select=*"
                );
                
                if (!res.IsSuccessStatusCode)
                    return null;

                var json = await res.Content.ReadAsStringAsync();
                var items = JsonSerializer.Deserialize<List<QuestionDifficulty>>(json, AppJson.Options);
                
                return items?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QuestionDifficultyService] Exception getting question: {ex.Message}");
                return null;
            }
        }

        private async Task<bool> CreateQuestionDifficultyAsync(QuestionDifficulty record)
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

        private async Task<bool> UpdateQuestionDifficultyAsync(QuestionDifficulty record)
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
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QuestionDifficultyService] Error updating: {ex.Message}");
                return false;
            }
        }

        public async Task<int> RecalculateAllDifficultiesAsync()
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

        public async Task<List<QuestionDifficulty>> GetAllQuestionsAsync(int limit = 1000)
        {
            try
            {
                var res = await _client.GetAsync(
                    $"{_url}/rest/v1/question_difficulties?select=QuestionFile,Difficulty,SuccessRate,TotalAttempts,CorrectAttempts,LastUpdated,ManualOverride&order=LastUpdated.desc&limit={limit}"
                );
                
                if (!res.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[QuestionDifficultyService] Error getting all: {res.StatusCode}");
                    return new List<QuestionDifficulty>();
                }

                var json = await res.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<QuestionDifficulty>>(json, AppJson.Options)
                    ?? new List<QuestionDifficulty>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QuestionDifficultyService] Exception: {ex.Message}");
                return new List<QuestionDifficulty>();
            }
        }
    }

