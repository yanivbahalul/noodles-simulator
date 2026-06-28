using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public sealed class TestResultsPageData
{
    public int Score { get; init; }
    public int MaxScore { get; init; }
    public int CorrectCount { get; init; }
    public int Total { get; init; }
    public string ElapsedText { get; init; } = "";
    public string ReviewToken { get; init; } = "";
    public bool HasMistakes { get; set; }
    public List<string> NewAchievements { get; set; } = new();
    public List<TestResultsItem> Items { get; init; } = new();
}

public sealed class TestResultsItem
{
    public string QuestionUrl { get; init; } = "";
    public string SelectedUrl { get; init; } = "";
    public string CorrectUrl { get; init; } = "";
    public bool IsCorrect { get; init; }
}

public enum TestResultsRedirect
{
    None,
    Login,
    MyExams,
    ActiveTest,
    ServiceUnavailable
}

public sealed class TestResultsLoadResult
{
    public TestResultsRedirect Redirect { get; init; }
    public string? Token { get; init; }
    public TestResultsPageData? Data { get; init; }
}

public class TestResultsPageService
{
    private readonly SupabaseStorageService? _storage;
    private readonly TestSessionService? _testSession;
    private readonly UserProgressService? _userProgress;
    private readonly AchievementService? _achievements;
    private readonly AuthService? _authService;

    public TestResultsPageService(
        SupabaseStorageService? storage = null,
        TestSessionService? testSession = null,
        UserProgressService? userProgress = null,
        AchievementService? achievements = null,
        AuthService? authService = null)
    {
        _storage = storage;
        _testSession = testSession;
        _userProgress = userProgress;
        _achievements = achievements;
        _authService = authService;
    }

    public async Task<TestResultsLoadResult> LoadAsync(HttpContext http, string token)
    {
        var username = http.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
            return new TestResultsLoadResult { Redirect = TestResultsRedirect.Login };

        if (_testSession == null)
            return new TestResultsLoadResult { Redirect = TestResultsRedirect.ServiceUnavailable };

        if (string.IsNullOrWhiteSpace(token))
            return new TestResultsLoadResult { Redirect = TestResultsRedirect.MyExams };

        var session = await _testSession.GetSessionAsync(token);
        if (session == null || session.Username != username)
            return new TestResultsLoadResult { Redirect = TestResultsRedirect.MyExams };

        if (string.Equals(session.Status, "active", StringComparison.OrdinalIgnoreCase))
            return new TestResultsLoadResult { Redirect = TestResultsRedirect.ActiveTest, Token = token };

        var data = await BuildPageDataAsync(session, token);
        await ProcessGamificationOnceAsync(http, username, session, token, data);
        return new TestResultsLoadResult { Redirect = TestResultsRedirect.None, Data = data };
    }

    private async Task<TestResultsPageData> BuildPageDataAsync(TestSession session, string token)
    {
        var questions = JsonSerializer.Deserialize<List<TestQuestion>>(session.QuestionsJson, AppJson.Options) ?? new List<TestQuestion>();
        var answers = JsonSerializer.Deserialize<List<TestAnswer>>(session.AnswersJson, AppJson.Options) ?? new List<TestAnswer>();

        var total = questions.Count;
        var correctCount = answers.Count(a => a != null && a.IsCorrect);
        var maxScore = session.MaxScore > 0 ? session.MaxScore : ExamScoring.MaxScore(total);
        var score = session.Score > 0 ? session.Score : ExamScoring.ScoreFromCorrectCount(correctCount);

        var elapsed = (session.CompletedUtc ?? DateTime.UtcNow) - session.StartedUtc;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        var elapsedText = string.Format("{0:D2}:{1:D2}:{2:D2}", (int)elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds);

        var state = new TestState
        {
            StartedUtc = session.StartedUtc,
            Questions = questions,
            Answers = answers,
            CurrentIndex = session.CurrentIndex
        };

        var items = await BuildItemsAsync(state);

        return new TestResultsPageData
        {
            Score = score,
            MaxScore = maxScore,
            CorrectCount = correctCount,
            Total = total,
            ElapsedText = elapsedText,
            ReviewToken = token,
            Items = items
        };
    }

    private async Task ProcessGamificationOnceAsync(
        HttpContext http,
        string username,
        TestSession session,
        string token,
        TestResultsPageData data)
    {
        var processedKey = $"ProcessedExam_{token}";
        if (http.Session.GetString(processedKey) == "1")
        {
            if (_userProgress != null)
            {
                var cached = await _userProgress.LoadAsync(username);
                data.HasMistakes = cached.SessionMistakes.Count > 0;
            }
            return;
        }

        http.Session.SetString(processedKey, "1");
        await ApplyGamificationAsync(username, session, data);
    }

    private async Task ApplyGamificationAsync(string username, TestSession session, TestResultsPageData data)
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
        for (var i = 0; i < questions.Count; i++)
        {
            var q = questions[i].Question;
            var a = i < answers.Count ? answers[i] : null;
            if (a != null && !a.IsCorrect && !string.IsNullOrEmpty(q))
                wrongQuestions.Add(q);
        }

        if (wrongQuestions.Count > 0)
            await _userProgress.AddSessionMistakesAsync(username, wrongQuestions);

        progress = await _userProgress.LoadAsync(username);
        data.HasMistakes = progress.SessionMistakes.Count > 0;
        data.NewAchievements = await _achievements.CheckExamAchievementsAsync(username, correctCount, total, isFirstExam, previousExamCorrect);

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

    private async Task<List<TestResultsItem>> BuildItemsAsync(TestState state)
    {
        var items = new List<TestResultsItem>();
        var qList = state.Questions ?? new List<TestQuestion>();
        var aList = state.Answers ?? new List<TestAnswer>();

        for (var i = 0; i < qList.Count; i++)
        {
            var q = qList[i];
            var a = i < aList.Count ? aList[i] : null;
            var correctKey = AnswerOptionShuffle.ResolveCorrectKey(q);

            var resolved = await ImageUrlResolver.ResolveQuestionAndAnswersAsync(_storage, q.Question, q.Answers);
            var qUrl = resolved.QuestionUrl;
            var answerUrls = resolved.AnswerUrls;

            items.Add(new TestResultsItem
            {
                QuestionUrl = qUrl,
                CorrectUrl = answerUrls.TryGetValue(correctKey, out var cu) ? cu : string.Empty,
                SelectedUrl = (a != null && !string.IsNullOrWhiteSpace(a.SelectedKey) && answerUrls.ContainsKey(a.SelectedKey))
                    ? answerUrls[a.SelectedKey]
                    : string.Empty,
                IsCorrect = a?.IsCorrect == true
            });
        }

        return items;
    }
}
