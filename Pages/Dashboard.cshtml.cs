using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NoodlesSimulator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace NoodlesSimulator.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly AuthService _authService;

        public DashboardModel(AuthService authService)
        {
            _authService = authService;
        }

        public List<User> AllUsers { get; set; } = new();
        public List<User> Cheaters { get; set; } = new();
        public List<User> BannedUsers { get; set; } = new();
        public List<User> OnlineUsers { get; set; } = new();
        public List<User> TopUsers { get; set; } = new();
        public double AverageSuccessRate { get; set; }

        public class ErrorReport
        {
            public string questionImage { get; set; }
            public string answers { get; set; }
            public string correctAnswer { get; set; }
            public string explanation { get; set; }
            public string selectedAnswer { get; set; }
            public string username { get; set; }
            public DateTime timestamp { get; set; }
        }

        public List<ErrorReport> ErrorReports { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                await LoadData();
                LoadErrorReports();
                return Page();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Dashboard OnGetAsync Error] {ex}");
                return StatusCode(500, $"Server error: {ex.Message}");
            }
        }

        private async Task LoadData()
        {
            try
            {
                AllUsers = await _authService.GetAllUsers();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Dashboard GetAllUsers Error] {ex}");
                AllUsers = new List<User>();
            }
            Cheaters = AllUsers.Where(u => u.IsCheater).ToList();
            BannedUsers = AllUsers.Where(u => u.IsBanned).ToList();
            OnlineUsers = AllUsers.Where(u => u.LastSeen != null && u.LastSeen > DateTime.UtcNow.AddMinutes(-5)).ToList();
            TopUsers = AllUsers.OrderByDescending(u => u.CorrectAnswers).Take(5).ToList();
            AverageSuccessRate = AllUsers.Where(u => u.TotalAnswered > 0)
                .Select(u => (double)u.CorrectAnswers / u.TotalAnswered)
                .DefaultIfEmpty(0).Average() * 100;
        }

        private void LoadErrorReports()
        {
            try
            {
                var reportsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports", "reports.json");
                if (System.IO.File.Exists(reportsPath))
                {
                    var json = System.IO.File.ReadAllText(reportsPath);
                    if (!string.IsNullOrWhiteSpace(json))
                        ErrorReports = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ErrorReport>>(json);
                    else
                        ErrorReports = new List<ErrorReport>();
                }
                else
                {
                    ErrorReports = new List<ErrorReport>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Dashboard LoadErrorReports Error] {ex}");
                ErrorReports = new List<ErrorReport>();
            }
        }
    }
}
