using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using HelloWorldWeb.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace HelloWorldWeb.Pages
{
    public class IndexModel : PageModel
    {
        private readonly AuthService _authService;

        public IndexModel(AuthService authService)
        {
            _authService = authService;
        }

        public bool AnswerChecked { get; set; }
        public bool IsCorrect { get; set; }
        public string SelectedAnswer { get; set; }
        public string QuestionImage { get; set; }
        public Dictionary<string, string> ShuffledAnswers { get; set; }
        public string Username { get; set; }
        public string ConnectionStatus { get; set; }
        public int OnlineCount { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(Username))
                return RedirectToPage("/Login");

            var isUp = await _authService.CheckConnection();
            ConnectionStatus = isUp ? "✅ Supabase connection OK" : "❌ Supabase connection FAILED";

            if (HttpContext.Session.GetString("SessionStart") == null)
            {
                HttpContext.Session.SetString("SessionStart", DateTime.UtcNow.ToString());
                HttpContext.Session.SetInt32("RapidTotal", 0);
                HttpContext.Session.SetInt32("RapidCorrect", 0);
            }

            var user = await _authService.GetUser(Username);
            if (user != null)
            {
                if (user.IsBanned)
                {
                    HttpContext.Session.Clear();
                    Response.Cookies.Delete("Username");
                    return RedirectToPage("/Login");
                }
                user.LastSeen = DateTime.UtcNow;
                await _authService.UpdateUser(user);
            }

            OnlineCount = (await _authService.GetAllUsers())
                .Where(u => u.LastSeen != null && u.LastSeen > DateTime.UtcNow.AddMinutes(-5)).Count();

            LoadRandomQuestion();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (Request.Form.ContainsKey("logout"))
            {
                HttpContext.Session.Clear();
                Response.Cookies.Delete("Username");
                return RedirectToPage("/Index");
            }

            Username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(Username))
                return RedirectToPage("/Login");

            var user = await _authService.GetUser(Username);
            if (user == null)
                return RedirectToPage("/Login");

            if (user.IsBanned)
            {
                HttpContext.Session.Clear();
                Response.Cookies.Delete("Username");
                return RedirectToPage("/Login");
            }

            if (Request.Form.ContainsKey("reset"))
            {
                user.CorrectAnswers = 0;
                user.TotalAnswered = 0;
                user.IsCheater = false;
                await _authService.UpdateUser(user);
                return RedirectToPage("/Index");
            }

            var answer = Request.Form["answer"];
            var questionImage = Request.Form["questionImage"];
            var answersJson = Request.Form["answersJson"];

            if (string.IsNullOrEmpty(answersJson))
            {
                LoadRandomQuestion();
                return Page();
            }

            SelectedAnswer = answer;
            AnswerChecked = true;
            QuestionImage = questionImage;
            ShuffledAnswers = JsonConvert.DeserializeObject<Dictionary<string, string>>(answersJson);
            IsCorrect = answer == "correct";

            user.TotalAnswered++;
            if (IsCorrect)
            {
                user.CorrectAnswers++;
                MoveCorrectImages();
            }

            await _authService.UpdateUser(user);

            var sessionStartStr = HttpContext.Session.GetString("SessionStart");
            DateTime.TryParse(sessionStartStr, out var sessionStart);
            var now = DateTime.UtcNow;
            var elapsedSeconds = (now - sessionStart).TotalSeconds;

            var rapidTotal = HttpContext.Session.GetInt32("RapidTotal") ?? 0;
            var rapidCorrect = HttpContext.Session.GetInt32("RapidCorrect") ?? 0;

            if (elapsedSeconds <= 100)
            {
                HttpContext.Session.SetInt32("RapidTotal", rapidTotal + 1);
                if (IsCorrect)
                    HttpContext.Session.SetInt32("RapidCorrect", rapidCorrect + 1);
            }
            else
            {
                HttpContext.Session.SetString("SessionStart", now.ToString());
                HttpContext.Session.SetInt32("RapidTotal", 1);
                HttpContext.Session.SetInt32("RapidCorrect", IsCorrect ? 1 : 0);
            }

            rapidTotal = HttpContext.Session.GetInt32("RapidTotal") ?? 0;
            rapidCorrect = HttpContext.Session.GetInt32("RapidCorrect") ?? 0;

            int cheaterCount = HttpContext.Session.GetInt32("CheaterCount") ?? 0;

            if (rapidTotal >= 10 || rapidCorrect >= 8)
            {
                Console.WriteLine($"[CHEATER DETECTED] User: {user.Username} | RapidTotal: {rapidTotal} | RapidCorrect: {rapidCorrect}");
                user.CorrectAnswers = 0;
                user.TotalAnswered = 0;
                user.IsCheater = true;
                await _authService.UpdateUser(user);

                cheaterCount++;
                HttpContext.Session.SetInt32("CheaterCount", cheaterCount);

                if (cheaterCount >= 3)
                {
                    user.IsBanned = true;
                    await _authService.UpdateUser(user);
                    HttpContext.Session.Clear();
                    Response.Cookies.Delete("Username");
                    return RedirectToPage("/Login");
                }

                HttpContext.Session.SetInt32("RapidTotal", 0);
                HttpContext.Session.SetInt32("RapidCorrect", 0);
                return RedirectToPage("/Cheater");
            }

            OnlineCount = (await _authService.GetAllUsers())
                .Where(u => u.LastSeen != null && u.LastSeen > DateTime.UtcNow.AddMinutes(-5)).Count();

            return Page();
        }

        private void MoveCorrectImages()
        {
            var wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var imagesPath = Path.Combine(wwwroot, "images");
            var correctPath = Path.Combine(wwwroot, "correct_answers");

            if (!Directory.Exists(correctPath))
                Directory.CreateDirectory(correctPath);

            var allFiles = new[] {
                QuestionImage,
                ShuffledAnswers["correct"],
                ShuffledAnswers["a"],
                ShuffledAnswers["b"],
                ShuffledAnswers["c"]
            };

            foreach (var file in allFiles)
            {
                var source = Path.Combine(imagesPath, file);
                var dest = Path.Combine(correctPath, file);
                if (System.IO.File.Exists(source) && !System.IO.File.Exists(dest))
                    System.IO.File.Move(source, dest);
            }
        }

        private void LoadRandomQuestion()
        {
            var imagesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");

            var allImages = Directory.GetFiles(imagesDir)
                .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".webp"))
                .Select(Path.GetFileName)
                .OrderBy(name => name)
                .ToList();

            var grouped = new List<List<string>>();
            for (int i = 0; i + 4 < allImages.Count; i += 5)
                grouped.Add(allImages.GetRange(i, 5));

            if (grouped.Count == 0)
            {
                QuestionImage = "placeholder.jpg";
                ShuffledAnswers = new Dictionary<string, string>();
                return;
            }

            var chosen = grouped[new Random().Next(grouped.Count)];
            QuestionImage = chosen[0];
            var correct = chosen[1];
            var wrong = chosen.Skip(2).Take(3).ToList();

            ShuffledAnswers = new List<(string, string)>
            {
                ("correct", correct),
                ("a", wrong[0]),
                ("b", wrong[1]),
                ("c", wrong[2])
            }
            .OrderBy(x => Guid.NewGuid())
            .ToDictionary(x => x.Item1, x => x.Item2);
        }
    }
}
