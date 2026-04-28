using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

#nullable enable

namespace NoodlesSimulator.Models
{
    public class AuthService
    {
        private readonly HttpClient _client;
        private readonly string _url;
        private readonly string _apiKey;
        private const string PasswordHashPrefix = "pbkdf2$";

        public AuthService(IConfiguration config)
        {
            _url = config["SUPABASE_URL"]!;
            _apiKey =
                config["SUPABASE_KEY"]
                ?? config["SUPABASE_SERVICE_ROLE_KEY"]
                ?? config["SUPABASE_ANON_KEY"]
                ?? throw new Exception("Missing Supabase key ENV vars.");

            if (string.IsNullOrWhiteSpace(_url))
                throw new Exception("Missing Supabase URL ENV var.");

            _client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(6)
            };
            _client.DefaultRequestHeaders.Add("apikey", _apiKey);
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<User?> Authenticate(string username, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    return null;
                }

                var user = await GetUser(username);
                if (user == null || string.IsNullOrWhiteSpace(user.Password))
                {
                    return null;
                }

                var stored = user.Password;
                var isValid = VerifyPassword(stored, password);
                if (!isValid)
                {
                    return null;
                }

                if (!IsHashedPassword(stored))
                {
                    user.Password = HashPassword(password);
                    _ = UpdateUser(user);
                }

