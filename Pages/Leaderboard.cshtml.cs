using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Models;
using NoodlesSimulator.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace NoodlesSimulator.Pages;

public class LeaderboardModel : PageModel
{
    private readonly AuthService _authService;
    private readonly UserProgressService _progress;

    public LeaderboardModel(AuthService authService, UserProgressService progress = null)
    {
        _authService = authService;
        _progress = progress;
    }

    public string ActiveTab { get; set; } = "total";
    public string ScoreColumnTitle { get; set; } = "תשובות נכונות";
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
        CurrentUsername = HttpContext.Session.GetString("Username") ?? "";

        try
        {
            Entries = ActiveTab switch
            {
                "rate" => await BuildSuccessRateEntriesAsync(),
                "weekly" => BuildWeeklyEntries(),
                "exam" => BuildExamEntries(),
                "daily" => BuildDailyEntries(),
                _ => await BuildTotalEntriesAsync()
            };
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
            "exam" => "ציון מבחן",
            "daily" => "נכון באתגר",
            _ => "תשובות נכונות"
        };
    }

    private async Task<List<LeaderboardEntry>> BuildTotalEntriesAsync()
    {
        var users = await _authService.GetTopUsersAsync(50);
        return users.Select(u => new LeaderboardEntry
        {
            Username = u.Username,
            ScoreDisplay = u.CorrectAnswers.ToString(),
            IsOnline = u.LastSeen != null && u.LastSeen > DateTime.UtcNow.AddMinutes(-5)
        }).ToList();
    }

    private async Task<List<LeaderboardEntry>> BuildSuccessRateEntriesAsync()
    {
        var users = await _authService.GetTopUsersBySuccessRateAsync(50, 50);
        return users.Select(u => new LeaderboardEntry
        {
            Username = u.Username,
            ScoreDisplay = u.TotalAnswered > 0
                ? $"{(int)((double)u.CorrectAnswers / u.TotalAnswered * 100)}%"
                : "0%",
            IsOnline = u.LastSeen != null && u.LastSeen > DateTime.UtcNow.AddMinutes(-5)
        }).ToList();
    }

    private List<LeaderboardEntry> BuildWeeklyEntries()
    {
        if (_progress == null) return new List<LeaderboardEntry>();
        return _progress.GetWeeklyLeaderboard(50).Select(r => new LeaderboardEntry
        {
            Username = r.Username,
            ScoreDisplay = r.WeeklyCorrect.ToString(),
            IsOnline = false
        }).ToList();
    }

    private List<LeaderboardEntry> BuildExamEntries()
    {
        if (_progress == null) return new List<LeaderboardEntry>();
        return _progress.GetExamLeaderboard(50).Select(r => new LeaderboardEntry
        {
            Username = r.Username,
            ScoreDisplay = $"{r.BestExamScore} ({r.BestExamCorrect}/17)",
            IsOnline = false
        }).ToList();
    }

    private List<LeaderboardEntry> BuildDailyEntries()
    {
        if (_progress == null) return new List<LeaderboardEntry>();
        var today = UserProgressService.TodayKey();
        return _progress.GetDailyLeaderboard(today, 50).Select(r => new LeaderboardEntry
        {
            Username = r.Username,
            ScoreDisplay = $"{r.Score}/10",
            IsOnline = false
        }).ToList();
    }
}
