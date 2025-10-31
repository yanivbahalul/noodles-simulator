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
    [IgnoreAntiforgeryToken]
    public class TestModel : PageModel
    {
        private const int TotalQuestions = 17;
        private static readonly TimeSpan TestDuration = TimeSpan.FromHours(2);

        private readonly SupabaseStorageService _storage;
        private readonly QuestionStatsService _stats;
        private readonly TestSessionService _testSession;
        private readonly EmailService _email;

        public TestModel(SupabaseStorageService storage = null, QuestionStatsService stats = null, TestSessionService testSession = null, EmailService email = null)
        {
            _storage = storage;
            _stats = stats;
            _testSession = testSession;
            _email = email;
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
            // Check if user is logged in
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToPage("/Login");
            }

            Console.WriteLine($"[Test OnGet] User: {username}, TestSessionService available: {_testSession != null}");

            if (_testSession == null)
            {
                Console.WriteLine($"[Test OnGet] ⚠️ TestSessionService is NULL - using legacy session-based system");
                // Fallback to old session-based system if service not available
                return await OnGetLegacy();
            }

            var token = Request.Query["token"].ToString();
            var start = Request.Query["start"].ToString();

            TestSession session = null;

            // Try to get session by token from URL
            if (!string.IsNullOrEmpty(token))
            {
                session = await _testSession.GetSession(token);
                
                // Verify session belongs to current user
                if (session != null && session.Username != username)
                {
                    return RedirectToPage("/MyExams");
                }
            }

            // If no token or session not found, check for active session
            if (session == null)
            {
                session = await _testSession.GetActiveSession(username);
            }

            // If user wants to start new test but has active session, redirect to it with alert
            if (!string.IsNullOrEmpty(start) && session != null && session.Status == "active")
            {
                TempData["ActiveTestAlert"] = "קיים מבחן פעיל! עליך לסיים אותו על מנת להתחיל מבחן חדש.";
                return RedirectToPage("/Test", new { token = session.Token });
            }

            // Create new session if explicitly starting or no active session exists
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
                    Console.WriteLine($"[Test OnGet] ⚠️ CreateSession returned NULL - falling back to legacy session-based system");
                    // Fallback to session-based if database fails
                    return await OnGetLegacy();
                }

                Console.WriteLine($"[Test OnGet] ✅ Session created successfully! Token: {session.Token}");
                Console.WriteLine($"[Test OnGet] Redirecting to /Test?token={session.Token}");

                // Email notification disabled per user request
                // SendTestStartedEmail(username, session.Token);

                return RedirectToPage("/Test", new { token = session.Token });
            }

            // Check if session is expired or completed
            if (_testSession.IsExpired(session) || session.Status != "active")
            {
                if (session.Status == "active")
                {
                    await _testSession.UpdateSessionStatus(session.Token, "expired");
                }
                return RedirectToPage("/TestResults", new { token = session.Token });
            }

            // Load state from session
            var testState = new TestState
            {
                StartedUtc = session.StartedUtc,
                Questions = JsonConvert.DeserializeObject<List<TestQuestion>>(session.QuestionsJson) ?? new List<TestQuestion>(),
                Answers = JsonConvert.DeserializeObject<List<TestAnswer>>(session.AnswersJson) ?? new List<TestAnswer>(),
                CurrentIndex = session.CurrentIndex
            };

            // Check if all questions answered
            if (testState.CurrentIndex >= testState.Questions.Count)
            {
                await _testSession.UpdateSessionStatus(session.Token, "completed");
                return RedirectToPage("/TestResults", new { token = session.Token });
            }

            await BindCurrentAsync(testState);
            ViewData["Token"] = session.Token;
            return Page();
        }

        private async Task<IActionResult> OnGetLegacy()
        {
            var start = Request.Query["start"].ToString();
            var advance = Request.Query["advance"].ToString();
            var difficulty = Request.Query["difficulty"].ToString();

            var state = GetState();

            if (!string.IsNullOrEmpty(start) || state == null)
            {
                state = await BuildNewStateAsync(difficulty);
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

        public async Task<IActionResult> OnPost()
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToPage("/Login");
            }

            var token = Request.Form["token"].ToString();
            
            if (_testSession == null || string.IsNullOrEmpty(token))
            {
                // Fallback to legacy system
                return OnPostLegacy();
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

            // Load current state
            var questions = JsonConvert.DeserializeObject<List<TestQuestion>>(session.QuestionsJson) ?? new List<TestQuestion>();
            var answers = JsonConvert.DeserializeObject<List<TestAnswer>>(session.AnswersJson) ?? new List<TestAnswer>();

            var selected = Request.Form["answer"].ToString();
            var idxStr = Request.Form["questionIndex"].ToString();
            int idx = session.CurrentIndex;
            int.TryParse(idxStr, out idx);
            idx = Math.Clamp(idx, 0, questions.Count - 1);

            var isCorrect = selected == "correct";

            // record answer only once
            if (answers.Count <= idx)
            {
                while (answers.Count < idx)
                    answers.Add(new TestAnswer());
                answers.Add(new TestAnswer { SelectedKey = selected, IsCorrect = isCorrect });
            }

            // record stats
            try { var qid = (idx >= 0 && idx < questions.Count) ? questions[idx].Question : null; _stats?.Record(qid, isCorrect); } catch { }

            // advance to next question
            session.CurrentIndex = Math.Min(idx + 1, questions.Count);
            session.AnswersJson = JsonConvert.SerializeObject(answers);
            session.Score = answers.Count(a => a != null && a.IsCorrect) * 6;
            session.MaxScore = questions.Count * 6;

            // Update session in database
            await _testSession.UpdateSession(session);

            if (_testSession.IsExpired(session) || session.CurrentIndex >= questions.Count)
            {
                if (session.Status == "active")
                {
                    await _testSession.UpdateSessionStatus(session.Token, "completed");
                    
                    // Email notification disabled per user request
                    // SendTestCompletedEmail(username, session.Token, session.Score, session.MaxScore);
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
                    
                    // Calculate final score
                    var questions = JsonConvert.DeserializeObject<List<TestQuestion>>(session.QuestionsJson) ?? new List<TestQuestion>();
                    var answers = JsonConvert.DeserializeObject<List<TestAnswer>>(session.AnswersJson) ?? new List<TestAnswer>();
                    
                    session.Status = "completed";
                    session.CompletedUtc = DateTime.UtcNow;
                    session.Score = answers.Count(a => a != null && a.IsCorrect) * 6;
                    session.MaxScore = questions.Count * 6;
                    
                    Console.WriteLine($"[Test OnPostEndTest] Updating session - Score: {session.Score}/{session.MaxScore}, Status: completed");
                    await _testSession.UpdateSession(session);
                    
                    Console.WriteLine($"[Test OnPostEndTest] ✅ Test ended successfully. Redirecting to results...");
                    
                    return RedirectToPage("/TestResults", new { token = token });
                }
                else
                {
                    Console.WriteLine($"[Test OnPostEndTest] ⚠️ Session not found or username mismatch");
                }
            }
            else
            {
                Console.WriteLine($"[Test OnPostEndTest] ⚠️ TestSessionService null or token empty");
            }
            
            // Fallback to legacy
            return RedirectToPage("/TestResults");
        }

        private IActionResult OnPostLegacy()
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

            // record stats
            try { var qid = (idx >= 0 && idx < state.Questions.Count) ? state.Questions[idx].Question : null; _stats?.Record(qid, isCorrect); } catch { }

            // advance to next question without revealing correctness
            state.CurrentIndex = Math.Min(idx + 1, state.Questions.Count);
            SaveState(state);

            if (IsExpired(state) || state.CurrentIndex >= state.Questions.Count)
                return RedirectToPage("/TestResults");

            return RedirectToPage("/Test");
        }

        private bool IsExpired(TestState state)
        {
            if (state == null) return true;
            var end = state.StartedUtc.Add(TestDuration);
            return DateTime.UtcNow >= end;
        }

        private const string SessionKey = "TestStateV1";

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

        private async Task<TestState> BuildNewStateAsync(string difficulty = null)
        {
            var state = new TestState
            {
                StartedUtc = DateTime.UtcNow,
                Questions = new List<TestQuestion>(),
                Answers = new List<TestAnswer>(),
                CurrentIndex = 0
            };

            // Build the source question list similarly to IndexModel
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
            // Load ALL images from Supabase/local (sorted)
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

            // If difficulty specified, only use questions from that difficulty list
            if (!string.IsNullOrEmpty(difficulty))
            {
                var allowedQuestions = await LoadDifficultyQuestionsAsync(difficulty);
                if (allowedQuestions != null && allowedQuestions.Any())
                {
                    Console.WriteLine($"[Test] Filtering by difficulty '{difficulty}': {allowedQuestions.Count} questions available");
                    
                    // For each question in the difficulty list, find it in allImages and take it + next 4
                    foreach (var questionFile in allowedQuestions)
                    {
                        if (string.IsNullOrWhiteSpace(questionFile))
                            continue;
                        
                        // Search for exact match (filenames include spaces as-is)
                        int idx = allImages.IndexOf(questionFile);
                        
                        if (idx >= 0 && idx + 4 < allImages.Count)
                        {
                            // Take the question image + 4 consecutive images (answers)
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
                            Console.WriteLine($"[Test] Debug: Looking for exact match. First 5 images in storage: {string.Join(", ", allImages.Take(5))}");
                        }
                    }
                    
                    Console.WriteLine($"[Test] Created {grouped.Count} question groups from difficulty '{difficulty}'");
                    return grouped;
                }
            }

            // No difficulty filter - take all questions in groups of 5
            for (int i = 0; i + 4 < allImages.Count; i += 5)
                grouped.Add(allImages.GetRange(i, 5));
            
            return grouped;
        }

        private async Task<List<string>> LoadDifficultyQuestionsAsync(string difficulty)
        {
            try
            {
                var difficultyFile = $"wwwroot/difficulty/{difficulty}.json";
                var fullPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), difficultyFile);
                
                if (!System.IO.File.Exists(fullPath))
                {
                    Console.WriteLine($"[Test] Difficulty file not found: {fullPath}");
                    return null;
                }

                var json = await System.IO.File.ReadAllTextAsync(fullPath);
                var difficultyData = JsonConvert.DeserializeObject<DifficultyConfig>(json);
                
                Console.WriteLine($"[Test] Loaded {difficultyData?.Questions?.Count ?? 0} questions for difficulty '{difficulty}'");
                return difficultyData?.Questions ?? new List<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Test] Error loading difficulty file: {ex.Message}");
                return null;
            }
        }

        public class DifficultyConfig
        {
            public string Difficulty { get; set; }
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public List<string> Questions { get; set; }
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

        private void SendTestStartedEmail(string username, string token)
        {
            if (_email == null || !_email.IsConfigured)
            {
                Console.WriteLine("[Test] Email service not configured, skipping test started notification");
                return;
            }

            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var testUrl = $"{baseUrl}/Test?token={token}";
                
                var subject = $"🎓 מבחן חדש התחיל - {username}";
                var body = $@"
                    <html dir='rtl'>
                    <body style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px; direction: rtl;'>
                        <div style='max-width: 600px; margin: 0 auto; background: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); direction: rtl;'>
                            <h1 style='color: #4caf50; text-align: center; direction: rtl; unicode-bidi: embed;'>🎓 מבחן חדש התחיל!</h1>
                            <p style='font-size: 16px; line-height: 1.6; direction: rtl; text-align: right; unicode-bidi: embed;'>
                                המשתמש <strong>{username}</strong> התחיל מבחן חדש במערכת Noodles Simulator.
                            </p>
                            <div style='background: #f9f9f9; padding: 15px; border-right: 4px solid #4caf50; margin: 20px 0; direction: rtl; text-align: right;'>
                                <p style='direction: rtl; unicode-bidi: embed;'><strong>פרטי המבחן:</strong></p>
                                <ul style='list-style: none; padding: 0; direction: rtl; text-align: right;'>
                                    <li style='unicode-bidi: embed;'>👤 משתמש: <strong>{username}</strong></li>
                                    <li style='unicode-bidi: embed;'>🕐 התחיל: <strong>{DateTime.Now:dd/MM/yyyy HH:mm}</strong></li>
                                    <li style='unicode-bidi: embed;'>🔑 טוקן: <code style='background: #eee; padding: 2px 6px; border-radius: 3px;'>{token.Substring(0, Math.Min(16, token.Length))}...</code></li>
                                    <li style='unicode-bidi: embed;'>⏱️ זמן זמין: <strong>2 שעות</strong></li>
                                    <li style='unicode-bidi: embed;'>📝 מספר שאלות: <strong>{TotalQuestions}</strong></li>
                                </ul>
                            </div>
                            <p style='text-align: center; margin-top: 30px;'>
                                <a href='{testUrl}' style='display: inline-block; padding: 12px 30px; background: #4caf50; color: white; text-decoration: none; border-radius: 5px; font-weight: bold; unicode-bidi: embed;'>
                                    צפה במבחן
                                </a>
                            </p>
                            <hr style='margin: 30px 0; border: none; border-top: 1px solid #eee;'>
                            <p style='font-size: 12px; color: #999; text-align: center; direction: rtl; unicode-bidi: embed;'>
                                זוהי התראה אוטומטית ממערכת Noodles Simulator<br>
                                ניתן להמשיך את המבחן מכל מכשיר באמצעות הקישור לעיל
                            </p>
                        </div>
                    </body>
                    </html>
                ";

                var sent = _email.Send(subject, body);
                if (sent)
                {
                    Console.WriteLine($"[Test] ✅ Test started email sent for user {username}");
                }
                else
                {
                    Console.WriteLine($"[Test] ❌ Failed to send test started email for user {username}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Test] ❌ Error sending test started email: {ex.Message}");
            }
        }

        private void SendTestCompletedEmail(string username, string token, int score, int maxScore)
        {
            if (_email == null || !_email.IsConfigured)
            {
                Console.WriteLine("[Test] Email service not configured, skipping test completed notification");
                return;
            }

            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var resultsUrl = $"{baseUrl}/TestResults?token={token}";
                var percentage = maxScore > 0 ? Math.Round((double)score / maxScore * 100, 1) : 0;
                var gradeEmoji = percentage >= 90 ? "🌟" : percentage >= 80 ? "✨" : percentage >= 70 ? "👍" : percentage >= 60 ? "📚" : "💪";
                
                var subject = $"{gradeEmoji} מבחן הושלם - {username} - ציון: {score}/{maxScore}";
                var body = $@"
                    <html dir='rtl'>
                    <body style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px; direction: rtl;'>
                        <div style='max-width: 600px; margin: 0 auto; background: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); direction: rtl;'>
                            <h1 style='color: #2196f3; text-align: center; direction: rtl; unicode-bidi: embed;'>{gradeEmoji} מבחן הושלם!</h1>
                            <p style='font-size: 16px; line-height: 1.6; direction: rtl; text-align: right; unicode-bidi: embed;'>
                                המשתמש <strong>{username}</strong> סיים מבחן במערכת Noodles Simulator.
                            </p>
                            <div style='background: #f0f8ff; padding: 20px; border-radius: 8px; text-align: center; margin: 20px 0; direction: rtl;'>
                                <h2 style='color: #2196f3; margin: 0 0 15px 0; unicode-bidi: embed;'>תוצאות המבחן</h2>
                                <div style='font-size: 48px; font-weight: bold; color: #4caf50; margin: 15px 0;'>
                                    {score}/{maxScore}
                                </div>
                                <div style='font-size: 24px; color: #666; unicode-bidi: embed;'>
                                    אחוז הצלחה: <strong style='color: #2196f3;'>{percentage}%</strong>
                                </div>
                            </div>
                            <div style='background: #f9f9f9; padding: 15px; border-right: 4px solid #2196f3; margin: 20px 0; direction: rtl; text-align: right;'>
                                <p style='direction: rtl; unicode-bidi: embed;'><strong>פרטי המבחן:</strong></p>
                                <ul style='list-style: none; padding: 0; direction: rtl; text-align: right;'>
                                    <li style='unicode-bidi: embed;'>👤 משתמש: <strong>{username}</strong></li>
                                    <li style='unicode-bidi: embed;'>🕐 הושלם: <strong>{DateTime.Now:dd/MM/yyyy HH:mm}</strong></li>
                                    <li style='unicode-bidi: embed;'>📊 ציון: <strong>{score} מתוך {maxScore}</strong></li>
                                    <li style='unicode-bidi: embed;'>📈 אחוז: <strong>{percentage}%</strong></li>
                                    <li style='unicode-bidi: embed;'>🔑 טוקן: <code style='background: #eee; padding: 2px 6px; border-radius: 3px;'>{token.Substring(0, Math.Min(16, token.Length))}...</code></li>
                                </ul>
                            </div>
                            <p style='text-align: center; margin-top: 30px;'>
                                <a href='{resultsUrl}' style='display: inline-block; padding: 12px 30px; background: #2196f3; color: white; text-decoration: none; border-radius: 5px; font-weight: bold; unicode-bidi: embed;'>
                                    צפה בתוצאות המלאות
                                </a>
                            </p>
                            <hr style='margin: 30px 0; border: none; border-top: 1px solid #eee;'>
                            <p style='font-size: 12px; color: #999; text-align: center; direction: rtl; unicode-bidi: embed;'>
                                זוהי התראה אוטומטית ממערכת Noodles Simulator<br>
                                ניתן לצפות בתוצאות המלאות באמצעות הקישור לעיל
                            </p>
                        </div>
                    </body>
                    </html>
                ";

                var sent = _email.Send(subject, body);
                if (sent)
                {
                    Console.WriteLine($"[Test] ✅ Test completed email sent for user {username} (Score: {score}/{maxScore})");
                }
                else
                {
                    Console.WriteLine($"[Test] ❌ Failed to send test completed email for user {username}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Test] ❌ Error sending test completed email: {ex.Message}");
            }
        }
    }
}


