using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NoodlesSimulator.Models;
using NoodlesSimulator.Services;
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
        private readonly QuestionDifficultyService _difficultyService;

        public DashboardModel(AuthService authService, QuestionDifficultyService difficultyService = null)
        {
            _authService = authService;
            _difficultyService = difficultyService;
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
        
        public List<QuestionDifficulty> DifficultyQuestions { get; set; } = new();
        public int EasyCount { get; set; }
        public int MediumCount { get; set; }
        public int HardCount { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                await LoadData();
                LoadErrorReports();
                await LoadDifficultyData();
                return Page();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Dashboard OnGetAsync Error] {ex}");
                return StatusCode(500, $"Server error: {ex.Message}");
            }
        }

        private async Task LoadDifficultyData()
        {
            try
            {
                if (_difficultyService != null)
                {
                    DifficultyQuestions = await _difficultyService.GetAllQuestions(500);
                    EasyCount = DifficultyQuestions.Count(q => q.Difficulty == "easy");
                    MediumCount = DifficultyQuestions.Count(q => q.Difficulty == "medium");
                    HardCount = DifficultyQuestions.Count(q => q.Difficulty == "hard");
                    
                    Console.WriteLine($"[Dashboard] Loaded {DifficultyQuestions.Count} questions: Easy={EasyCount}, Medium={MediumCount}, Hard={HardCount}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Dashboard] Error loading difficulty data: {ex.Message}");
                DifficultyQuestions = new List<QuestionDifficulty>();
            }
        }

        public async Task<IActionResult> OnPostRecalculateDifficulties()
        {
            try
            {
                if (_difficultyService != null)
                {
                    var count = await _difficultyService.RecalculateAllDifficulties();
                    Console.WriteLine($"[Dashboard] Recalculated {count} difficulties");
                    TempData["DifficultyMessage"] = $"עודכנו {count} רמות קושי אוטומטית!";
                }
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Dashboard] Error recalculating: {ex}");
                TempData["DifficultyError"] = $"שגיאה: {ex.Message}";
                return RedirectToPage();
            }
        }

        private async Task LoadData()
        {
            try
            {
                AllUsers = await _authService.GetAllUsersLight();
                Cheaters = AllUsers.Where(u => u.IsCheater).ToList();
                BannedUsers = AllUsers.Where(u => u.IsBanned).ToList();
                OnlineUsers = AllUsers.Where(u => u.LastSeen != null && u.LastSeen > DateTime.UtcNow.AddMinutes(-5)).ToList();
                TopUsers = AllUsers.OrderByDescending(u => u.CorrectAnswers).Take(5).ToList();
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
                var isProd = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
                var reportsPath = isProd 
                    ? "/data-keys/reports/reports.json"
                    : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports", "reports.json");
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
