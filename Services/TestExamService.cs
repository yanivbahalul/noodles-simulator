using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public enum TestExamGetAction
{
    ServiceUnavailable,
    CreateFailed,
    RedirectMyExams,
    RedirectTest,
    RedirectTestResults,
    ShowPage
}

public class TestExamGetResult
{
    public TestExamGetAction Action { get; init; }
    public string? RedirectToken { get; init; }
    public string? TempDataAlert { get; init; }
    public string? ErrorMessage { get; init; }
    public TestSession? Session { get; init; }
    public TestQuestionBinding? Binding { get; init; }
}

public class TestAnswerProcessResult
{
    public bool ServiceUnavailable { get; init; }
    public bool MissingToken { get; init; }
    public bool RedirectMyExams { get; init; }
    public string? RedirectPath { get; init; }
    public TestSession? Session { get; init; }
    public TestState? State { get; init; }
    public TestQuestionBinding? Binding { get; init; }
}

public class TestQuestionBinding
{
    public int CurrentIndex { get; init; }
    public int DisplayQuestionNumber => CurrentIndex + 1;
    public int TotalQuestions { get; init; }
    public int ProgressPercent => TotalQuestions == 0 ? 0 : CurrentIndex * 100 / TotalQuestions;
    public string QuestionImageUrl { get; init; } = "";
    public Dictionary<string, string> ShuffledAnswers { get; init; } = new();
    public Dictionary<string, string> AnswerImageUrls { get; init; } = new();
    public string TestEndUtcString { get; init; } = "";
    public int TestRemainingSeconds { get; init; }
}

public class TestExamService
{
    public const int DefaultQuestionCount = 17;

    private readonly SupabaseStorageService? _storage;
    private readonly TestSessionService? _testSession;
    private readonly QuestionDifficultyService? _difficultyService;
    private readonly ActivityEventService? _activityEvents;
    private readonly QuestionGroupLoader? _questionGroups;

    public TestExamService(
        SupabaseStorageService? storage = null,
        TestSessionService? testSession = null,
        QuestionDifficultyService? difficultyService = null,
        ActivityEventService? activityEvents = null,
        QuestionGroupLoader? questionGroups = null)
    {
        _storage = storage;
        _testSession = testSession;
        _difficultyService = difficultyService;
        _activityEvents = activityEvents;
        _questionGroups = questionGroups;
    }

    public async Task<TestExamGetResult> HandleGetAsync(
        string username,
        string? token,
        string? start,
        string? difficulty)
    {
        if (_testSession == null)
            return new TestExamGetResult
            {
                Action = TestExamGetAction.ServiceUnavailable,
                ErrorMessage = "Test session service is not available."
            };

        TestSession? session = null;

        if (!string.IsNullOrEmpty(token))
        {
            session = await _testSession.GetSessionAsync(token);
            if (session?.Username != username)
                return new TestExamGetResult { Action = TestExamGetAction.RedirectMyExams };
        }

        if (session == null)
            session = await _testSession.GetActiveSessionAsync(username);

        if (!string.IsNullOrEmpty(start) && session != null && session.Status == "active")
        {
            return new TestExamGetResult
            {
                Action = TestExamGetAction.RedirectTest,
                RedirectToken = session.Token,
                TempDataAlert = "קיים מבחן פעיל! עליך לסיים אותו על מנת להתחיל מבחן חדש."
            };
        }

        if (!string.IsNullOrEmpty(start) || session == null)
        {
            var state = await BuildNewStateAsync(difficulty);
            var questionsJson = JsonSerializer.Serialize(state.Questions, AppJson.Options);
            session = await _testSession.CreateSessionAsync(username, questionsJson);

            if (session == null)
                return new TestExamGetResult { Action = TestExamGetAction.CreateFailed };

            _activityEvents?.Log(username, ActivityEventCatalog.ExamStart, new Dictionary<string, object>
            {
                ["token"] = session.Token
            });

            return new TestExamGetResult
            {
                Action = TestExamGetAction.RedirectTest,
                RedirectToken = session.Token
            };
        }

        if (_testSession.IsExpired(session) || session.Status != "active")
        {
            if (session.Status == "active")
                await _testSession.ExpireSessionAsync(session);

            return new TestExamGetResult
            {
                Action = TestExamGetAction.RedirectTestResults,
                RedirectToken = session.Token
            };
        }

        var testState = DeserializeState(session);
        if (testState.CurrentIndex >= testState.Questions.Count)
        {
            await _testSession.UpdateSessionStatusAsync(session.Token, "completed");
            return new TestExamGetResult
            {
                Action = TestExamGetAction.RedirectTestResults,
                RedirectToken = session.Token
            };
        }

        var binding = await BindCurrentAsync(testState);
        return new TestExamGetResult
        {
            Action = TestExamGetAction.ShowPage,
            Session = session,
            Binding = binding
        };
    }

