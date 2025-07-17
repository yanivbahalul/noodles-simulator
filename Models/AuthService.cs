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
            catch
            {
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
            catch
            {
                return null;
            }
        }

        public async Task UpdateUser(User updatedUser)
        {
            var patch = new[]
            {
                new {
                    Username = updatedUser.Username,
                    CorrectAnswers = updatedUser.CorrectAnswers,
                    TotalAnswered = updatedUser.TotalAnswered,
                    IsCheater = updatedUser.IsCheater,
                    IsBanned = updatedUser.IsBanned,
                    LastSeen = updatedUser.LastSeen ?? DateTime.UtcNow
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_url}/rest/v1/users?Username=eq.{updatedUser.Username}")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "return=representation");

            var response = await _client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"UpdateUser failed for {updatedUser.Username}");
        }

        public async Task<List<User>> GetAllUsers()
        {
            try
            {
                var res = await _client.GetAsync($"{_url}/rest/v1/users?select=*");
                var json = await res.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
            }
            catch
            {
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
            catch
            {
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
            catch
            {
                return false;
            }
        }
    }
}
