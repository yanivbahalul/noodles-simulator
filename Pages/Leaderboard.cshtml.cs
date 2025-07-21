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
                SortedUsers = await _authService.GetTopUsers(50); // get top 50 users for leaderboard
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Leaderboard OnGetAsync Error] {ex}");
                SortedUsers = new List<User>();
            }
        }
    }
}
