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
                Cheaters = await _authService.GetCheaters();
                BannedUsers = await _authService.GetBannedUsers();
                TopUsers = await _authService.GetTopUsers(5);
                OnlineUsers = Cheaters.Concat(BannedUsers).Concat(TopUsers)
                    .Where(u => u.LastSeen != null && u.LastSeen > DateTime.UtcNow.AddMinutes(-5))
                    .Distinct().ToList();
                // אם אתה רוצה את כל המשתמשים המחוברים, אפשר להוסיף פונקציה ייעודית ב-AuthService
                AllUsers = Cheaters.Concat(BannedUsers).Concat(TopUsers).Distinct().ToList();
                AverageSuccessRate = AllUsers.Where(u => u.TotalAnswered > 0)
                    .Select(u => (double)u.CorrectAnswers / u.TotalAnswered)
                    .DefaultIfEmpty(0).Average() * 100;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Dashboard LoadData Error] {ex}");
                Cheaters = new List<User>();
                BannedUsers = new List<User>();
                TopUsers = new List<User>();
                OnlineUsers = new List<User>();
                AllUsers = new List<User>();
                AverageSuccessRate = 0;
            }
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