    public async Task<TestAnswerProcessResult> ProcessAnswerAsync(string username, string? token, string? selected)
    {
        if (_testSession == null)
            return new TestAnswerProcessResult { ServiceUnavailable = true };

        if (string.IsNullOrEmpty(token))
            return new TestAnswerProcessResult { MissingToken = true };

        var session = await _testSession.GetSessionAsync(token);
        if (session == null || session.Username != username)
            return new TestAnswerProcessResult { RedirectMyExams = true };

        if (_testSession.IsExpired(session) || session.Status != "active")
        {
            await _testSession.ExpireSessionAsync(session);
            return new TestAnswerProcessResult
            {
                RedirectPath = $"/TestResults?token={Uri.EscapeDataString(session.Token)}"
            };
        }

        var questions = JsonSerializer.Deserialize<List<TestQuestion>>(session.QuestionsJson, AppJson.Options)
                        ?? new List<TestQuestion>();
        var answers = JsonSerializer.Deserialize<List<TestAnswer>>(session.AnswersJson, AppJson.Options)
                      ?? new List<TestAnswer>();
        var idx = session.CurrentIndex;

        if (idx < 0 || idx >= questions.Count)
        {
            return new TestAnswerProcessResult
            {
                RedirectPath = $"/TestResults?token={Uri.EscapeDataString(session.Token)}"
            };
        }

        var isCorrect = AnswerOptionShuffle.IsSelectedCorrect(questions[idx], selected!);

        while (answers.Count < idx)
            answers.Add(new TestAnswer());

        var recorded = new TestAnswer { SelectedKey = selected, IsCorrect = isCorrect };
        if (answers.Count == idx)
            answers.Add(recorded);
        else if (idx < answers.Count)
            answers[idx] = recorded;

        try
        {
            var qid = questions[idx].Question;
            if (!string.IsNullOrEmpty(qid) && _difficultyService != null)
                _ = _difficultyService.UpdateQuestionStatsAsync(qid, isCorrect);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TestExamService Stats Update Error] {ex.Message}");
        }

        session.CurrentIndex = Math.Min(idx + 1, questions.Count);
        session.AnswersJson = JsonSerializer.Serialize(answers, AppJson.Options);
        session.Score = ExamScoring.ScoreFromCorrectCount(answers.Count(a => a != null && a.IsCorrect));
        session.MaxScore = ExamScoring.MaxScore(questions.Count);

        await _testSession.UpdateSessionAsync(session);

        if (_testSession.IsExpired(session) || session.CurrentIndex >= questions.Count)
        {
            if (session.Status == "active")
            {
                await _testSession.UpdateSessionStatusAsync(session.Token, "completed");
                LogExamComplete(session.Username, session.Score, session.MaxScore);
            }

            return new TestAnswerProcessResult
            {
                RedirectPath = $"/TestResults?token={Uri.EscapeDataString(session.Token)}"
            };
        }

        var state = new TestState
        {
            StartedUtc = TestSessionService.EnsureUtc(session.StartedUtc),
            Questions = questions,
            Answers = answers,
            CurrentIndex = session.CurrentIndex
        };

