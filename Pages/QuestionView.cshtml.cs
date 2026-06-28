using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages;

public class QuestionViewModel : PageModel
{
    private static readonly string[] AnswerKeys = { "correct", "a", "b", "c" };

    private readonly SupabaseStorageService _storage;
    private readonly QuestionGroupLoader _questionGroups;

    public QuestionViewModel(SupabaseStorageService storage = null, QuestionGroupLoader questionGroups = null)
    {
        _storage = storage;
        _questionGroups = questionGroups;
    }

    public string QuestionImageUrl { get; set; }
    public Dictionary<string, string> AnswerImageUrls { get; set; } = new Dictionary<string, string>();
    public string SelectedAnswerKey { get; set; }
    public string CorrectAnswerKey { get; set; } = "correct";
    public bool ShowAnswerResults { get; set; }
    public string BackUrl { get; set; } = "/Index";
    public string BackLabel { get; set; } = "⬅ חזרה לחידון";

    public async Task<IActionResult> OnGet()
    {
        var username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrWhiteSpace(username))
            return RedirectToPage("/Login");

        SetBackNavigation(Request.Query["from"].ToString(), Request.Query["scope"].ToString());

        var questionId = Request.Query["id"].ToString();
        if (string.IsNullOrWhiteSpace(questionId))
            return Page();

        var selectedFile = ApplyQueryParameters();
        await LoadQuestionAsync(questionId, selectedFile);
        return Page();
    }

    private string ApplyQueryParameters()
    {
        SelectedAnswerKey = Request.Query["selected"].ToString();
        var selectedFile = Request.Query["selectedFile"].ToString();
        var correctParam = Request.Query["correct"].ToString();
        if (!string.IsNullOrWhiteSpace(correctParam))
            CorrectAnswerKey = correctParam;

        ShowAnswerResults = !string.IsNullOrWhiteSpace(SelectedAnswerKey)
                            || !string.IsNullOrWhiteSpace(selectedFile);
        return selectedFile;
    }

    private async Task LoadQuestionAsync(string questionId, string selectedFile)
    {
        if (_questionGroups == null)
            return;

        var group = await _questionGroups.FindGroupByQuestionIdAsync(questionId);
        if (group == null)
            return;

        if (_storage != null)
        {
            var signed = await _storage.GetSignedUrlsAsync(group);
            QuestionImageUrl = signed.TryGetValue(group[0], out var questionUrl) ? questionUrl : string.Empty;
            PopulateAnswerUrls(group, (key, file) =>
                !string.IsNullOrWhiteSpace(file) && signed.TryGetValue(file, out var url) ? url : null);
        }
        else
        {
            QuestionImageUrl = $"/images/{group[0]}";
            PopulateAnswerUrls(group, (key, file) =>
                string.IsNullOrWhiteSpace(file) ? null : $"/images/{file}");
        }

        ResolveSelectedAnswerKey(group, selectedFile);
    }

    private void PopulateAnswerUrls(List<string> group, Func<string, string, string> resolveUrl)
    {
        for (var k = 1; k < group.Count && k - 1 < AnswerKeys.Length; k++)
        {
            var key = AnswerKeys[k - 1];
            var url = resolveUrl(key, group[k]);
            if (!string.IsNullOrWhiteSpace(url))
                AnswerImageUrls[key] = url;
        }
    }

    private void ResolveSelectedAnswerKey(List<string> group, string selectedFile)
    {
        if (string.IsNullOrWhiteSpace(selectedFile))
            return;

        for (var k = 1; k < group.Count && k - 1 < AnswerKeys.Length; k++)
        {
            if (string.Equals(group[k], selectedFile, StringComparison.OrdinalIgnoreCase))
            {
                SelectedAnswerKey = AnswerKeys[k - 1];
                break;
            }
        }
    }

    private void SetBackNavigation(string from, string scope)
    {
        if (string.Equals(from, "dashboard", StringComparison.OrdinalIgnoreCase))
        {
            BackUrl = "/Dashboard";
            BackLabel = "⬅ חזרה לניהול";
            return;
        }

        if (string.Equals(from, "stats", StringComparison.OrdinalIgnoreCase))
        {
            BackUrl = string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase)
                ? "/Stats?scope=all"
                : "/Stats";
            BackLabel = "⬅ חזרה לסטטיסטיקה";
        }
    }
}
