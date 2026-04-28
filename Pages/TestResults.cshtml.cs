using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
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
        public string ReviewToken { get; set; } = string.Empty;

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

            var session = await _testSession.GetSession(token);
            if (session == null || session.Username != username)
            {
                return RedirectToPage("/MyExams");
            }

            ReviewToken = token;
            await LoadFromTestSession(session);
            return Page();
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
}


