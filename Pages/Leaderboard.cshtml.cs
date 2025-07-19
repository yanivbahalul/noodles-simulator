using Microsoft.AspNetCore.Mvc.RazorPages;
using NoodlesSimulator.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace NoodlesSimulator.Pages
{
    public class LeaderboardModel : PageModel
    {
        private readonly AuthService _authService;

        public LeaderboardModel(AuthService authService)
        {
            _authService = authService;
        }

        public List<User> SortedUsers { get; set; } = new();

        public async Task OnGetAsync()
        {
            try
            {
                var users = await _authService.GetAllUsers();
                SortedUsers = users
                    .OrderByDescending(u => u.CorrectAnswers)
                    .ThenBy(u => u.TotalAnswered)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Leaderboard OnGetAsync Error] {ex}");
                SortedUsers = new List<User>();
            }
        }
    }
}
