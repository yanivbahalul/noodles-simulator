using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NoodlesSimulator.Services;
using NoodlesSimulator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages
{
    public class TestModel : PageModel
    {
        private const int TotalQuestions = 17;
        private static readonly TimeSpan TestDuration = TimeSpan.FromHours(2);

        private readonly SupabaseStorageService _storage;
        private readonly QuestionStatsService _stats;
        private readonly TestSessionService _testSession;
        private readonly QuestionDifficultyService _difficultyService;

        public TestModel(SupabaseStorageService storage = null, QuestionStatsService stats = null, TestSessionService testSession = null, QuestionDifficultyService difficultyService = null)
        {
            _storage = storage;
            _stats = stats;
            _testSession = testSession;
            _difficultyService = difficultyService;
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

        public async Task<IActionResult> OnGet()
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToPage("/Login");
            }

            Console.WriteLine($"[Test OnGet] User: {username}, TestSessionService available: {_testSession != null}");

            if (_testSession == null)
            {
                return StatusCode(503, "Test session service is not available.");
            }

            var token = Request.Query["token"].ToString();
            var start = Request.Query["start"].ToString();

            TestSession session = null;

            if (!string.IsNullOrEmpty(token))
            {
                session = await _testSession.GetSession(token);
                
                if (session != null && session.Username != username)
                {
                    return RedirectToPage("/MyExams");
                }
            }

            if (session == null)
            {
                session = await _testSession.GetActiveSession(username);
            }

            if (!string.IsNullOrEmpty(start) && session != null && session.Status == "active")
            {
                TempData["ActiveTestAlert"] = "קיים מבחן פעיל! עליך לסיים אותו על מנת להתחיל מבחן חדש.";
                return RedirectToPage("/Test", new { token = session.Token });
            }

            if (!string.IsNullOrEmpty(start) || session == null)
            {
                var difficulty = Request.Query["difficulty"].ToString();
                Console.WriteLine($"[Test OnGet] Creating new test session for user '{username}' with difficulty '{difficulty}'");
                
                var state = await BuildNewStateAsync(difficulty);
                Console.WriteLine($"[Test OnGet] Built state with {state.Questions.Count} questions");
                
                var questionsJson = JsonConvert.SerializeObject(state.Questions);
                Console.WriteLine($"[Test OnGet] Attempting to create session in database...");
                
                session = await _testSession.CreateSession(username, questionsJson);
                
                if (session == null)
                {
                    return StatusCode(500, "Failed to create test session.");
                }

                Console.WriteLine($"[Test OnGet] Session created successfully! Token: {session.Token}");
                Console.WriteLine($"[Test OnGet] Redirecting to /Test?token={session.Token}");

                return RedirectToPage("/Test", new { token = session.Token });
            }

            if (_testSession.IsExpired(session) || session.Status != "active")
            {
                if (session.Status == "active")
                {
                    await _testSession.UpdateSessionStatus(session.Token, "expired");
                }
                return RedirectToPage("/TestResults", new { token = session.Token });
            }

            var testState = new TestState
            {
                StartedUtc = session.StartedUtc,
                Questions = JsonConvert.DeserializeObject<List<TestQuestion>>(session.QuestionsJson) ?? new List<TestQuestion>(),
                Answers = JsonConvert.DeserializeObject<List<TestAnswer>>(session.AnswersJson) ?? new List<TestAnswer>(),
                CurrentIndex = session.CurrentIndex
            };

            if (testState.CurrentIndex >= testState.Questions.Count)
            {
                await _testSession.UpdateSessionStatus(session.Token, "completed");
                return RedirectToPage("/TestResults", new { token = session.Token });
            }

            await BindCurrentAsync(testState);
            ViewData["Token"] = session.Token;
            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToPage("/Login");
            }

            var token = Request.Form["token"].ToString();
            
            if (_testSession == null)
            {
                return StatusCode(503, "Test session service is not available.");
            }
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest("Missing test token.");
            }

            var session = await _testSession.GetSession(token);
            if (session == null || session.Username != username)
            {
                return RedirectToPage("/MyExams");
            }

            if (_testSession.IsExpired(session) || session.Status != "active")
            {
                await _testSession.UpdateSessionStatus(session.Token, "expired");
                return RedirectToPage("/TestResults", new { token = session.Token });
            }

            var questions = JsonConvert.DeserializeObject<List<TestQuestion>>(session.QuestionsJson) ?? new List<TestQuestion>();
            var answers = JsonConvert.DeserializeObject<List<TestAnswer>>(session.AnswersJson) ?? new List<TestAnswer>();

            var selected = Request.Form["answer"].ToString();
            var idxStr = Request.Form["questionIndex"].ToString();
            int idx = session.CurrentIndex;
            int.TryParse(idxStr, out idx);
            idx = Math.Clamp(idx, 0, questions.Count - 1);

            var isCorrect = selected == "correct";

            if (answers.Count <= idx)
            {
                while (answers.Count < idx)
                    answers.Add(new TestAnswer());
                answers.Add(new TestAnswer { SelectedKey = selected, IsCorrect = isCorrect });
            }

            try 
            { 
                var qid = (idx >= 0 && idx < questions.Count) ? questions[idx].Question : null; 
                _stats?.Record(qid, isCorrect);
                
                if (!string.IsNullOrEmpty(qid) && _difficultyService != null)
                {
                    _ = _difficultyService.UpdateQuestionStats(qid, isCorrect);
                }
            } 
            catch (Exception ex) { Console.WriteLine($"[Test OnPost Stats Update Error] {ex.Message}"); }

            session.CurrentIndex = Math.Min(idx + 1, questions.Count);
            session.AnswersJson = JsonConvert.SerializeObject(answers);
            session.Score = answers.Count(a => a != null && a.IsCorrect) * 6;
            session.MaxScore = questions.Count * 6;

            await _testSession.UpdateSession(session);

            if (_testSession.IsExpired(session) || session.CurrentIndex >= questions.Count)
            {
                if (session.Status == "active")
                {
                    await _testSession.UpdateSessionStatus(session.Token, "completed");
                }
                return RedirectToPage("/TestResults", new { token = session.Token });
            }

            return RedirectToPage("/Test", new { token = session.Token });
        }

        public async Task<IActionResult> OnPostEndTest()
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToPage("/Login");
            }

            var token = Request.Form["token"].ToString();
            Console.WriteLine($"[Test OnPostEndTest] Called with token: {token}");
            
            if (_testSession != null && !string.IsNullOrEmpty(token))
            {
                var session = await _testSession.GetSession(token);
                if (session != null && session.Username == username)
                {
                    Console.WriteLine($"[Test OnPostEndTest] Session found. Current status: {session.Status}");
                    
                    var questions = JsonConvert.DeserializeObject<List<TestQuestion>>(session.QuestionsJson) ?? new List<TestQuestion>();
                    var answers = JsonConvert.DeserializeObject<List<TestAnswer>>(session.AnswersJson) ?? new List<TestAnswer>();
                    
                    session.Status = "completed";
                    session.CompletedUtc = DateTime.UtcNow;
                    session.Score = answers.Count(a => a != null && a.IsCorrect) * 6;
                    session.MaxScore = questions.Count * 6;
                    
                    Console.WriteLine($"[Test OnPostEndTest] Updating session - Score: {session.Score}/{session.MaxScore}, Status: completed");
                    await _testSession.UpdateSession(session);
                    
                    Console.WriteLine($"[Test OnPostEndTest] Test ended successfully. Redirecting to results...");
                    
                    return RedirectToPage("/TestResults", new { token = token });
                }
                else
                {
                    Console.WriteLine($"[Test OnPostEndTest] Session not found or username mismatch");
                }
            }
            else
            {
                Console.WriteLine($"[Test OnPostEndTest] TestSessionService null or token empty");
            }
            
            return RedirectToPage("/TestResults");
        }

        private async Task<TestState> BuildNewStateAsync(string difficulty = null)
        {
            var state = new TestState
            {
                StartedUtc = DateTime.UtcNow,
                Questions = new List<TestQuestion>(),
                Answers = new List<TestAnswer>(),
                CurrentIndex = 0
            };

            var all = await LoadAllQuestionGroupsAsync(difficulty);
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

        private async Task<List<List<string>>> LoadAllQuestionGroupsAsync(string difficulty = null)
        {
            List<string> allImages;
            if (_storage != null)
            {
                var images = await _storage.ListFilesAsync("");
                allImages = images
                    .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".webp"))
                    .OrderBy(name => name)
                    .ToList();
            }
            else
            {
                var imagesDir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "images");
                if (!System.IO.Directory.Exists(imagesDir))
                    return new List<List<string>>();

                allImages = System.IO.Directory.GetFiles(imagesDir)
                    .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".webp"))
                    .Select(System.IO.Path.GetFileName)
                    .OrderBy(name => name)
                    .ToList();
            }

            var grouped = new List<List<string>>();

            if (!string.IsNullOrEmpty(difficulty))
            {
                var allowedQuestions = await LoadDifficultyQuestionsAsync(difficulty);
                if (allowedQuestions != null && allowedQuestions.Any())
                {
                    Console.WriteLine($"[Test] Filtering by difficulty '{difficulty}': {allowedQuestions.Count} questions available");
                    
                    foreach (var questionFile in allowedQuestions)
                    {
                        if (string.IsNullOrWhiteSpace(questionFile))
                            continue;
                        
                        int idx = allImages.IndexOf(questionFile);
                        
                        if (idx < 0)
                        {
                            idx = allImages.FindIndex(img => 
                                string.Equals(img, questionFile, StringComparison.OrdinalIgnoreCase));
                        }
                        
                        if (idx < 0)
                        {
                            var trimmed = questionFile.Trim();
                            idx = allImages.FindIndex(img => 
                                img.Trim().Equals(trimmed, StringComparison.OrdinalIgnoreCase));
                        }
                        
                        if (idx >= 0 && idx + 4 < allImages.Count)
                        {
                            var group = allImages.GetRange(idx, 5);
                            grouped.Add(group);
                        }
                        else if (idx >= 0)
                        {
                            Console.WriteLine($"[Test] Warning: Question '{questionFile}' found at index {idx}, but not enough images after it (need {idx + 5}, have {allImages.Count})");
                        }
                        else
                        {
                            Console.WriteLine($"[Test] Warning: Question '{questionFile}' not found in Supabase storage (total images: {allImages.Count})");
                            if (grouped.Count == 0)
                            {
                                Console.WriteLine($"[Test] Debug: First 10 images in storage: {string.Join(", ", allImages.Take(10))}");
                            }
                        }
                    }
                    
                    Console.WriteLine($"[Test] Created {grouped.Count} question groups from difficulty '{difficulty}'");
                    return grouped;
                }
            }

            for (int i = 0; i + 4 < allImages.Count; i += 5)
                grouped.Add(allImages.GetRange(i, 5));
            
            return grouped;
        }

        private async Task<List<string>> LoadDifficultyQuestionsAsync(string difficulty)
        {
            try
            {
                if (_difficultyService != null)
                {
                    Console.WriteLine($"[Test] Loading difficulty '{difficulty}' from database...");
                    var questions = await _difficultyService.GetQuestionsByDifficulty(difficulty);
                    
                    if (questions != null && questions.Any())
                    {
                        Console.WriteLine($"[Test] Loaded {questions.Count} questions from database for difficulty '{difficulty}'");
                        return questions;
                    }
                    
                    Console.WriteLine($"[Test] No questions in database for difficulty '{difficulty}', falling back to JSON");
                }
                
                var difficultyFile = $"wwwroot/difficulty/{difficulty}.json";
                var fullPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), difficultyFile);
                
                if (!System.IO.File.Exists(fullPath))
                {
                    Console.WriteLine($"[Test] Difficulty file not found: {fullPath}");
                    return null;
                }

                var json = await System.IO.File.ReadAllTextAsync(fullPath);
                var difficultyData = JsonConvert.DeserializeObject<DifficultyConfig>(json);
                
                Console.WriteLine($"[Test] Loaded {difficultyData?.Questions?.Count ?? 0} questions from JSON for difficulty '{difficulty}'");
                return difficultyData?.Questions ?? new List<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Test] Error loading difficulty: {ex.Message}");
                return null;
            }
        }

        private async Task BindCurrentAsync(TestState state)
        {
            CurrentIndex = Math.Clamp(state.CurrentIndex, 0, Math.Max(0, state.Questions.Count - 1));
            var q = state.Questions[CurrentIndex];

            ShuffledAnswers = q.Answers;
            var answers = q.Answers ?? new Dictionary<string, string>();
            var resolved = await ImageUrlResolver.ResolveQuestionAndAnswersAsync(_storage, q.Question, answers);
            QuestionImageUrl = resolved.QuestionUrl;
            AnswerImageUrls = resolved.AnswerUrls;

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


