using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;
using NoodlesSimulator.Models;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Pages;

public class QuizDemoModel : PageModel
{
    private static readonly Regex ScreenshotName = new(
        @"^Screenshot at (\w{3}) (\d{1,2}) (\d{2})-(\d{2})-(\d{2})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, string> MonthNumbers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Jan"] = "01", ["Feb"] = "02", ["Mar"] = "03", ["Apr"] = "04",
        ["May"] = "05", ["Jun"] = "06", ["Jul"] = "07", ["Aug"] = "08",
        ["Sep"] = "09", ["Oct"] = "10", ["Nov"] = "11", ["Dec"] = "12"
    };

    private readonly IWebHostEnvironment _env;
    private readonly QuestionExplanationService _explanations;

    public QuizDemoModel(IWebHostEnvironment env, QuestionExplanationService explanations)
    {
        _env = env;
        _explanations = explanations;
    }

    public IReadOnlyList<string> ReadyQuestions { get; private set; } = Array.Empty<string>();
    public string? CurrentQuestion { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? q)
    {
        if (_env.IsProduction())
            return RedirectToPage("/Index");

        if (string.IsNullOrEmpty(HttpContext.Session.GetString("Username")))
            return RedirectToPage("/Login", new { returnUrl = "/Index/demo" });

        if (!string.IsNullOrWhiteSpace(q))
        {
            var trimmed = q.Trim();
            if (await _explanations.HasReadyExplanationAsync(trimmed))
            {
                HttpContext.Session.SetString(PracticeQuizService.DemoExplanationSessionKey, trimmed);
                return RedirectToPage("/Index");
            }
        }

        ReadyQuestions = await _explanations.ListReadyQuestionFilesAsync();
        CurrentQuestion = HttpContext.Session.GetString(PracticeQuizService.DemoExplanationSessionKey);
        return Page();
    }

    public static string FormatQuestionLabel(string questionFile)
    {
        if (string.IsNullOrWhiteSpace(questionFile))
            return "—";

        var name = Path.GetFileName(questionFile);
        var stem = Regex.Replace(name, @"\.(png|jpg|jpeg|webp)$", "", RegexOptions.IgnoreCase);
        var match = ScreenshotName.Match(stem);
        if (!match.Success)
            return stem.Length > 28 ? $"{stem[..25]}…" : stem;

        var mon = MonthNumbers.TryGetValue(match.Groups[1].Value, out var m) ? m : match.Groups[1].Value;
        return $"{match.Groups[2].Value.PadLeft(2, '0')}/{mon} {match.Groups[3].Value}:{match.Groups[4].Value}";
    }
}
