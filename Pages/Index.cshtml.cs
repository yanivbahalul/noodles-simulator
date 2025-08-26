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
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Pages
{
    [IgnoreAntiforgeryToken]
    public class IndexModel : PageModel
    {
        private readonly AuthService _authService;
        private readonly SupabaseStorageService _storage; // may be null if not configured
        private readonly EmailService _emailService;

        private static List<string> _localImagesCache;
        private static DateTime _localImagesCachedAt;
        private static readonly TimeSpan _localImagesTtl = TimeSpan.FromMinutes(2);

        public IndexModel(AuthService authService, SupabaseStorageService storage = null, EmailService emailService = null)
        {
            _authService = authService;
            _storage = storage;
            _emailService = emailService;
        }

        public bool AnswerChecked { get; set; }
        public bool IsCorrect { get; set; }
        public string SelectedAnswer { get; set; }
        public string QuestionImage { get; set; }
        public Dictionary<string, string> ShuffledAnswers { get; set; }
        public string Username { get; set; }
        public string ConnectionStatus { get; set; }
        public int OnlineCount { get; set; }

        // Holds signed URLs for current question and answers for rendering
        public string QuestionImageUrl { get; set; }
        public Dictionary<string, string> AnswerImageUrls { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                Username = HttpContext.Session.GetString("Username");
                if (string.IsNullOrEmpty(Username))
                {
                    HttpContext.Session.Clear();
                    Response.Cookies.Delete(".Noodles.Session");
                    Response.Cookies.Delete("Username");
                    return RedirectToPage("/Login");
                }

                var isUp = false;
                try { isUp = await _authService.CheckConnection(); }
                catch (Exception) { }
                ConnectionStatus = isUp ? "✅ Supabase connection OK" : "❌ Supabase connection FAILED";

                if (HttpContext.Session.GetString("SessionStart") == null)
                {
                    HttpContext.Session.SetString("SessionStart", DateTime.UtcNow.ToString());
                    HttpContext.Session.SetInt32("RapidTotal", 0);
                    HttpContext.Session.SetInt32("RapidCorrect", 0);
                }

                User user = null;
                try { user = await _authService.GetUser(Username); }
                catch (Exception) { }
                if (user != null)
                {
                    if (user.IsBanned)
                    {
                        HttpContext.Session.Clear();
                        Response.Cookies.Delete("Username");
                        return RedirectToPage("/Login");
                    }
                    user.LastSeen = DateTime.UtcNow;
                    try { await _authService.UpdateUser(user); } catch (Exception) { }
                }

                try
                {
                    OnlineCount = await _authService.GetOnlineUserCount();
                }
                catch (Exception) { OnlineCount = 0; }

                // Preload next question faster
                try { await LoadRandomQuestionAsync(); } catch (Exception) { }
                return Page();
            }
            catch (Exception ex)
            {
                HttpContext.Session.Clear();
                Response.Cookies.Delete(".Noodles.Session");
                Response.Cookies.Delete("Username");
                Console.WriteLine($"[OnGetAsync Error] {ex}");
                return RedirectToPage("/Login");
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
                try { user = await _authService.GetUser(Username); } catch (Exception) { }
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
                    try { await _authService.UpdateUser(user); } catch (Exception) { }
                    return RedirectToPage("/Index");
                }

                var answer = Request.Form["answer"];
                var questionImage = Request.Form["questionImage"];
                var answersJson = Request.Form["answersJson"];

                if (string.IsNullOrEmpty(answersJson))
                {
                    try { await LoadRandomQuestionAsync(); } catch (Exception) { }
                    return Page();
                }

                SelectedAnswer = answer;
                AnswerChecked = true;
                QuestionImage = questionImage;
                try { ShuffledAnswers = JsonConvert.DeserializeObject<Dictionary<string, string>>(answersJson); }
                catch (Exception) { ShuffledAnswers = new Dictionary<string, string>(); }
                IsCorrect = answer == "correct";

                user.TotalAnswered++;
                if (IsCorrect)
                {
                    user.CorrectAnswers++;
                    if (_storage == null)
                    {
                        try { MoveCorrectImagesLocal(); } catch (Exception ex) { Console.WriteLine($"[MoveCorrectImagesLocal Error] {ex}"); }
                    }
                }

                try { await _authService.UpdateUser(user); } catch (Exception) { }

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

                if (rapidTotal >= 30 || rapidCorrect >= 20)
                {
                    Console.WriteLine($"[CHEATER DETECTED] User: {user.Username} | RapidTotal: {rapidTotal} | RapidCorrect: {rapidCorrect}");
                    user.CorrectAnswers = 0;
                    user.TotalAnswered = 0;
                    user.IsCheater = true;
                    try { await _authService.UpdateUser(user); } catch (Exception) { }

                    cheaterCount++;
                    HttpContext.Session.SetInt32("CheaterCount", cheaterCount);

                    if (cheaterCount >= 3)
                    {
                        user.IsBanned = true;
                        try { await _authService.UpdateUser(user); } catch (Exception) { }
                        HttpContext.Session.Clear();
                        Response.Cookies.Delete("Username");
                        return RedirectToPage("/Login");
                    }

                    HttpContext.Session.SetInt32("RapidTotal", 0);
                    HttpContext.Session.SetInt32("RapidCorrect", 0);
                    return RedirectToPage("/Cheater");
                }

                // Do not block response on online count; compute best-effort
                _ = Task.Run(async () =>
                {
                    try { OnlineCount = await _authService.GetOnlineUserCount(); } catch { OnlineCount = 0; }
                });

                await PopulateUrlsAsync();

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
                message.Subject = $"[Noodles Simulator] דיווח טעות חדשה מהמשתמש {username}";

                var bodyBuilder = new BodyBuilder();
                var answersDict = new Dictionary<string, string>();
                try
                {
                    if (!string.IsNullOrWhiteSpace(answers))
                        answersDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(answers);
                }
                catch (Exception) { }

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

                // Fire-and-forget via EmailService to avoid blocking the user response
                try
                {
                    var to = Environment.GetEnvironmentVariable("EMAIL_TO") ?? "";
                    if (!string.IsNullOrWhiteSpace(to) && _emailService != null)
                    {
                        var html = bodyBuilder.HtmlBody;
                        _ = _emailService.SendEmailAsync(to, message.Subject, html);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ReportEmail Dispatch Error] {ex}");
                }

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OnPostReportErrorAsync Error] {ex}");
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        private async Task LoadRandomQuestionAsync()
        {
            try
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
                    var imagesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
                    if (!Directory.Exists(imagesDir))
                    {
                        QuestionImage = "placeholder.jpg";
                        ShuffledAnswers = new Dictionary<string, string>();
                        await PopulateUrlsAsync();
                        return;
                    }

                    if (_localImagesCache != null && (DateTime.UtcNow - _localImagesCachedAt) < _localImagesTtl)
                    {
                        filtered = _localImagesCache;
                    }
                    else
                    {
                        filtered = Directory.GetFiles(imagesDir)
                            .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".webp"))
                            .Select(Path.GetFileName)
                            .OrderBy(name => name)
                            .ToList();
                        _localImagesCache = filtered;
                        _localImagesCachedAt = DateTime.UtcNow;
                    }
                }

                var grouped = new List<List<string>>();
                for (int i = 0; i + 4 < filtered.Count; i += 5)
                    grouped.Add(filtered.GetRange(i, 5));

                if (grouped.Count == 0)
                {
                    QuestionImage = "placeholder.jpg";
                    ShuffledAnswers = new Dictionary<string, string>();
                    await PopulateUrlsAsync();
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
                .OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue))
                .ToDictionary(x => x.Item1, x => x.Item2);

                await PopulateUrlsAsync();
            }
            catch (Exception)
            {
            }
        }

        private async Task PopulateUrlsAsync()
        {
            if (_storage != null)
            {
                await PopulateSignedUrlsAsync();
                return;
            }

            try
            {
                QuestionImageUrl = string.IsNullOrWhiteSpace(QuestionImage) ? string.Empty : ($"/images/{QuestionImage}");
                AnswerImageUrls = new Dictionary<string, string>();
                foreach (var kv in ShuffledAnswers ?? new Dictionary<string, string>())
                {
                    if (!string.IsNullOrWhiteSpace(kv.Value))
                        AnswerImageUrls[kv.Key] = $"/images/{kv.Value}";
                }
            }
            catch (Exception)
            {
                QuestionImageUrl = string.Empty;
                AnswerImageUrls = new Dictionary<string, string>();
            }
        }

        private async Task PopulateSignedUrlsAsync()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(QuestionImage))
                    QuestionImageUrl = await _storage.GetSignedUrlAsync(QuestionImage);
                else
                    QuestionImageUrl = string.Empty;

                var keys = ShuffledAnswers?.Values?.Where(v => !string.IsNullOrWhiteSpace(v)).ToList() ?? new List<string>();
                var urls = await _storage.GetSignedUrlsAsync(keys);
                AnswerImageUrls = new Dictionary<string, string>();
                foreach (var kv in ShuffledAnswers ?? new Dictionary<string, string>())
                {
                    if (!string.IsNullOrWhiteSpace(kv.Value) && urls.TryGetValue(kv.Value, out var url))
                        AnswerImageUrls[kv.Key] = url;
                }
            }
            catch (Exception)
            {
                QuestionImageUrl = string.Empty;
                AnswerImageUrls = new Dictionary<string, string>();
            }
        }

        private void MoveCorrectImagesLocal()
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
                Console.WriteLine($"[MoveCorrectImagesLocal Error] {ex}");
            }
        }
    }
}
