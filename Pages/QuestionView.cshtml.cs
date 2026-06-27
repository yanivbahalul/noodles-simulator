using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoodlesSimulator.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages;

public class QuestionViewModel : PageModel
{
    private static readonly string[] AnswerKeys = { "correct", "a", "b", "c" };

    private readonly SupabaseStorageService _storage;

    public QuestionViewModel(SupabaseStorageService storage = null)
    {
        _storage = storage;
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

        if (_storage != null)
            await LoadQuestionFromStorageAsync(questionId, selectedFile);
        else
            LoadQuestionFromLocalFiles(questionId, selectedFile);

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

    private async Task LoadQuestionFromStorageAsync(string questionId, string selectedFile)
    {
        var all = await _storage.ListFilesAsync("");
        var group = FindQuestionGroup(FilterImageNames(all), questionId);
        if (group == null)
            return;

        var signed = await _storage.GetSignedUrlsAsync(group);
        QuestionImageUrl = signed.TryGetValue(group[0], out var questionUrl) ? questionUrl : string.Empty;
        PopulateAnswerUrls(group, (key, file) =>
            !string.IsNullOrWhiteSpace(file) && signed.TryGetValue(file, out var url) ? url : null);
        ResolveSelectedAnswerKey(group, selectedFile);
    }

    private void LoadQuestionFromLocalFiles(string questionId, string selectedFile)
    {
        var imagesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
        if (!Directory.Exists(imagesDir))
            return;

        var group = FindQuestionGroup(
            Directory.GetFiles(imagesDir)
                .Where(IsImageFile)
                .Select(Path.GetFileName)
                .OrderBy(name => name)
                .ToList(),
            questionId);
        if (group == null)
            return;

        QuestionImageUrl = $"/images/{group[0]}";
        PopulateAnswerUrls(group, (key, file) =>
            string.IsNullOrWhiteSpace(file) ? null : $"/images/{file}");
        ResolveSelectedAnswerKey(group, selectedFile);
    }

    private static List<string> FindQuestionGroup(List<string> sortedImages, string questionId)
    {
        for (var i = 0; i + 4 < sortedImages.Count; i += 5)
        {
            var group = sortedImages.GetRange(i, 5);
            if (string.Equals(group[0], questionId, StringComparison.OrdinalIgnoreCase))
                return group;
        }

        return null;
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

    private static List<string> FilterImageNames(IEnumerable<string> names) =>
        names.Where(IsImageFile).OrderBy(name => name).ToList();

    private static bool IsImageFile(string path) =>
        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);

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
