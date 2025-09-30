using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NoodlesSimulator.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages
{
    public class TestReviewModel : PageModel
    {
        private const string SessionKey = "TestStateV1";
        private readonly SupabaseStorageService _storage;

        public TestReviewModel(SupabaseStorageService storage = null)
        {
            _storage = storage;
        }

        public string QuestionImageUrl { get; set; }
        public Dictionary<string, string> AnswerImageUrls { get; set; } = new Dictionary<string, string>();
        public string SelectedKey { get; set; }

        public class TestQuestion { public string Question { get; set; } public Dictionary<string, string> Answers { get; set; } }
        public class TestAnswer { public string SelectedKey { get; set; } public bool IsCorrect { get; set; } }
        public class TestState { public List<TestQuestion> Questions { get; set; } public List<TestAnswer> Answers { get; set; } }

        public async Task OnGet()
        {
            int i = 0;
            int.TryParse(Request.Query["i"], out i);
            var state = GetState();
            if (state == null || state.Questions == null || i < 0 || i >= state.Questions.Count)
                return;

            var q = state.Questions[i];
            var a = (state.Answers != null && i < state.Answers.Count) ? state.Answers[i] : null;
            SelectedKey = a?.SelectedKey;

            if (_storage != null)
            {
                var paths = new List<string> { q.Question };
                paths.AddRange(q.Answers?.Values ?? new List<string>());
                var signed = await _storage.GetSignedUrlsAsync(paths);
                QuestionImageUrl = signed.TryGetValue(q.Question, out var qu) ? qu : string.Empty;
                foreach (var kv in q.Answers ?? new Dictionary<string, string>())
                {
                    if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                    AnswerImageUrls[kv.Key] = signed.TryGetValue(kv.Value, out var au) ? au : string.Empty;
                }
            }
            else
            {
                QuestionImageUrl = string.IsNullOrWhiteSpace(q.Question) ? string.Empty : ($"/images/{q.Question}");
                foreach (var kv in q.Answers ?? new Dictionary<string, string>())
                {
                    if (!string.IsNullOrWhiteSpace(kv.Value))
                        AnswerImageUrls[kv.Key] = $"/images/{kv.Value}";
                }
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


