using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Models;
using NoodlesSimulator.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        public List<QuestionDifficulty> DifficultyQuestions { get; set; } = new();
        public int EasyCount { get; set; }
        public int MediumCount { get; set; }
        public int HardCount { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var username = HttpContext.Session.GetString("Username");
            if (!string.Equals(username, "Admin", StringComparison.Ordinal))
            {
                return RedirectToPage("/Login");
            }

            try
            {
                await LoadData();
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
                    // Auto-recalculate difficulties to ensure they're always up-to-date
                    await _difficultyService.RecalculateAllDifficulties();
                    
                    // Load updated questions
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

    }
}
