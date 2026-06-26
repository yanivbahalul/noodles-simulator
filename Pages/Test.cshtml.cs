using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text;
using System.Text.Json;
using NoodlesSimulator.Models;
using NoodlesSimulator.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages;

public class TestModel : PageModel
{
        private const int TotalQuestions = 17;
        private static readonly TimeSpan TestDuration = TimeSpan.FromHours(2);

        private readonly SupabaseStorageService _storage;
        private readonly QuestionStatsService _stats;
        private readonly TestSessionService _testSession;
        private readonly QuestionDifficultyService _difficultyService;
        private readonly ActivityEventService _activityEvents;

        public TestModel(SupabaseStorageService storage = null, QuestionStatsService stats = null, TestSessionService testSession = null, QuestionDifficultyService difficultyService = null, ActivityEventService activityEvents = null)
        {
            _storage = storage;
            _stats = stats;
            _testSession = testSession;
            _difficultyService = difficultyService;
            _activityEvents = activityEvents;
        }

        public bool AnswerChecked { get; set; }
        public bool IsCorrect { get; set; }
        public string SelectedAnswer { get; set; }
        public string QuestionImageUrl { get; set; }
        public Dictionary<string, string> ShuffledAnswers { get; set; }
        public Dictionary<string, string> AnswerImageUrls { get; set; }
        public int CurrentIndex { get; set; }
        public int DisplayQuestionNumber => CurrentIndex + 1;
        public int QuestionCount => TotalQuestions;
        public int ProgressPercent => TotalQuestions == 0 ? 0 : CurrentIndex * 100 / TotalQuestions;
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
                session = await _testSession.GetSessionAsync(token);
                
