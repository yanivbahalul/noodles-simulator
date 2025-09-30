using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NoodlesSimulator.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages
{
    [IgnoreAntiforgeryToken]
    public class TestModel : PageModel
    {
        private const string SessionKey = "TestStateV1";
        private const int TotalQuestions = 17;
        private static readonly TimeSpan TestDuration = TimeSpan.FromHours(2);

        private readonly SupabaseStorageService _storage;

        public TestModel(SupabaseStorageService storage = null)
        {
            _storage = storage;
        }

        public bool AnswerChecked { get; set; }
        public bool IsCorrect { get; set; }
        public string SelectedAnswer { get; set; }
        public string QuestionImageUrl { get; set; }
        public Dictionary<string, string> ShuffledAnswers { get; set; }
        public Dictionary<string, string> AnswerImageUrls { get; set; }
        public int CurrentIndex { get; set; }
        public int DisplayQuestionNumber => CurrentIndex + 1;
        public string TestEndUtcString { get; set; }

        public class TestQuestion
        {
            public string Question { get; set; }
            public Dictionary<string, string> Answers { get; set; }
        }

        public class TestAnswer
        {
            public string SelectedKey { get; set; }
            public bool IsCorrect { get; set; }
        }

        public class TestState
        {
            public DateTime StartedUtc { get; set; }
            public List<TestQuestion> Questions { get; set; } = new List<TestQuestion>();
            public List<TestAnswer> Answers { get; set; } = new List<TestAnswer>();
            public int CurrentIndex { get; set; }
        }

        public async Task<IActionResult> OnGet()
        {
            var start = Request.Query["start"].ToString();
            var advance = Request.Query["advance"].ToString();

            var state = GetState();

            if (!string.IsNullOrEmpty(start) || state == null)
            {
                state = await BuildNewStateAsync();
                SaveState(state);
            }
            else if (!string.IsNullOrEmpty(advance))
            {
                // move forward only if the current was checked or answered
                if (state.CurrentIndex < state.Answers.Count && state.Answers[state.CurrentIndex] != null)
                {
                    state.CurrentIndex = Math.Min(state.CurrentIndex + 1, state.Questions.Count);
                    SaveState(state);
                }
            }

            if (IsExpired(state) || state.CurrentIndex >= state.Questions.Count)
                return RedirectToPage("/TestResults");

            await BindCurrentAsync(state);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var state = GetState();
            if (state == null)
                return RedirectToPage("/Test", new { start = 1 });

            if (IsExpired(state) || state.CurrentIndex >= state.Questions.Count)
                return RedirectToPage("/TestResults");

            var selected = Request.Form["answer"].ToString();
            var idxStr = Request.Form["questionIndex"].ToString();
            int idx = state.CurrentIndex;
            int.TryParse(idxStr, out idx);
            idx = Math.Clamp(idx, 0, state.Questions.Count - 1);

            var q = state.Questions[idx];
            var isCorrect = selected == "correct";

            // record answer only once
            if (state.Answers.Count <= idx)
            {
                while (state.Answers.Count < idx)
                    state.Answers.Add(new TestAnswer());
                state.Answers.Add(new TestAnswer { SelectedKey = selected, IsCorrect = isCorrect });
            }

            SaveState(state);

            AnswerChecked = true;
            IsCorrect = isCorrect;
            SelectedAnswer = selected;
            await BindCurrentAsync(state);
            return Page();
        }

        private bool IsExpired(TestState state)
        {
            if (state == null) return true;
            var end = state.StartedUtc.Add(TestDuration);
            return DateTime.UtcNow >= end;
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

        private void SaveState(TestState state)
        {
            try
            {
                HttpContext.Session.SetString(SessionKey, JsonConvert.SerializeObject(state));
            }
            catch { }
        }

        private async Task<TestState> BuildNewStateAsync()
        {
            var state = new TestState
            {
                StartedUtc = DateTime.UtcNow,
                Questions = new List<TestQuestion>(),
                Answers = new List<TestAnswer>(),
                CurrentIndex = 0
            };

            // Build the source question list similarly to IndexModel
            var all = await LoadAllQuestionGroupsAsync();
            FisherYatesShuffle(all);
            foreach (var g in all.Take(TotalQuestions))
            {
                var correct = g[1];
                var wrong = g.Skip(2).Take(3).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                var answers = new List<(string key, string img)>
                {
                    ("correct", correct)
                };
                if (wrong.Count > 0) answers.Add(("a", wrong[0]));
                if (wrong.Count > 1) answers.Add(("b", wrong[1]));
                if (wrong.Count > 2) answers.Add(("c", wrong[2]));
                answers = answers.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToList();

                state.Questions.Add(new TestQuestion
                {
                    Question = g[0],
                    Answers = answers.ToDictionary(x => x.key, x => x.img)
                });
            }

            return state;
        }

        private async Task<List<List<string>>> LoadAllQuestionGroupsAsync()
        {
            List<string> filtered;
            if (_storage != null)
            {
                var allImages = await _storage.ListFilesAsync("");
                filtered = allImages
                    .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".webp"))
                    .OrderBy(name => name)
                    .ToList();
            }
            else
            {
                var imagesDir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "images");
                if (!System.IO.Directory.Exists(imagesDir))
                    return new List<List<string>>();

                filtered = System.IO.Directory.GetFiles(imagesDir)
                    .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".webp"))
                    .Select(System.IO.Path.GetFileName)
                    .OrderBy(name => name)
                    .ToList();
            }

            var grouped = new List<List<string>>();
            for (int i = 0; i + 4 < filtered.Count; i += 5)
                grouped.Add(filtered.GetRange(i, 5));
            return grouped;
        }

        private async Task BindCurrentAsync(TestState state)
        {
            CurrentIndex = Math.Clamp(state.CurrentIndex, 0, Math.Max(0, state.Questions.Count - 1));
            var q = state.Questions[CurrentIndex];

            ShuffledAnswers = q.Answers;
            var answers = q.Answers ?? new Dictionary<string, string>();

            if (_storage != null)
            {
                // Signed URLs
                var paths = new List<string> { q.Question };
                paths.AddRange(answers.Values.Where(v => !string.IsNullOrWhiteSpace(v)));
                var signed = await _storage.GetSignedUrlsAsync(paths);
                QuestionImageUrl = signed.TryGetValue(q.Question, out var qu) ? qu : string.Empty;
                AnswerImageUrls = new Dictionary<string, string>();
                foreach (var kv in answers)
                {
                    if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                    AnswerImageUrls[kv.Key] = signed.TryGetValue(kv.Value, out var au) ? au : string.Empty;
                }
            }
            else
            {
                QuestionImageUrl = string.IsNullOrWhiteSpace(q.Question) ? string.Empty : ($"/images/{q.Question}");
                AnswerImageUrls = new Dictionary<string, string>();
                foreach (var kv in answers)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Value))
                        AnswerImageUrls[kv.Key] = $"/images/{kv.Value}";
                }
            }

            var end = state.StartedUtc.Add(TestDuration);
            TestEndUtcString = end.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        private static void FisherYatesShuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}


