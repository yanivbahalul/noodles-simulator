using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NoodlesSimulator.Services;
using NoodlesSimulator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages
{
    public class TestResultsModel : PageModel
    {
        private const string SessionKey = "TestStateV1";
        private readonly SupabaseStorageService _storage;
        private readonly TestSessionService _testSession;

        public TestResultsModel(SupabaseStorageService storage = null, TestSessionService testSession = null)
        {
            _storage = storage;
            _testSession = testSession;
        }

        public int Score { get; set; }
        public int MaxScore { get; set; }
        public int CorrectCount { get; set; }
        public int Total { get; set; }
        public string ElapsedText { get; set; }

        public class ResultItem
        {
            public string QuestionUrl { get; set; }
            public string SelectedUrl { get; set; }
            public string CorrectUrl { get; set; }
            public bool IsCorrect { get; set; }
        }

        public List<ResultItem> Items { get; set; } = new List<ResultItem>();

        public class TestQuestion { public string Question { get; set; } public Dictionary<string, string> Answers { get; set; } }
        public class TestAnswer { public string SelectedKey { get; set; } public bool IsCorrect { get; set; } }
        public class TestState { public DateTime StartedUtc { get; set; } public List<TestQuestion> Questions { get; set; } public List<TestAnswer> Answers { get; set; } public int CurrentIndex { get; set; } }

        public async Task OnGet()
        {
            var token = Request.Query["token"].ToString();

            // Try token-based system first
            if (_testSession != null && !string.IsNullOrEmpty(token))
            {
                var session = await _testSession.GetSession(token);
                if (session != null)
                {
                    // Verify user owns this test
                    var username = HttpContext.Session.GetString("Username");
                    if (!string.IsNullOrEmpty(username) && session.Username == username)
                    {
                        await LoadFromTestSession(session);
                        return;
                    }
                }
            }

            // Fallback to legacy session-based system
            var state = GetState();
            if (state == null)
            {
                Total = 0; MaxScore = 0; Score = 0; CorrectCount = 0; ElapsedText = "-";
                return;
            }

            Total = state.Questions?.Count ?? 0;
            CorrectCount = state.Answers?.Count(a => a != null && a.IsCorrect) ?? 0;
            MaxScore = Total * 6;
            Score = CorrectCount * 6;
            var elapsed = DateTime.UtcNow - state.StartedUtc;
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
            ElapsedText = string.Format("{0:D2}:{1:D2}:{2:D2}", (int)elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds);

            await BuildItemsAsync(state);
            // Optionally clear state to prevent revisiting
            // HttpContext.Session.Remove(SessionKey);
        }

        private async Task LoadFromTestSession(TestSession session)
        {
            var questions = JsonConvert.DeserializeObject<List<TestQuestion>>(session.QuestionsJson) ?? new List<TestQuestion>();
            var answers = JsonConvert.DeserializeObject<List<TestAnswer>>(session.AnswersJson) ?? new List<TestAnswer>();

            Total = questions.Count;
            CorrectCount = answers.Count(a => a != null && a.IsCorrect);
            MaxScore = session.MaxScore > 0 ? session.MaxScore : Total * 6;
            Score = session.Score > 0 ? session.Score : CorrectCount * 6;

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
                var correctKey = "correct";

                string qUrl;
                var answerUrls = new Dictionary<string, string>();

                if (_storage != null)
                {
                    var paths = new List<string> { q.Question };
                    var answerVals = (q.Answers != null) ? new List<string>(q.Answers.Values) : new List<string>();
                    paths.AddRange(answerVals);
                    var signed = await _storage.GetSignedUrlsAsync(paths);
                    qUrl = signed.TryGetValue(q.Question, out var qu) ? qu : string.Empty;
                    foreach (var kv in q.Answers ?? new Dictionary<string, string>())
                    {
                        if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                        answerUrls[kv.Key] = signed.TryGetValue(kv.Value, out var au) ? au : string.Empty;
                    }
                }
                else
                {
                    qUrl = string.IsNullOrWhiteSpace(q.Question) ? string.Empty : ($"/images/{q.Question}");
                    foreach (var kv in q.Answers ?? new Dictionary<string, string>())
                    {
                        if (!string.IsNullOrWhiteSpace(kv.Value))
                            answerUrls[kv.Key] = $"/images/{kv.Value}";
                    }
                }

                Items.Add(new ResultItem
                {
                    QuestionUrl = qUrl,
                    CorrectUrl = answerUrls.TryGetValue(correctKey, out var cu) ? cu : string.Empty,
                    SelectedUrl = (a != null && !string.IsNullOrWhiteSpace(a.SelectedKey) && answerUrls.ContainsKey(a.SelectedKey)) ? answerUrls[a.SelectedKey] : string.Empty,
                    IsCorrect = a?.IsCorrect == true
                });
            }
        }

        private TestState GetState()
        {
            try
            {
                var json = HttpContext.Session.GetString(SessionKey);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonConvert.DeserializeObject<TestState>(json);
            }
            catch { return null; }
        }
    }
}