                if (session?.Username != username)
                {
                    return RedirectToPage("/MyExams");
                }
            }

            if (session == null)
            {
                session = await _testSession.GetActiveSessionAsync(username);
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
                
                var questionsJson = JsonSerializer.Serialize(state.Questions, AppJson.Options);
                Console.WriteLine("[Test OnGet] Attempting to create session in database...");
                
                session = await _testSession.CreateSessionAsync(username, questionsJson);
                
                if (session == null)
                {
                    return StatusCode(500, "Failed to create test session.");
                }

                _activityEvents?.Log(username, "exam_start", new Dictionary<string, object>
                {
                    ["token"] = session.Token
                });

                Console.WriteLine($"[Test OnGet] Session created successfully! Token: {session.Token}");
                Console.WriteLine($"[Test OnGet] Redirecting to /Test?token={session.Token}");

                return RedirectToPage("/Test", new { token = session.Token });
            }

            if (_testSession.IsExpired(session) || session.Status != "active")
            {
                if (session.Status == "active")
                {
                    await _testSession.UpdateSessionStatusAsync(session.Token, "expired");
                }
                return RedirectToPage("/TestResults", new { token = session.Token });
            }

            var testState = new TestState
            {
                StartedUtc = session.StartedUtc,
                Questions = JsonSerializer.Deserialize<List<TestQuestion>>(session.QuestionsJson, AppJson.Options) ?? new List<TestQuestion>(),
                Answers = JsonSerializer.Deserialize<List<TestAnswer>>(session.AnswersJson, AppJson.Options) ?? new List<TestAnswer>(),
                CurrentIndex = session.CurrentIndex
            };

            if (testState.CurrentIndex >= testState.Questions.Count)
            {
                await _testSession.UpdateSessionStatusAsync(session.Token, "completed");
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
            var selected = Request.Form["answer"].ToString();
            var result = await ProcessTestAnswerAsync(username, token, selected);
            if (result.ErrorResult != null)
                return result.ErrorResult;
            if (result.RedirectResult != null)
                return result.RedirectResult;
            if (result.RedirectPath != null)
                return Redirect(result.RedirectPath);

            return RedirectToPage("/Test", new { token });
        }

        public async Task<IActionResult> OnPostSubmitAnswerAsync()
        {
            try
            {
                var username = HttpContext.Session.GetString("Username");
                if (string.IsNullOrEmpty(username))
                    return new JsonResult(new { error = "Unauthorized", redirect = "/Login" }) { StatusCode = 401 };

                string body;
                using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                    body = await reader.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(body))
                    return new JsonResult(new { error = "Empty body" }) { StatusCode = 400 };

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (!root.TryGetProperty("token", out var tokenEl) || !root.TryGetProperty("answer", out var answerEl))
                    return new JsonResult(new { error = "Invalid body" }) { StatusCode = 400 };

                var token = tokenEl.GetString();
                var selected = answerEl.GetString();
                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(selected))
                    return new JsonResult(new { error = "Invalid body" }) { StatusCode = 400 };

                var result = await ProcessTestAnswerAsync(username, token, selected);
                if (result.ErrorResult != null)
                    return result.ErrorResult;
                if (result.RedirectPath != null)
                    return new JsonResult(new { redirect = result.RedirectPath });

                await BindCurrentAsync(result.State);
                return new JsonResult(BuildTestQuestionResponse(result.Session));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OnPostSubmitAnswerAsync Error] {ex}");
                return new JsonResult(new { error = "Server error" }) { StatusCode = 500 };
            }
        }

        private sealed class TestAnswerProcessResult
        {
            public IActionResult ErrorResult { get; init; }
            public IActionResult RedirectResult { get; init; }
            public string RedirectPath { get; init; }
            public TestSession Session { get; init; }
            public TestState State { get; init; }
        }

        private async Task<TestAnswerProcessResult> ProcessTestAnswerAsync(string username, string token, string selected)
        {
            if (_testSession == null)
            {
                return new TestAnswerProcessResult
                {
                    ErrorResult = StatusCode(503, "Test session service is not available.")
                };
            }
            if (string.IsNullOrEmpty(token))
            {
                return new TestAnswerProcessResult
                {
                    ErrorResult = BadRequest("Missing test token.")
                };
            }

            var session = await _testSession.GetSessionAsync(token);
            if (session == null || session.Username != username)
            {
                return new TestAnswerProcessResult
                {
                    RedirectResult = RedirectToPage("/MyExams")
                };
            }

            if (_testSession.IsExpired(session) || session.Status != "active")
            {
                await _testSession.UpdateSessionStatusAsync(session.Token, "expired");
                return new TestAnswerProcessResult
                {
                    RedirectPath = $"/TestResults?token={Uri.EscapeDataString(session.Token)}"
                };
            }

            var questions = JsonSerializer.Deserialize<List<TestQuestion>>(session.QuestionsJson, AppJson.Options) ?? new List<TestQuestion>();
            var answers = JsonSerializer.Deserialize<List<TestAnswer>>(session.AnswersJson, AppJson.Options) ?? new List<TestAnswer>();
            var idx = session.CurrentIndex;

            if (idx < 0 || idx >= questions.Count)
            {
                return new TestAnswerProcessResult
                {
                    RedirectPath = $"/TestResults?token={Uri.EscapeDataString(session.Token)}"
                };
            }

            var q = questions[idx];
            var isCorrect = AnswerOptionShuffle.IsSelectedCorrect(q, selected);

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
                _stats?.Record(qid, isCorrect);

                if (!string.IsNullOrEmpty(qid) && _difficultyService != null)
                {
                    _ = _difficultyService.UpdateQuestionStatsAsync(qid, isCorrect);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Test ProcessTestAnswer Stats Update Error] {ex.Message}");
            }

            session.CurrentIndex = Math.Min(idx + 1, questions.Count);
            session.AnswersJson = JsonSerializer.Serialize(answers, AppJson.Options);
            session.Score = answers.Count(a => a != null && a.IsCorrect) * 6;
            session.MaxScore = questions.Count * 6;

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
                StartedUtc = session.StartedUtc,
                Questions = questions,
                Answers = answers,
                CurrentIndex = session.CurrentIndex
            };

            return new TestAnswerProcessResult
            {
                Session = session,
                State = state
            };
        }

        private object BuildTestQuestionResponse(TestSession session)
        {
            return new
            {
                questionImageUrl = QuestionImageUrl,
                displayQuestionNumber = DisplayQuestionNumber,
                totalQuestions = TotalQuestions,
                progressPercent = ProgressPercent,
                score = session.Score,
                maxScore = session.MaxScore,
                answers = (ShuffledAnswers ?? new Dictionary<string, string>())
                    .Select(kv => new
                    {
                        key = kv.Key,
                        imageUrl = AnswerImageUrls?.TryGetValue(kv.Key, out var url) == true ? url : ""
                    })
                    .ToList(),
                redirect = (string)null
            };
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
            
            if (_testSession == null || string.IsNullOrEmpty(token))
            {
                Console.WriteLine("[Test OnPostEndTest] TestSessionService null or token empty");
                return RedirectToPage("/TestResults");
            }

            var session = await _testSession.GetSessionAsync(token);
            if (session?.Username != username)
            {
                Console.WriteLine("[Test OnPostEndTest] Session not found or username mismatch");
                return RedirectToPage("/TestResults");
            }

            Console.WriteLine($"[Test OnPostEndTest] Session found. Current status: {session.Status}");

            var questions = JsonSerializer.Deserialize<List<TestQuestion>>(session.QuestionsJson, AppJson.Options) ?? new List<TestQuestion>();
            var answers = JsonSerializer.Deserialize<List<TestAnswer>>(session.AnswersJson, AppJson.Options) ?? new List<TestAnswer>();

            session.Status = "completed";
            session.CompletedUtc = DateTime.UtcNow;
            session.Score = answers.Count(a => a != null && a.IsCorrect) * 6;
            session.MaxScore = questions.Count * 6;

            Console.WriteLine($"[Test OnPostEndTest] Updating session - Score: {session.Score}/{session.MaxScore}, Status: completed");
            await _testSession.UpdateSessionAsync(session);
            LogExamComplete(session.Username, session.Score, session.MaxScore);

            Console.WriteLine("[Test OnPostEndTest] Test ended successfully. Redirecting to results...");

            return RedirectToPage("/TestResults", new { token });
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
                    if (allowedQuestions.Count > 0)
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
                    var questions = await _difficultyService.GetQuestionsByDifficultyAsync(difficulty);
                    
                        if (questions.Count > 0)
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
                var difficultyData = JsonSerializer.Deserialize<DifficultyConfig>(json, AppJson.Options);
                
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

        private void LogExamComplete(string username, int score, int maxScore)
        {
            _activityEvents?.Log(username, "exam_complete", new Dictionary<string, object>
            {
                ["score"] = score,
                ["maxScore"] = maxScore
            });
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
