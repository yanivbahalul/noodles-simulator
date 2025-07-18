using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;
using MailKit.Net.Smtp;
using MimeKit;
using System.Security.Cryptography;

namespace NoodlesSimulator.Pages
{
    [IgnoreAntiforgeryToken]
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

        // private static readonly Random _random = new Random(); // לא צריך יותר

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

        public async Task<IActionResult> OnPostReportErrorAsync()
        {
            try
            {
                string body;
                using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                    body = await reader.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(body))
                    return new JsonResult(new { error = "Empty body" }) { StatusCode = 400 };

                var data = Newtonsoft.Json.Linq.JObject.Parse(body);
                var questionImage = data["questionImage"]?.ToString();
                var answers = data["answers"]?.ToString();
                var correctAnswer = data["correctAnswer"]?.ToString();
                var explanation = data["explanation"]?.ToString();
                var selectedAnswer = data["selectedAnswer"]?.ToString();
                var username = HttpContext.Session.GetString("Username") ?? "Unknown";
                var timestamp = DateTime.UtcNow;

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("Noodles Simulator", "yanivbahlul@gmail.com"));
                message.To.Add(new MailboxAddress("Yaniv Bahlul", "yanivbahlul@gmail.com"));
                message.Subject = $"[Noodles Simulator] דיווח טעות חדשה מהמשתמש {username}";

                var bodyBuilder = new BodyBuilder();
                var answersDict = new Dictionary<string, string>();
                try
                {
                    if (!string.IsNullOrWhiteSpace(answers))
                        answersDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(answers);
                }
                catch { }

                var abcd = new[] { "A", "B", "C", "D" };
                var answersBlock = new StringBuilder();
                var allAnswers = answersDict.Values.ToList();
                int correctIdx = allAnswers.IndexOf(correctAnswer);
                if (correctIdx >= 0)
                {
                    answersBlock.Append($"A: {allAnswers[correctIdx]} <br>");
                }
                var distractors = allAnswers.Where((v, i) => i != correctIdx).ToList();
                for (int i = 0; i < distractors.Count && i < 3; i++)
                {
                    answersBlock.Append($"{abcd[i + 1]}: {distractors[i]} <br>");
                }
                string selectedLetter = "לא סומנה תשובה";
                if (!string.IsNullOrWhiteSpace(selectedAnswer))
                {
                    if (correctIdx >= 0 && selectedAnswer == answersDict.Keys.ElementAt(correctIdx))
                        selectedLetter = "A";
                    else
                    {
                        int idx = allAnswers.IndexOf(answersDict.ContainsKey(selectedAnswer) ? answersDict[selectedAnswer] : null);
                        if (idx >= 0 && idx != correctIdx)
                        {
                            int distractorIdx = idx < correctIdx ? idx : idx - 1;
                            if (distractorIdx >= 0 && distractorIdx < 3)
                                selectedLetter = abcd[distractorIdx + 1];
                        }
                    }
                }

                // בניית גוף המייל
                var htmlBody =
                    "<div dir='rtl' style='text-align:right; font-family:Arial,sans-serif;'>" +
                    $"📩 דווח חדש התקבל מהמערכת<br><br>" +
                    $"👤 משתמש: {username} <br>" +
                    $"🕓 תאריך: {timestamp:yyyy-MM-dd HH:mm:ss} <br><br>" +
                    $"❓ שאלה: {questionImage} <br><br>" +
                    $"📝 תשובות אפשריות:<br>{answersBlock}<br>" +
                    $"❌ תשובה שסומנה: {selectedLetter} <br><br>" +
                    $"סיבה: {explanation} <br><br>" +
                    "━━━━━━━━━━━━━━━━━━━━━━ <br>" +
                    "מערכת: Noodles Simulator <br>" +
                    "🎮 Find your limits. Or crash into them.<br>" +
                    "</div>";

                bodyBuilder.HtmlBody = htmlBody;
                bodyBuilder.TextBody = null;
                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync("yanivbahlul@gmail.com", "ixakgpzsxfxamyqs");
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Mail Error] {ex}");
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        private void MoveCorrectImages()
        {
            var wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var imagesPath = Path.Combine(wwwroot, "images");
            var correctPath = Path.Combine(wwwroot, "correct_answers");

            if (!Directory.Exists(correctPath))
                Directory.CreateDirectory(correctPath);

            var allFiles = new[]
            {
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

            // בחירה אקראית חזקה
            int index = RandomNumberGenerator.GetInt32(grouped.Count);
            var chosen = grouped[index];
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