        var binding = await BindCurrentAsync(state);
        return new TestAnswerProcessResult
        {
            Session = session,
            State = state,
            Binding = binding
        };
    }

    public async Task<string?> EndTestAsync(string username, string? token)
    {
        if (_testSession == null || string.IsNullOrEmpty(token))
            return null;

        var session = await _testSession.GetSessionAsync(token);
        if (session?.Username != username)
            return null;

        var questions = JsonSerializer.Deserialize<List<TestQuestion>>(session.QuestionsJson, AppJson.Options)
                        ?? new List<TestQuestion>();
        var answers = JsonSerializer.Deserialize<List<TestAnswer>>(session.AnswersJson, AppJson.Options)
                      ?? new List<TestAnswer>();

        session.Status = "completed";
        session.CompletedUtc = DateTime.UtcNow;
        session.Score = ExamScoring.ScoreFromCorrectCount(answers.Count(a => a != null && a.IsCorrect));
        session.MaxScore = ExamScoring.MaxScore(questions.Count);

        await _testSession.UpdateSessionAsync(session);
        LogExamComplete(session.Username, session.Score, session.MaxScore);

        return session.Token;
    }

    public object BuildQuestionJsonResponse(TestQuestionBinding binding)
    {
        return new
        {
            questionImageUrl = binding.QuestionImageUrl,
            displayQuestionNumber = binding.DisplayQuestionNumber,
            totalQuestions = binding.TotalQuestions,
            progressPercent = binding.ProgressPercent,
            remainingSeconds = binding.TestRemainingSeconds,
            answers = binding.ShuffledAnswers
                .Select(kv => new
                {
                    key = kv.Key,
                    imageUrl = binding.AnswerImageUrls.TryGetValue(kv.Key, out var url) ? url : ""
                })
                .ToList(),
            redirect = (string?)null
        };
    }

    public async Task<TestState> BuildNewStateAsync(string? difficulty = null)
    {
        var state = new TestState
        {
            StartedUtc = DateTime.UtcNow,
            Questions = new List<TestQuestion>(),
            Answers = new List<TestAnswer>(),
            CurrentIndex = 0
        };

        if (_questionGroups == null)
            return state;

        var all = await _questionGroups.ListGroupsForDifficultyAsync(difficulty, _difficultyService);
        ListShuffle.FisherYates(all);
        foreach (var g in all.Take(DefaultQuestionCount))
        {
            var correct = g[1];
            var wrong = g.Skip(2).Take(3).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var shuffled = AnswerOptionShuffle.Create(correct, wrong);

            state.Questions.Add(new TestQuestion
            {
                Question = g[0],
                CorrectKey = shuffled.CorrectKey,
                Answers = shuffled.Options
            });
        }

        return state;
    }

    public async Task<TestQuestionBinding> BindCurrentAsync(TestState state)
    {
        var total = state.Questions.Count;
        var currentIndex = Math.Clamp(state.CurrentIndex, 0, Math.Max(0, total - 1));
        var q = state.Questions[currentIndex];
        var answers = q.Answers ?? new Dictionary<string, string>();
        var resolved = await ImageUrlResolver.ResolveQuestionAndAnswersAsync(_storage, q.Question, answers);
        var end = TestSessionService.GetExamEndUtc(state.StartedUtc);

        return new TestQuestionBinding
        {
            CurrentIndex = currentIndex,
            TotalQuestions = DefaultQuestionCount,
            QuestionImageUrl = resolved.QuestionUrl,
            ShuffledAnswers = q.Answers ?? new Dictionary<string, string>(),
            AnswerImageUrls = resolved.AnswerUrls,
            TestEndUtcString = end.ToString("o"),
            TestRemainingSeconds = TestSessionService.GetRemainingSeconds(state.StartedUtc)
        };
    }

    private static TestState DeserializeState(TestSession session)
    {
        return new TestState
        {
            StartedUtc = TestSessionService.EnsureUtc(session.StartedUtc),
            Questions = JsonSerializer.Deserialize<List<TestQuestion>>(session.QuestionsJson, AppJson.Options)
                        ?? new List<TestQuestion>(),
            Answers = JsonSerializer.Deserialize<List<TestAnswer>>(session.AnswersJson, AppJson.Options)
                      ?? new List<TestAnswer>(),
            CurrentIndex = session.CurrentIndex
        };
    }

    private void LogExamComplete(string username, int score, int maxScore)
    {
        _activityEvents?.Log(username, ActivityEventCatalog.ExamComplete, new Dictionary<string, object>
        {
            ["score"] = score,
            ["maxScore"] = maxScore
        });
    }
}
