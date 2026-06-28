using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using NoodlesSimulator.Services;
using NoodlesSimulator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages;

public class TestResultsModel : PageModel
{
    private readonly SupabaseStorageService _storage;
    private readonly TestSessionService _testSession;
    private readonly UserProgressService _userProgress;
    private readonly AchievementService _achievements;
    private readonly AuthService _authService;

    public TestResultsModel(SupabaseStorageService storage = null, TestSessionService testSession = null, UserProgressService userProgress = null, AchievementService achievements = null, AuthService authService = null)
    {
        _storage = storage;
        _testSession = testSession;
        _userProgress = userProgress;
        _achievements = achievements;
        _authService = authService;
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
        var username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
        {
            return RedirectToPage("/Login");
        }

        var token = Request.Query["token"].ToString();
        if (_testSession == null)
        {
            return StatusCode(503, "Test session service is not available.");
        }
        if (string.IsNullOrWhiteSpace(token))
        {
            return RedirectToPage("/MyExams");
        }

        var session = await _testSession.GetSessionAsync(token);
        if (session == null || session.Username != username)
        {
            return RedirectToPage("/MyExams");
        }

        if (string.Equals(session.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToPage("/Test", new { token });
        }

        ReviewToken = token;
        await LoadFromTestSession(session);
        await ProcessGamificationOnceAsync(username, session, token);
        return Page();
    }

    private async Task ProcessGamificationOnceAsync(string username, TestSession session, string token)
    {
        var processedKey = $"ProcessedExam_{token}";
        if (HttpContext.Session.GetString(processedKey) == "1")
        {
            if (_userProgress != null)
            {
                var cached = await _userProgress.LoadAsync(username);
                HasMistakes = cached.SessionMistakes.Count > 0;
            }
            return;
        }
        HttpContext.Session.SetString(processedKey, "1");
        await ProcessGamificationAsync(username, session);
    }

    private async Task ProcessGamificationAsync(string username, TestSession session)
    {
        if (_userProgress == null || _achievements == null) return;

        var questions = JsonSerializer.Deserialize<List<TestQuestion>>(session.QuestionsJson, AppJson.Options) ?? new List<TestQuestion>();
        var answers = JsonSerializer.Deserialize<List<TestAnswer>>(session.AnswersJson, AppJson.Options) ?? new List<TestAnswer>();
        var correctCount = answers.Count(a => a != null && a.IsCorrect);
        var total = questions.Count;
        var score = session.Score > 0 ? session.Score : ExamScoring.ScoreFromCorrectCount(correctCount);

        var progress = await _userProgress.LoadAsync(username);
        var isFirstExam = progress.ExamsCompleted == 0;
        var previousExamCorrect = await _userProgress.RecordExamCompleteAsync(username, correctCount, total, score);

        var wrongQuestions = new List<string>();
        for (int i = 0; i < questions.Count; i++)
        {
            var q = questions[i].Question;
            var a = i < answers.Count ? answers[i] : null;
            if (a != null && !a.IsCorrect && !string.IsNullOrEmpty(q))
                wrongQuestions.Add(q);
        }
        if (wrongQuestions.Count > 0)
            await _userProgress.AddSessionMistakesAsync(username, wrongQuestions);

        progress = await _userProgress.LoadAsync(username);
        HasMistakes = progress.SessionMistakes.Count > 0;
        NewAchievements = await _achievements.CheckExamAchievementsAsync(username, correctCount, total, isFirstExam, previousExamCorrect);

        if (_authService != null)
        {
            var user = await _authService.GetUserAsync(username);
            if (user != null)
            {
                user.Xp = progress.Xp;
                user.Level = QuizGamification.LevelFromXp(user.Xp);
                await _authService.UpdateUserAsync(user);
            }
        }
    }

    private async Task LoadFromTestSession(TestSession session)
    {
        var questions = JsonSerializer.Deserialize<List<TestQuestion>>(session.QuestionsJson, AppJson.Options) ?? new List<TestQuestion>();
        var answers = JsonSerializer.Deserialize<List<TestAnswer>>(session.AnswersJson, AppJson.Options) ?? new List<TestAnswer>();

        Total = questions.Count;
        CorrectCount = answers.Count(a => a != null && a.IsCorrect);
        MaxScore = session.MaxScore > 0 ? session.MaxScore : ExamScoring.MaxScore(Total);
        Score = session.Score > 0 ? session.Score : ExamScoring.ScoreFromCorrectCount(CorrectCount);

        var elapsed = (session.CompletedUtc ?? DateTime.UtcNow) - session.StartedUtc;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        ElapsedText = string.Format("{0:D2}:{1:D2}:{2:D2}", (int)elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds);

        var state = new TestState
        {
            StartedUtc = session.StartedUtc,
            Questions = questions,
            Answers = answers,
            CurrentIndex = session.CurrentIndex
        };

        await BuildItemsAsync(state);
    }

    private async Task BuildItemsAsync(TestState state)
    {
        var qList = state.Questions ?? new List<TestQuestion>();
        var aList = state.Answers ?? new List<TestAnswer>();

        for (int i = 0; i < qList.Count; i++)
        {
            var q = qList[i];
            var a = i < aList.Count ? aList[i] : null;
            var correctKey = AnswerOptionShuffle.ResolveCorrectKey(q);

            var resolved = await ImageUrlResolver.ResolveQuestionAndAnswersAsync(_storage, q.Question, q.Answers);
            var qUrl = resolved.QuestionUrl;
            var answerUrls = resolved.AnswerUrls;

            Items.Add(new ResultItem
            {
                QuestionUrl = qUrl,
                CorrectUrl = answerUrls.TryGetValue(correctKey, out var cu) ? cu : string.Empty,
                SelectedUrl = (a != null && !string.IsNullOrWhiteSpace(a.SelectedKey) && answerUrls.ContainsKey(a.SelectedKey)) ? answerUrls[a.SelectedKey] : string.Empty,
                IsCorrect = a?.IsCorrect == true
            });
        }
    }
}