                return user;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Authenticate Exception] {ex}");
                return null;
            }
        }

        public async Task<bool> Register(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return false;

            if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
                return false;

            if (username.Length < 5 || password.Length < 5)
                return false;

            if (!Regex.IsMatch(username, "^[a-zA-Z0-9א-ת]+$") || !Regex.IsMatch(password, "^[a-zA-Z0-9א-ת]+$"))
                return false;

            var existingUser = await GetUser(username);
            if (existingUser != null)
                return false;

            var newUser = new[]
            {
                new User
                {
                    Username = username,
                    Password = HashPassword(password),
                    IsAdmin = false,
                    CorrectAnswers = 0,
                    TotalAnswered = 0,
                    IsCheater = false,
                    IsBanned = false,
                    LastSeen = DateTime.UtcNow
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(newUser), Encoding.UTF8, "application/json");
            var res = await _client.PostAsync($"{_url}/rest/v1/users", content);
            return res.IsSuccessStatusCode;
        }

        public async Task<User?> GetUser(string username)
        {
            try
            {
                var safeUsername = Uri.EscapeDataString(username);
                var res = await _client.GetAsync($"{_url}/rest/v1/users?Username=eq.{safeUsername}&select=*");
                var json = await res.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<List<User>>(json);
                return users?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetUser Exception] {ex}");
                return null;
            }
        }

        public async Task<bool> UpdateUser(User updatedUser)
        {
            try
            {
                var patch = new Dictionary<string, object>
                {
                    ["CorrectAnswers"] = updatedUser.CorrectAnswers,
                    ["TotalAnswered"] = updatedUser.TotalAnswered,
                    ["IsCheater"] = updatedUser.IsCheater,
                    ["IsBanned"] = updatedUser.IsBanned,
                    ["LastSeen"] = (updatedUser.LastSeen ?? DateTime.UtcNow).ToString("o")
                };
                if (!string.IsNullOrWhiteSpace(updatedUser.Password))
                {
                    patch["Password"] = updatedUser.Password;
                }

                var content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");
                var safeUsername = Uri.EscapeDataString(updatedUser.Username);
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_url}/rest/v1/users?Username=eq.{safeUsername}")
                {
                    Content = content
                };
                request.Headers.Add("Prefer", "return=representation");

                var response = await _client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[UpdateUser Error] PATCH failed for {updatedUser.Username}: {response.StatusCode} | {errorBody}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UpdateUser Exception] {ex}");
                return false;
            }
        }

        public async Task<int> GetOnlineUserCount()
        {
            try
            {
                var threshold = DateTime.UtcNow.AddMinutes(-5).ToString("o");
                var candidateColumns = new[] { "LastSeen", "last_seen" };

                foreach (var col in candidateColumns)
                {
                    var res = await _client.GetAsync($"{_url}/rest/v1/users?select={Uri.EscapeDataString(col)}&{Uri.EscapeDataString(col)}=gt.{Uri.EscapeDataString(threshold)}");
                    var json = await res.Content.ReadAsStringAsync();
                    if (!res.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[GetOnlineUserCount] query with '{col}' failed: {res.StatusCode} | {json}");
                        continue;
                    }

                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        return doc.RootElement.GetArrayLength();
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetOnlineUserCount Exception] {ex}");
                return 0;
            }
        }

        public async Task<bool> TouchLastSeen(string username, DateTime? at = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            var when = (at ?? DateTime.UtcNow).ToString("o");
            var safeUsername = Uri.EscapeDataString(username);
            var candidateColumns = new[] { "LastSeen", "last_seen" };

            foreach (var col in candidateColumns)
            {
                try
                {
                    var patch = new Dictionary<string, string> { [col] = when };
                    var content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");
                    var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_url}/rest/v1/users?Username=eq.{safeUsername}")
                    {
                        Content = content
                    };
                    request.Headers.Add("Prefer", "return=minimal");

                    var response = await _client.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                        return true;

                    var errorBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[TouchLastSeen] PATCH with '{col}' failed for {username}: {response.StatusCode} | {errorBody}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TouchLastSeen] Exception with '{col}' for {username}: {ex.Message}");
                }
            }

            return false;
        }

        public async Task<List<User>> GetCheaters()
        {
            try
            {
                var res = await _client.GetAsync($"{_url}/rest/v1/users?IsCheater=eq.true&select=*");
                var json = await res.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetCheaters Exception] {ex}");
                return new List<User>();
            }
        }

        public async Task<List<User>> GetBannedUsers()
        {
            try
            {
                var res = await _client.GetAsync($"{_url}/rest/v1/users?IsBanned=eq.true&select=*");
                var json = await res.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetBannedUsers Exception] {ex}");
                return new List<User>();
            }
        }

        public async Task<List<User>> GetTopUsers(int count = 5)
        {
            try
            {
                var res = await _client.GetAsync($"{_url}/rest/v1/users?select=*&order=CorrectAnswers.desc&limit={count}");
                var json = await res.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetTopUsers Exception] {ex}");
                return new List<User>();
            }
        }

        public async Task<bool> DeleteUser(string username)
        {
            try
            {
                var safeUsername = Uri.EscapeDataString(username);
                var request = new HttpRequestMessage(HttpMethod.Delete, $"{_url}/rest/v1/users?Username=eq.{safeUsername}");
                request.Headers.Add("Prefer", "return=representation");
                var response = await _client.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteUser Exception] {ex}");
                return false;
            }
        }

        public async Task<bool> CheckConnection()
        {
            try
            {
                var res = await _client.GetAsync($"{_url}/rest/v1/users?select=Username&limit=1");
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CheckConnection Exception] {ex}");
                return false;
            }
        }

        public async Task<List<User>> GetAllUsersLight()
        {
            try
            {
                var res = await _client.GetAsync($"{_url}/rest/v1/users?select=Username,IsCheater,IsBanned,LastSeen,CorrectAnswers,TotalAnswered");
                var json = await res.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetAllUsersLight Exception] {ex}");
                return new List<User>();
            }
        }

        private static bool IsHashedPassword(string storedPassword)
        {
            return !string.IsNullOrWhiteSpace(storedPassword) && storedPassword.StartsWith(PasswordHashPrefix, StringComparison.Ordinal);
        }

        private static string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100000, HashAlgorithmName.SHA256, 32);
            return $"{PasswordHashPrefix}{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        private static bool VerifyPassword(string storedPassword, string providedPassword)
        {
            if (string.IsNullOrWhiteSpace(storedPassword) || string.IsNullOrWhiteSpace(providedPassword))
            {
                return false;
            }

            if (!IsHashedPassword(storedPassword))
            {
                return string.Equals(storedPassword, providedPassword, StringComparison.Ordinal);
            }

            var parts = storedPassword.Split('$');
            if (parts.Length != 3)
            {
                return false;
            }

            try
            {
                var salt = Convert.FromBase64String(parts[1]);
                var expectedHash = Convert.FromBase64String(parts[2]);
                var actualHash = Rfc2898DeriveBytes.Pbkdf2(providedPassword, salt, 100000, HashAlgorithmName.SHA256, expectedHash.Length);
                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch
            {
                return false;
            }
        }
    }
}
