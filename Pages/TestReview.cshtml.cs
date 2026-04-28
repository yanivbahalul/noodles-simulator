using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NoodlesSimulator.Services;
using NoodlesSimulator.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages
{
    public class TestReviewModel : PageModel
    {
        private readonly SupabaseStorageService _storage;
        private readonly TestSessionService _testSession;

        public TestReviewModel(SupabaseStorageService storage = null, TestSessionService testSession = null)
        {
            _storage = storage;
            _testSession = testSession;
        }

        public string QuestionImageUrl { get; set; }
        public Dictionary<string, string> AnswerImageUrls { get; set; } = new Dictionary<string, string>();
        public string SelectedKey { get; set; }
        public string ReviewToken { get; set; } = string.Empty;

        public async Task<IActionResult> OnGet()
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToPage("/Login");
            }

            int i = 0;
            int.TryParse(Request.Query["i"], out i);
            ReviewToken = Request.Query["token"].ToString();
            var state = await GetStateAsync(username);
            if (state == null || state.Questions == null || i < 0 || i >= state.Questions.Count)
            {
                return RedirectToPage("/MyExams");
            }

            var q = state.Questions[i];
            var a = (state.Answers != null && i < state.Answers.Count) ? state.Answers[i] : null;
            SelectedKey = a?.SelectedKey;

            var resolved = await ImageUrlResolver.ResolveQuestionAndAnswersAsync(_storage, q.Question, q.Answers);
            QuestionImageUrl = resolved.QuestionUrl;
            AnswerImageUrls = resolved.AnswerUrls;
            return Page();
        }

        private async Task<TestState> GetStateAsync(string username)
        {
            if (_testSession == null)
            {
                return null;
            }

            var token = Request.Query["token"].ToString();
            if (!string.IsNullOrWhiteSpace(token))
            {
                var session = await _testSession.GetSession(token);
                if (session != null && session.Username == username)
                {
                    return new TestState
                    {
                        StartedUtc = session.StartedUtc,
                        CurrentIndex = session.CurrentIndex,
                        Questions = JsonConvert.DeserializeObject<List<TestQuestion>>(session.QuestionsJson) ?? new List<TestQuestion>(),
                        Answers = JsonConvert.DeserializeObject<List<TestAnswer>>(session.AnswersJson) ?? new List<TestAnswer>()
                    };
                }
            }

            return null;
        }
    }
}


