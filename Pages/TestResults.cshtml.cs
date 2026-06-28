using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NoodlesSimulator.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages;

public class TestResultsModel : PageModel
{
    private readonly TestResultsPageService _resultsPage;

    public TestResultsModel(TestResultsPageService resultsPage = null)
    {
        _resultsPage = resultsPage;
    }

    public int Score { get; set; }
    public int MaxScore { get; set; }
    public int CorrectCount { get; set; }
    public int Total { get; set; }
    public string ElapsedText { get; set; }
    public string ReviewToken { get; set; } = string.Empty;
    public bool HasMistakes { get; set; }
    public List<string> NewAchievements { get; set; } = new();

    public class ResultItem
    {
        public string QuestionUrl { get; set; }
        public string SelectedUrl { get; set; }
        public string CorrectUrl { get; set; }
        public bool IsCorrect { get; set; }
    }

    public List<ResultItem> Items { get; set; } = new List<ResultItem>();

    public async Task<IActionResult> OnGet()
    {
        if (_resultsPage == null)
            return StatusCode(503, "Test results service is not available.");

        var token = Request.Query["token"].ToString();
        var result = await _resultsPage.LoadAsync(HttpContext, token);

        switch (result.Redirect)
        {
            case TestResultsRedirect.Login:
                return RedirectToPage("/Login");
            case TestResultsRedirect.MyExams:
                return RedirectToPage("/MyExams");
            case TestResultsRedirect.ActiveTest:
                return RedirectToPage("/Test", new { token = result.Token });
            case TestResultsRedirect.ServiceUnavailable:
                return StatusCode(503, "Test session service is not available.");
            case TestResultsRedirect.None:
                break;
            default:
                return StatusCode(500, "Unexpected test results state.");
        }

        ApplyPageData(result.Data);
        return Page();
    }

    private void ApplyPageData(TestResultsPageData data)
    {
        if (data == null) return;

        Score = data.Score;
        MaxScore = data.MaxScore;
        CorrectCount = data.CorrectCount;
        Total = data.Total;
        ElapsedText = data.ElapsedText;
        ReviewToken = data.ReviewToken;
        HasMistakes = data.HasMistakes;
        NewAchievements = data.NewAchievements;
        Items = data.Items.ConvertAll(item => new ResultItem
        {
            QuestionUrl = item.QuestionUrl,
            SelectedUrl = item.SelectedUrl,
            CorrectUrl = item.CorrectUrl,
            IsCorrect = item.IsCorrect
        });
    }
}
