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
            try
            {
                Username = HttpContext.Session.GetString("Username");
                if (string.IsNullOrEmpty(Username))
                    return RedirectToPage("/Login");

                var isUp = false;
                try { isUp = await _authService.CheckConnection(); }
                catch (Exception ex) { Console.WriteLine($"[CheckConnection Error] {ex}"); }
                ConnectionStatus = isUp ? "✅ Supabase connection OK" : "❌ Supabase connection FAILED";

                if (HttpContext.Session.GetString("SessionStart") == null)
                {
                    HttpContext.Session.SetString("SessionStart", DateTime.UtcNow.ToString());
                    HttpContext.Session.SetInt32("RapidTotal", 0);
                    HttpContext.Session.SetInt32("RapidCorrect", 0);
                }

                User user = null;
                try { user = await _authService.GetUser(Username); }
                catch (Exception ex) { Console.WriteLine($"[GetUser Error] {ex}"); }
                if (user != null)
                {
                    if (user.IsBanned)
                    {
                        HttpContext.Session.Clear();
                        Response.Cookies.Delete("Username");
                        return RedirectToPage("/Login");
                    }
                    user.LastSeen = DateTime.UtcNow;
                    try { await _authService.UpdateUser(user); } catch (Exception ex) { Console.WriteLine($"[UpdateUser Error] {ex}"); }
                }

                try
                {
                    var allUsers = await _authService.GetAllUsers();
                    OnlineCount = allUsers.Where(u => u.LastSeen != null && u.LastSeen > DateTime.UtcNow.AddMinutes(-5)).Count();
                }
                catch (Exception ex) { Console.WriteLine($"[GetAllUsers Error] {ex}"); OnlineCount = 0; }

                try { LoadRandomQuestion(); } catch (Exception ex) { Console.WriteLine($"[LoadRandomQuestion Error] {ex}"); }
                return Page();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OnGetAsync Error] {ex}");
                return StatusCode(500, $"Server error: {ex.Message}");
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
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

                User user = null;
                try { user = await _authService.GetUser(Username); } catch (Exception ex) { Console.WriteLine($"[GetUser Error] {ex}"); }
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
                    try { await _authService.UpdateUser(user); } catch (Exception ex) { Console.WriteLine($"[UpdateUser Error] {ex}"); }
                    return RedirectToPage("/Index");
                }

                var answer = Request.Form["answer"];
                var questionImage = Request.Form["questionImage"];
                var answersJson = Request.Form["answersJson"];

                if (string.IsNullOrEmpty(answersJson))
                {
                    try { LoadRandomQuestion(); } catch (Exception ex) { Console.WriteLine($"[LoadRandomQuestion Error] {ex}"); }
                    return Page();
                }

                SelectedAnswer = answer;
                AnswerChecked = true;
                QuestionImage = questionImage;
                try { ShuffledAnswers = JsonConvert.DeserializeObject<Dictionary<string, string>>(answersJson); }
                catch (Exception ex) { Console.WriteLine($"[Deserialize Answers Error] {ex}"); ShuffledAnswers = new Dictionary<string, string>(); }
                IsCorrect = answer == "correct";

                user.TotalAnswered++;
                if (IsCorrect)
                {
                    user.CorrectAnswers++;
                    try { MoveCorrectImages(); } catch (Exception ex) { Console.WriteLine($"[MoveCorrectImages Error] {ex}"); }
                }

                try { await _authService.UpdateUser(user); } catch (Exception ex) { Console.WriteLine($"[UpdateUser Error] {ex}"); }

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
                    try { await _authService.UpdateUser(user); } catch (Exception ex) { Console.WriteLine($"[UpdateUser Error] {ex}"); }

                    cheaterCount++;
                    HttpContext.Session.SetInt32("CheaterCount", cheaterCount);

                    if (cheaterCount >= 3)
                    {
                        user.IsBanned = true;
                        try { await _authService.UpdateUser(user); } catch (Exception ex) { Console.WriteLine($"[UpdateUser Error] {ex}"); }
                        HttpContext.Session.Clear();
                        Response.Cookies.Delete("Username");
                        return RedirectToPage("/Login");
                    }

                    HttpContext.Session.SetInt32("RapidTotal", 0);
                    HttpContext.Session.SetInt32("RapidCorrect", 0);
                    return RedirectToPage("/Cheater");
                }

                try
                {
                    var allUsers = await _authService.GetAllUsers();
                    OnlineCount = allUsers.Where(u => u.LastSeen != null && u.LastSeen > DateTime.UtcNow.AddMinutes(-5)).Count();
                }
                catch (Exception ex) { Console.WriteLine($"[GetAllUsers Error] {ex}"); OnlineCount = 0; }

                return Page();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OnPostAsync Error] {ex}");
                return StatusCode(500, $"Server error: {ex.Message}");
            }
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
                catch (Exception ex) { Console.WriteLine($"[Deserialize Answers Error] {ex}"); }

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

                try
                {
                    using (var client = new SmtpClient())
                    {
                        await client.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
                        await client.AuthenticateAsync("yanivbahlul@gmail.com", "ixakgpzsxfxamyqs");
                        await client.SendAsync(message);
                        await client.DisconnectAsync(true);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MailKit Error] {ex}");
                    return new JsonResult(new { error = "Failed to send email: " + ex.Message }) { StatusCode = 500 };
                }

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OnPostReportErrorAsync Error] {ex}");
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        private void MoveCorrectImages()
        {
            try
            {
                var wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var imagesPath = Path.Combine(wwwroot, "images");
                var correctPath = Path.Combine(wwwroot, "correct_answers");

                if (!Directory.Exists(correctPath))
                    Directory.CreateDirectory(correctPath);

                var allFiles = new[]
                {
                    QuestionImage,
                    ShuffledAnswers.ContainsKey("correct") ? ShuffledAnswers["correct"] : null,
                    ShuffledAnswers.ContainsKey("a") ? ShuffledAnswers["a"] : null,
                    ShuffledAnswers.ContainsKey("b") ? ShuffledAnswers["b"] : null,
                    ShuffledAnswers.ContainsKey("c") ? ShuffledAnswers["c"] : null
                };

                foreach (var file in allFiles)
                {
                    if (string.IsNullOrEmpty(file)) continue;
                    var source = Path.Combine(imagesPath, file);
                    var dest = Path.Combine(correctPath, file);
                    if (System.IO.File.Exists(source) && !System.IO.File.Exists(dest))
                        System.IO.File.Move(source, dest);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MoveCorrectImages Error] {ex}");
            }
        }

        private void LoadRandomQuestion()
        {
            try
            {
                var imagesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
                if (!Directory.Exists(imagesDir))
                {
                    QuestionImage = "placeholder.jpg";
                    ShuffledAnswers = new Dictionary<string, string>();
                    return;
                }

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

                int index = RandomNumberGenerator.GetInt32(grouped.Count);
                var chosen = grouped[index];
                QuestionImage = chosen[0];
                var correct = chosen[1];
                var wrong = chosen.Skip(2).Take(3).ToList();

                ShuffledAnswers = new List<(string, string)>
                {
                    ("correct", correct),
                    ("a", wrong.Count > 0 ? wrong[0] : null),
                    ("b", wrong.Count > 1 ? wrong[1] : null),
                    ("c", wrong.Count > 2 ? wrong[2] : null)
                }
                .Where(x => !string.IsNullOrEmpty(x.Item2))
                .OrderBy(x => Guid.NewGuid())
                .ToDictionary(x => x.Item1, x => x.Item2);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LoadRandomQuestion Error] {ex}");
                QuestionImage = "placeholder.jpg";
                ShuffledAnswers = new Dictionary<string, string>();
            }
        }
    }
}
