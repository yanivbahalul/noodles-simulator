using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

        public AuthService(IConfiguration config)
        {
            _url = config["SUPABASE_URL"]!;
            _apiKey = config["SUPABASE_KEY"]!;

            if (string.IsNullOrWhiteSpace(_url) || string.IsNullOrWhiteSpace(_apiKey))
                throw new Exception("Missing Supabase ENV vars.");

            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("apikey", _apiKey);
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<User?> Authenticate(string username, string password)
        {
            try
            {
                var res = await _client.GetAsync($"{_url}/rest/v1/users?Username=eq.{username}&Password=eq.{password}&select=*");
                var json = await res.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<List<User>>(json);
                return users?.FirstOrDefault();
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
                    Password = password,
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
                var res = await _client.GetAsync($"{_url}/rest/v1/users?Username=eq.{username}&select=*");
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
                var patch = new {
                    Username = updatedUser.Username,
                    Password = updatedUser.Password, // הוסף אם צריך לעדכן סיסמה
                    CorrectAnswers = updatedUser.CorrectAnswers,
                    TotalAnswered = updatedUser.TotalAnswered,
                    IsCheater = updatedUser.IsCheater,
                    IsBanned = updatedUser.IsBanned,
                    LastSeen = updatedUser.LastSeen ?? DateTime.UtcNow
                };

                var content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_url}/rest/v1/users?Username=eq.{updatedUser.Username}")
                {
                    Content = content
                };
                request.Headers.Add("Prefer", "return=representation");

                var response = await _client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[UpdateUser Error] PATCH failed for {updatedUser.Username}: {response.StatusCode}");
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
                var res = await _client.GetAsync($"{_url}/rest/v1/users?select=LastSeen");
                var json = await res.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<List<User>>(json);
                return users?.Count(u => u.LastSeen != null && u.LastSeen > DateTime.UtcNow.AddMinutes(-5)) ?? 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetOnlineUserCount Exception] {ex}");
                return 0;
            }
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
                var request = new HttpRequestMessage(HttpMethod.Delete, $"{_url}/rest/v1/users?Username=eq.{username}");
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
    }
}
