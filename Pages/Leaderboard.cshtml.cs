using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace NoodlesSimulator.Pages;

public class LeaderboardModel : PageModel
{
    private readonly LeaderboardDataService _leaderboard;

    public LeaderboardModel(LeaderboardDataService leaderboard)
    {
        _leaderboard = leaderboard;
    }

    public string ActiveTab { get; set; } = "total";
    public string ScoreColumnTitle { get; set; } = "תשובות נכונות";
    public string TabHint { get; set; } = "";
    public List<LeaderboardEntry> Entries { get; set; } = new();
    public string CurrentUsername { get; set; } = "";

    public class LeaderboardEntry
    {
        public string Username { get; set; } = "";
        public string ScoreDisplay { get; set; } = "";
        public bool IsOnline { get; set; }
    }

    public async Task OnGetAsync(string tab = "total")
    {
        ActiveTab = string.IsNullOrWhiteSpace(tab) ? "total" : tab;
        if (ActiveTab == "daily") ActiveTab = "level";
        CurrentUsername = HttpContext.Session.GetString("Username") ?? "";

        try
        {
            var (rows, hint) = await _leaderboard.GetRowsAsync(ActiveTab);
            TabHint = hint;
            Entries = rows.Select(r => new LeaderboardEntry
            {
                Username = r.Username,
                ScoreDisplay = r.ScoreDisplay,
                IsOnline = r.IsOnline
            }).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Leaderboard OnGetAsync Error] {ex}");
            Entries = new List<LeaderboardEntry>();
        }

        ScoreColumnTitle = ActiveTab switch
        {
            "rate" => "אחוז הצלחה",
            "weekly" => "נכונות השבוע",
            "exam" => "מבחנים שהושלמו",
            "achievement" or "achievements" => "הישגים",
            "level" => "רמה",
            _ => "תשובות נכונות"
        };
    }
}
