using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace NoodlesSimulator.Pages;

public class LeaderboardModel : PageModel
{
    private readonly AuthService _authService;

    public LeaderboardModel(AuthService authService)
    {
        _authService = authService;
    }

    public List<User> SortedUsers { get; set; } = new();
    public string CurrentUsername { get; set; } = "";

    public async Task OnGetAsync()
    {
        try
        {
            CurrentUsername = HttpContext.Session.GetString("Username") ?? "";
            SortedUsers = await _authService.GetTopUsersAsync(50);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Leaderboard OnGetAsync Error] {ex}");
            SortedUsers = new List<User>();
        }
    }
}
