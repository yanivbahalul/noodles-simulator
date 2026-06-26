using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NoodlesSimulator.Models;

#nullable enable

namespace NoodlesSimulator.Services;

public class TestSessionService
{
        private readonly HttpClient _client;
        private readonly string _url;
        private readonly string _apiKey;
        private static readonly TimeSpan TestDuration = TimeSpan.FromHours(2);

        public TestSessionService(IConfiguration config)
        {
            _url = SupabaseConfiguration.Url(config) ?? string.Empty;
            _apiKey = SupabaseConfiguration.ServiceRoleApiKey(config)
                      ?? SupabaseConfiguration.AnonApiKey(config)
                      ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_url) || string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("Missing Supabase ENV vars.");

            _client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            _client.DefaultRequestHeaders.Add("apikey", _apiKey);
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public string GenerateToken()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }

        public async Task<TestSession?> CreateSessionAsync(string username, string questionsJson)
        {
            try
            {
                Console.WriteLine($"[TestSessionService] CreateSessionAsync called for user: {username}");
                Console.WriteLine($"[TestSessionService] Questions JSON length: {questionsJson?.Length ?? 0}");
                
                var token = GenerateToken();
                
                var now = DateTime.UtcNow;
                
                var session = new TestSession
                {
                    Token = token,
                    Username = username,
                    StartedUtc = now,
                    Status = "active",
                    QuestionsJson = questionsJson,
                    AnswersJson = "[]",
                    CurrentIndex = 0,
                    Score = 0,
                    MaxScore = 0,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                var payload = new[]
                {
                    new
                    {
                        Token = session.Token,
                        Username = session.Username,
                        StartedUtc = session.StartedUtc.ToString("o"),
                        Status = session.Status,
                        QuestionsJson = session.QuestionsJson,
                        AnswersJson = session.AnswersJson,
                        CurrentIndex = session.CurrentIndex,
                        Score = session.Score,
                        MaxScore = session.MaxScore,
                        CreatedAt = session.CreatedAt.ToString("o"),
                        UpdatedAt = session.UpdatedAt.ToString("o")
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                Console.WriteLine($"[TestSessionService] Posting to: {_url}/rest/v1/test_sessions");
                
                var res = await _client.PostAsync($"{_url}/rest/v1/test_sessions", content);
                
                Console.WriteLine($"[TestSessionService] Response status: {res.StatusCode}");
                
                if (!res.IsSuccessStatusCode)
                {
                    var error = await res.Content.ReadAsStringAsync();
                    Console.WriteLine($"[TestSessionService] CreateSessionAsync Error - Status: {res.StatusCode}");
                    Console.WriteLine($"[TestSessionService] CreateSessionAsync Error body length: {error?.Length ?? 0}");
                    return null;
                }

                Console.WriteLine("[TestSessionService] Session created in database successfully!");
                return session;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TestSessionService] CreateSessionAsync Exception: {ex.Message}");
                Console.WriteLine($"[TestSessionService] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        public async Task<TestSession?> GetSessionAsync(string token)
        {
            try
            {
                var res = await _client.GetAsync($"{_url}/rest/v1/test_sessions?Token=eq.{Uri.EscapeDataString(token)}&select=*");
                if (!res.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[GetSessionAsync Error] {res.StatusCode}");
                    return null;
                }

                var json = await res.Content.ReadAsStringAsync();
                var sessions = JsonSerializer.Deserialize<List<TestSession>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return sessions?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetSessionAsync Exception] {ex}");
                return null;
            }
        }

        public async Task<TestSession?> GetActiveSessionAsync(string username)
        {
            try
            {
                var res = await _client.GetAsync(
                    $"{_url}/rest/v1/test_sessions?Username=eq.{Uri.EscapeDataString(username)}&Status=eq.active&select=*&order=StartedUtc.desc&limit=1"
                );
                
                if (!res.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[GetActiveSessionAsync Error] {res.StatusCode}");
                    return null;
                }

                var json = await res.Content.ReadAsStringAsync();
                var sessions = JsonSerializer.Deserialize<List<TestSession>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                var session = sessions?.FirstOrDefault();
                
                // Check if expired
                if (session != null && IsExpired(session))
                {
                    await UpdateSessionStatusAsync(session.Token, "expired");
                    return null;
                }
                
                return session;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetActiveSessionAsync Exception] {ex}");
                return null;
            }
        }

        public async Task<bool> UpdateSessionAsync(TestSession session)
        {
            try
            {
                session.UpdatedAt = DateTime.UtcNow;
                
                Console.WriteLine($"[TestSessionService UpdateSessionAsync] Token: {session.Token}, Status: {session.Status}");
                Console.WriteLine($"[TestSessionService UpdateSessionAsync] Score: {session.Score}/{session.MaxScore}");
                
                var patch = new
                {
                    AnswersJson = session.AnswersJson,
                    CurrentIndex = session.CurrentIndex,
                    Score = session.Score,
                    MaxScore = session.MaxScore,
                    Status = session.Status,
                    CompletedUtc = session.CompletedUtc.HasValue ? session.CompletedUtc.Value.ToString("o") : null,
                    UpdatedAt = session.UpdatedAt.ToString("o")
                };

                var content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), 
                    $"{_url}/rest/v1/test_sessions?Token=eq.{Uri.EscapeDataString(session.Token)}")
                {
                    Content = content
                };
                request.Headers.Add("Prefer", "return=minimal");

                Console.WriteLine("[TestSessionService UpdateSessionAsync] Sending PATCH to Supabase...");
                var response = await _client.SendAsync(request);
                
                Console.WriteLine($"[TestSessionService UpdateSessionAsync] Response: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[TestSessionService UpdateSessionAsync] Error - Status: {response.StatusCode}");
                    Console.WriteLine($"[TestSessionService UpdateSessionAsync] Error body length: {error?.Length ?? 0}");
                    return false;
                }
                
                Console.WriteLine("[TestSessionService UpdateSessionAsync] Session updated successfully!");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TestSessionService UpdateSessionAsync] Exception: {ex.Message}");
                Console.WriteLine($"[TestSessionService UpdateSessionAsync] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<bool> UpdateSessionStatusAsync(string token, string status)
        {
            try
            {
                var patch = new
                {
                    Status = status,
                    UpdatedAt = DateTime.UtcNow.ToString("o"),
                    CompletedUtc = (status == "completed" || status == "expired") ? DateTime.UtcNow.ToString("o") : null
                };

                var content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), 
                    $"{_url}/rest/v1/test_sessions?Token=eq.{Uri.EscapeDataString(token)}")
                {
                    Content = content
                };
                request.Headers.Add("Prefer", "return=minimal");

                var response = await _client.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UpdateSessionStatusAsync Exception] {ex}");
                return false;
            }
        }

        public async Task<List<TestSession>> GetUserSessionsAsync(string username, int limit = 50)
        {
            try
            {
                var res = await _client.GetAsync(
                    $"{_url}/rest/v1/test_sessions?Username=eq.{Uri.EscapeDataString(username)}&select=*&order=StartedUtc.desc&limit={limit}"
                );
                
                if (!res.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[GetUserSessionsAsync Error] {res.StatusCode}");
                    return new List<TestSession>();
                }

                var json = await res.Content.ReadAsStringAsync();
                var sessions = JsonSerializer.Deserialize<List<TestSession>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return sessions ?? new List<TestSession>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetUserSessionsAsync Exception] {ex}");
                return new List<TestSession>();
            }
        }

        public bool IsExpired(TestSession session)
        {
            if (session == null) return true;
            var end = session.StartedUtc.Add(TestDuration);
            return DateTime.UtcNow >= end;
        }

        public TimeSpan GetRemainingTime(TestSession session)
        {
            if (session == null) return TimeSpan.Zero;
            var end = session.StartedUtc.Add(TestDuration);
            var remaining = end - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        public async Task<List<(string Username, int ExamCount)>> GetExamCountLeaderboardAsync(int limit = 50)
        {
            try
            {
                var res = await _client.GetAsync(
                    $"{_url}/rest/v1/test_sessions?Status=eq.completed&select=Username&limit=5000");
                if (!res.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[GetExamCountLeaderboardAsync] Error: {res.StatusCode}");
                    return new List<(string, int)>();
                }

                var json = await res.Content.ReadAsStringAsync();
                var sessions = JsonSerializer.Deserialize<List<TestSession>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<TestSession>();

                var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var session in sessions)
                {
                    if (string.IsNullOrWhiteSpace(session.Username)) continue;
                    if (!counts.ContainsKey(session.Username))
                        counts[session.Username] = 0;
                    counts[session.Username]++;
                }

                return counts
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                    .Take(limit)
                    .Select(kv => (kv.Key, kv.Value))
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetExamCountLeaderboardAsync Exception] {ex}");
                return new List<(string, int)>();
            }
        }
    }

