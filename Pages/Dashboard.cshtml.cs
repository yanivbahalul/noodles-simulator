using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Models;
using NoodlesSimulator.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages;

public class DashboardModel : PageModel
{
    private readonly AuthService _authService;
    private readonly QuestionDifficultyService _difficultyService;
    private readonly UserFeedbackService _feedbackService;
    private readonly QuestionReportService _questionReports;
    private readonly DashboardDataService _dashboardData;

    public DashboardModel(
        AuthService authService,
        QuestionDifficultyService difficultyService = null,
        UserFeedbackService feedbackService = null,
        QuestionReportService questionReports = null,
        DashboardDataService dashboardData = null)
    {
        _authService = authService;
        _difficultyService = difficultyService;
        _feedbackService = feedbackService;
        _questionReports = questionReports;
        _dashboardData = dashboardData;
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

    public List<UserFeedbackService.UserFeedbackEntry> FeedbackEntries { get; set; } = new();
    public List<QuestionReportService.QuestionReportEntry> QuestionReports { get; set; } = new();
    public List<DashboardDataService.ProblematicQuestionRow> ProblematicQuestions { get; set; } = new();
    public int OpenQuestionReportsCount { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var isAdmin = HttpContext.Session.GetString("IsAdmin");
        if (!string.Equals(isAdmin, "1", StringComparison.Ordinal))
        {
            return RedirectToPage("/Login");
        }

        try
        {
            await LoadData();
            await LoadDifficultyData(recalculate: false);
            return Page();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Dashboard OnGetAsync Error] {ex}");
            return StatusCode(500, "Server error");
        }
    }

    public async Task<IActionResult> OnPostRecalculateDifficultiesAsync()
    {
        var isAdmin = HttpContext.Session.GetString("IsAdmin");
        if (!string.Equals(isAdmin, "1", StringComparison.Ordinal))
            return new JsonResult(new { success = false });

        try
        {
            await LoadDifficultyData(recalculate: true);
            return new JsonResult(new
            {
                success = true,
                easyCount = EasyCount,
                mediumCount = MediumCount,
                hardCount = HardCount,
                total = DifficultyQuestions.Count
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Dashboard RecalculateDifficulties Error] {ex}");
            return new JsonResult(new { success = false });
        }
    }

    private async Task LoadDifficultyData(bool recalculate)
    {
        try
        {
            if (_difficultyService != null)
            {
                if (recalculate)
                    await _difficultyService.RecalculateAllDifficultiesAsync();
                DifficultyQuestions = await _difficultyService.GetAllQuestionsAsync(500);
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
            AllUsers = await _authService.GetAllUsersLightAsync();
            Cheaters = AllUsers.Where(u => u.IsCheater).ToList();
            BannedUsers = AllUsers.Where(u => u.IsBanned).ToList();
            OnlineUsers = AllUsers.Where(u => AuthService.UserIsOnline(u) && !u.IsBanned && !u.IsCheater).ToList();
            TopUsers = AllUsers.OrderByDescending(u => u.CorrectAnswers).Take(5).ToList();
            AverageSuccessRate = AllUsers.Where(u => u.TotalAnswered > 0)
                .Select(u => (double)u.CorrectAnswers / u.TotalAnswered)
                .DefaultIfEmpty(0).Average() * 100;

            FeedbackEntries = _feedbackService != null && _feedbackService.IsEnabled
                ? await _feedbackService.GetSubmittedFeedbackAsync()
                : new List<UserFeedbackService.UserFeedbackEntry>();

            QuestionReports = _questionReports?.GetAll(100) ?? new List<QuestionReportService.QuestionReportEntry>();
            OpenQuestionReportsCount = _questionReports?.OpenCount ?? 0;
            ProblematicQuestions = _dashboardData != null
                ? await _dashboardData.GetProblematicQuestionsAsync()
                : new List<DashboardDataService.ProblematicQuestionRow>();
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
