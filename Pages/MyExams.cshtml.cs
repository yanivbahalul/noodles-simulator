using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Services;
using NoodlesSimulator.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages
{
    public class MyExamsModel : PageModel
    {
        private readonly TestSessionService _testSession;

        public MyExamsModel(TestSessionService testSession = null)
        {
            _testSession = testSession;
        }

        public List<TestSession> Sessions { get; set; } = new List<TestSession>();
        public TestSession ActiveSession { get; set; }
        public bool CanReviewTest { get; set; } = true;

        public async Task<IActionResult> OnGet()
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToPage("/Login");
            }

            if (_testSession == null)
            {
                // Service not available
                Sessions = new List<TestSession>();
                return Page();
            }

            // Get all user sessions
            Sessions = await _testSession.GetUserSessions(username, 50);
            
            // Find active session
            ActiveSession = Sessions.FirstOrDefault(s => s.Status == "active");
            
            // Check if any active sessions are expired
            foreach (var session in Sessions.Where(s => s.Status == "active"))
            {
                if (_testSession.IsExpired(session))
                {
                    await _testSession.UpdateSessionStatus(session.Token, "expired");
                    session.Status = "expired";
                }
            }

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
            
            if (_testSession != null && !string.IsNullOrEmpty(token))
            {
                var session = await _testSession.GetSession(token);
                if (session != null && session.Username == username && session.Status == "active")
                {
                    // Mark test as completed
                    await _testSession.UpdateSessionStatus(token, "completed");
                    
                    // Calculate final score
                    var questions = JsonConvert.DeserializeObject<List<object>>(session.QuestionsJson);
                    var answers = JsonConvert.DeserializeObject<List<object>>(session.AnswersJson);
                    
                    session.Score = answers?.Count ?? 0;
                    session.MaxScore = (questions?.Count ?? 0) * 6;
                    
                    await _testSession.UpdateSession(session);
                    
                    Console.WriteLine($"[MyExams] User {username} ended test from MyExams page. Token: {token}");
                    
                    TempData["TestEndedMessage"] = "המבחן הסתיים! התוצאות נשמרו.";
                }
            }
            
            return RedirectToPage("/MyExams");
        }

        public int GetQuestionCount(TestSession session)
        {
            try
            {
                if (string.IsNullOrEmpty(session.QuestionsJson))
                    return 0;
                
                var questions = JsonConvert.DeserializeObject<List<object>>(session.QuestionsJson);
                return questions?.Count ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        public string GetRemainingTime(TestSession session)
        {
            if (_testSession == null || session == null || session.Status != "active")
                return null;

            try
            {
                var remaining = _testSession.GetRemainingTime(session);
                if (remaining <= TimeSpan.Zero)
                    return "פג תוקף";

                return string.Format("{0:D2}:{1:D2}:{2:D2}", 
                    (int)remaining.TotalHours, 
                    remaining.Minutes, 
                    remaining.Seconds);
            }
            catch
            {
                return null;
            }
        }
    }
}

