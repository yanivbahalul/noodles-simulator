using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text;
using System.Text.Json;
using NoodlesSimulator.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace NoodlesSimulator.Pages;

public class TestModel : PageModel
{
    private readonly TestExamService _exam;

    public TestModel(TestExamService exam)
    {
        _exam = exam;
    }

    public bool AnswerChecked { get; set; }
    public bool IsCorrect { get; set; }
    public string SelectedAnswer { get; set; }
    public string QuestionImageUrl { get; set; }
    public Dictionary<string, string> ShuffledAnswers { get; set; }
    public Dictionary<string, string> AnswerImageUrls { get; set; }
    public int CurrentIndex { get; set; }
    public int DisplayQuestionNumber => CurrentIndex + 1;
    public int TotalQuestions { get; set; }
    public int QuestionCount => TotalQuestions > 0 ? TotalQuestions : TestExamService.DefaultQuestionCount;
    public int AnsweredCount { get; set; }
    public int ProgressPercent => QuestionCount == 0 ? 0 : AnsweredCount * 100 / QuestionCount;
    public string? SelectedAnswerKey { get; set; }
    public List<bool> AnsweredByIndex { get; set; } = new();
    public string TestEndUtcString { get; set; }
    public int TestRemainingSeconds { get; set; }

    public async Task<IActionResult> OnGet()
    {
        var username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
            return RedirectToPage("/Login");

        var result = await _exam.HandleGetAsync(
            username,
            Request.Query["token"].ToString(),
            Request.Query["start"].ToString(),
            Request.Query["difficulty"].ToString());

        switch (result.Action)
        {
            case TestExamGetAction.ServiceUnavailable:
                return StatusCode(503, result.ErrorMessage);
            case TestExamGetAction.CreateFailed:
                return StatusCode(500, "Failed to create test session.");
            case TestExamGetAction.RedirectMyExams:
                return RedirectToPage("/MyExams");
            case TestExamGetAction.RedirectTest:
                if (!string.IsNullOrEmpty(result.TempDataAlert))
                    TempData["ActiveTestAlert"] = result.TempDataAlert;
                return RedirectToPage("/Test", new { token = result.RedirectToken });
            case TestExamGetAction.RedirectTestResults:
                return RedirectToPage("/TestResults", new { token = result.RedirectToken });
            case TestExamGetAction.ShowPage:
                ApplyBinding(result.Binding!);
                ViewData["Token"] = result.Session!.Token;
                return Page();
            default:
                return RedirectToPage("/MyExams");
        }
    }

    public async Task<IActionResult> OnPost()
    {
        var username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
            return RedirectToPage("/Login");

        var token = Request.Form["token"].ToString();
        var selected = Request.Form["answer"].ToString();
        var result = await _exam.ProcessAnswerAsync(username, token, selected);

        if (result.ServiceUnavailable)
            return StatusCode(503, "Test session service is not available.");
        if (result.MissingToken)
            return BadRequest("Missing test token.");
        if (result.RedirectMyExams)
            return RedirectToPage("/MyExams");
        if (result.RedirectPath != null)
            return Redirect(result.RedirectPath);

        ApplyBinding(result.Binding!);
        return RedirectToPage("/Test", new { token });
    }

    public async Task<IActionResult> OnPostSubmitAnswerAsync()
    {
        try
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
                return new JsonResult(new { error = "Unauthorized", redirect = "/Login" }) { StatusCode = 401 };

            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                body = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
                return new JsonResult(new { error = "Empty body" }) { StatusCode = 400 };

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("token", out var tokenEl) || !root.TryGetProperty("answer", out var answerEl))
                return new JsonResult(new { error = "Invalid body" }) { StatusCode = 400 };

            var token = tokenEl.GetString();
            var selected = answerEl.GetString();
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(selected))
                return new JsonResult(new { error = "Invalid body" }) { StatusCode = 400 };

            var result = await _exam.ProcessAnswerAsync(username, token, selected);
            if (result.ServiceUnavailable)
                return new JsonResult(new { error = "Service unavailable" }) { StatusCode = 503 };
            if (result.MissingToken)
                return new JsonResult(new { error = "Invalid body" }) { StatusCode = 400 };
            if (result.RedirectMyExams)
                return new JsonResult(new { redirect = "/MyExams" });
            if (result.RedirectPath != null)
                return new JsonResult(new { redirect = result.RedirectPath });

            ApplyBinding(result.Binding!);
            return new JsonResult(_exam.BuildQuestionJsonResponse(result.Binding!));
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"[OnPostSubmitAnswerAsync Error] {ex}");
            return new JsonResult(new { error = "Server error" }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> OnPostNavigateAsync()
    {
        try
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
                return new JsonResult(new { error = "Unauthorized", redirect = "/Login" }) { StatusCode = 401 };

            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                body = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
                return new JsonResult(new { error = "Empty body" }) { StatusCode = 400 };

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("token", out var tokenEl) || !root.TryGetProperty("index", out var indexEl))
                return new JsonResult(new { error = "Invalid body" }) { StatusCode = 400 };

            var token = tokenEl.GetString();
            if (string.IsNullOrWhiteSpace(token) || !indexEl.TryGetInt32(out var index))
                return new JsonResult(new { error = "Invalid body" }) { StatusCode = 400 };

            var result = await _exam.NavigateToQuestionAsync(username, token, index);
            if (result.ServiceUnavailable)
                return new JsonResult(new { error = "Service unavailable" }) { StatusCode = 503 };
            if (result.MissingToken)
                return new JsonResult(new { error = "Invalid body" }) { StatusCode = 400 };
            if (result.RedirectMyExams)
                return new JsonResult(new { redirect = "/MyExams" });
            if (result.RedirectPath != null)
                return new JsonResult(new { redirect = result.RedirectPath });

            ApplyBinding(result.Binding!);
            return new JsonResult(_exam.BuildQuestionJsonResponse(result.Binding!));
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"[OnPostNavigateAsync Error] {ex}");
            return new JsonResult(new { error = "Server error" }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> OnPostEndTest()
    {
        var username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
            return RedirectToPage("/Login");

        var token = Request.Form["token"].ToString();
        var endedToken = await _exam.EndTestAsync(username, token);
        if (endedToken == null)
            return RedirectToPage("/TestResults");

        return RedirectToPage("/TestResults", new { token = endedToken });
    }

    private void ApplyBinding(TestQuestionBinding binding)
    {
        CurrentIndex = binding.CurrentIndex;
        TotalQuestions = binding.TotalQuestions;
        AnsweredCount = binding.AnsweredCount;
        SelectedAnswerKey = binding.SelectedAnswerKey;
        AnsweredByIndex = binding.AnsweredByIndex;
        QuestionImageUrl = binding.QuestionImageUrl;
        ShuffledAnswers = binding.ShuffledAnswers;
        AnswerImageUrls = binding.AnswerImageUrls;
        TestEndUtcString = binding.TestEndUtcString;
        TestRemainingSeconds = binding.TestRemainingSeconds;
    }
}
