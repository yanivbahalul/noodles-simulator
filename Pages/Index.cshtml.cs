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
        private readonly QuestionStatsService _stats;

        private static List<string> _localImagesCache;
        private static DateTime _localImagesCachedAt;
        private static readonly TimeSpan _localImagesTtl = TimeSpan.FromMinutes(2);
        // Prevent the same question (first image in a group of 5) from showing >3 times/hour
        private static readonly object _questionRateLock = new object();
        private static readonly Dictionary<string, List<DateTime>> _questionShownTimes = new Dictionary<string, List<DateTime>>();

        // Shuffle-bag to ensure we cycle through all groups before repeating
        private static readonly object _bagLock = new object();
        private static List<int> _bagOrder;
        private static int _bagIndex = 0;
        private static int _bagSourceCount = 0;
        private static DateTime _bagBuiltAt;
        private static readonly TimeSpan _bagTtl = TimeSpan.FromMinutes(30);
        private static readonly Dictionary<string, int> _groupShownCount = new Dictionary<string, int>();

        // Debug snapshots for diagnostics endpoint
        public static (int trackedQuestions, int throttledNow) GetThrottleSnapshot()
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddHours(-1);
            lock (_questionRateLock)
            {
                int throttled = 0;
                foreach (var kv in _questionShownTimes)
                {
                    var list = kv.Value;
                    list.RemoveAll(t => t < cutoff);
                    if (list.Count >= 3) throttled++;
                }
                return (_questionShownTimes.Count, throttled);
            }
        }

        public static Dictionary<int, int> GetGroupShownHistogramSnapshot()
        {
            lock (_bagLock)
            {
                var hist = new Dictionary<int, int>();
                foreach (var kv in _groupShownCount)
                {
                    var c = kv.Value;
                    if (!hist.ContainsKey(c)) hist[c] = 0;
                    hist[c]++;
                }
                return hist;
            }
        }

        public IndexModel(AuthService authService, SupabaseStorageService storage = null, EmailService emailService = null, QuestionStatsService stats = null)
        {
            _authService = authService;
            _storage = storage;
            _emailService = emailService;
            _stats = stats;
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
        
        // Store original file names for reporting purposes
        public string QuestionImageOriginalName { get; set; }
        public Dictionary<string, string> AnswerImageOriginalNames { get; set; }

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

                try { _stats?.Record(QuestionImage, IsCorrect); } catch { }

                try { await _authService.UpdateUser(user); } catch (Exception) { }

                var sessionStartStr = HttpContext.Session.GetString("SessionStart");
                DateTime.TryParse(sessionStartStr, out var sessionStart);
                var now = DateTime.UtcNow;
                var elapsedSeconds = (now - sessionStart).TotalSeconds;

                var rapidTotal = HttpContext.Session.GetInt32("RapidTotal") ?? 0;
                var rapidCorrect = HttpContext.Session.GetInt32("RapidCorrect") ?? 0;

                if (elapsedSeconds <= 120)
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

                if (rapidTotal >= 20 || rapidCorrect >= 15)
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

                // If questionImage looks like a signed URL, try to extract the original filename
                if (!string.IsNullOrWhiteSpace(questionImage) && questionImage.Contains("token="))
                {
                    var extractedName = SupabaseStorageService.ExtractFileNameFromSignedUrl(questionImage);
                    if (!string.IsNullOrWhiteSpace(extractedName))
                        questionImage = extractedName;
                }

                // If correctAnswer looks like a signed URL, try to extract the original filename
                if (!string.IsNullOrWhiteSpace(correctAnswer) && correctAnswer.Contains("token="))
                {
                    var extractedName = SupabaseStorageService.ExtractFileNameFromSignedUrl(correctAnswer);
                    if (!string.IsNullOrWhiteSpace(extractedName))
                        correctAnswer = extractedName;
                }

                var message = new MimeMessage();
                message.Subject = $"[Noodles Simulator] דיווח טעות חדשה מהמשתמש {username}";

                var bodyBuilder = new BodyBuilder();
                var answersDict = new Dictionary<string, string>();
                try
                {
                    if (!string.IsNullOrWhiteSpace(answers))
                        answersDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(answers);
                    
                    // If any answers look like signed URLs, try to extract original filenames
                    if (answersDict != null)
                    {
                        var cleanedAnswers = new Dictionary<string, string>();
                        foreach (var kv in answersDict)
                        {
                            var value = kv.Value;
                            if (!string.IsNullOrWhiteSpace(value) && value.Contains("token="))
                            {
                                var extractedName = SupabaseStorageService.ExtractFileNameFromSignedUrl(value);
                                if (!string.IsNullOrWhiteSpace(extractedName))
                                    value = extractedName;
                            }
                            cleanedAnswers[kv.Key] = value;
                        }
                        answersDict = cleanedAnswers;
                    }
                }
                catch (Exception) { }

                // Determine which answer was selected
                var abcd = new[] { "A", "B", "C", "D" };
                var allAnswers = answersDict.Values.ToList();
                int correctIdx = allAnswers.IndexOf(correctAnswer);
                string correctKey = answersDict.ContainsValue(correctAnswer) 
                    ? answersDict.First(kv => kv.Value == correctAnswer).Key 
                    : null;
                
                string selectedLetter = "לא סומנה תשובה";
                string selectedAnswerValue = "";
                if (!string.IsNullOrWhiteSpace(selectedAnswer))
                {
                    if (answersDict.ContainsKey(selectedAnswer))
                    {
                        selectedAnswerValue = answersDict[selectedAnswer];
                    }
                    
                    if (correctKey != null && selectedAnswer == correctKey)
                    {
                        selectedLetter = "A (תשובה נכונה)";
                    }
                    else
                    {
                        int idx = allAnswers.IndexOf(selectedAnswerValue);
                        if (idx >= 0 && idx != correctIdx)
                        {
                            int distractorIdx = idx < correctIdx ? idx : idx - 1;
                            if (distractorIdx >= 0 && distractorIdx < 3)
                                selectedLetter = abcd[distractorIdx + 1];
                        }
                    }
                }
                
                // Build answers list text
                var answersList = new StringBuilder();
                answersList.Append($"<span style='color: #28a745; font-weight: bold;'>A:</span> <span style='color: #28a745;'>{correctAnswer}</span><br/>");
                var distractors = allAnswers.Where((v, i) => i != correctIdx).ToList();
                for (int i = 0; i < Math.Min(3, distractors.Count); i++)
                {
                    var letter = abcd[i + 1];
                    var distractor = distractors[i];
                    var isSelected = selectedAnswerValue == distractor;
                    var style = isSelected ? "font-weight: bold; color: #ffc107;" : "";
                    answersList.Append($"<span style='{style}'>{letter}: {distractor}</span><br/>");
                }
                
                // Build question view URL - need to get the base URL
                var request = HttpContext.Request;
                var baseUrl = $"{request.Scheme}://{request.Host}";
                
                // Build URL with selected answer and correct answer parameters
                var queryParams = new System.Collections.Specialized.NameValueCollection();
                queryParams.Add("id", questionImage);
                if (!string.IsNullOrWhiteSpace(selectedAnswer))
                {
                    // Map selectedAnswer key to the actual answer key (correct, a, b, c)
                    if (answersDict.ContainsKey(selectedAnswer))
                    {
                        queryParams.Add("selected", selectedAnswer);
                    }
                }
                queryParams.Add("correct", "correct"); // Always "correct" is the right answer
                
                var queryString = string.Join("&", 
                    queryParams.AllKeys.Select(key => $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(queryParams[key])}"));
                var questionViewUrl = $"{baseUrl}/QuestionView?{queryString}";

                var htmlBody = $@"
<!DOCTYPE html>
<html dir='rtl' lang='he'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>דיווח שגיאה</title>
</head>
<body style='margin: 0; padding: 0; background-color: #f5f5f5; direction: rtl;'>
    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f9f9f9; direction: rtl;'>
        <!-- Header with gradient -->
        <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 20px; border-radius: 10px 10px 0 0; text-align: center; direction: rtl;'>
            <h2 style='color: white; margin: 0; direction: rtl; unicode-bidi: embed;'>📩 דיווח חדש התקבל מהמערכת</h2>
        </div>
        
        <!-- Main content -->
        <div style='background-color: white; padding: 25px; border-radius: 0 0 10px 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); direction: rtl; text-align: right;'>
            <!-- User and timestamp -->
            <p style='font-size: 16px; color: #333; line-height: 1.8; direction: rtl; text-align: right; unicode-bidi: embed;'>
                <strong>👤 משתמש:</strong> {username}<br/>
                <strong>🕓 תאריך:</strong> {timestamp:dd/MM/yyyy HH:mm:ss}<br/>
            </p>
            
            <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'/>
            
            <!-- Question Info -->
            <div style='margin: 20px 0; padding: 20px; background-color: #f8f9fa; border-radius: 8px; direction: rtl; text-align: right;'>
                <p style='font-size: 16px; color: #333; margin-bottom: 15px; direction: rtl; unicode-bidi: embed;'>
                    <strong>❓ שם קובץ השאלה:</strong> {questionImage}
                </p>
                <p style='font-size: 16px; color: #333; margin-bottom: 15px; direction: rtl; unicode-bidi: embed;'>
                    <strong>📝 תשובות אפשריות:</strong><br/>
                    <span style='font-size: 14px; line-height: 1.8;'>{answersList.ToString()}</span>
                </p>
            </div>
            
            <!-- Explanation (optional box) -->
            <div style='background-color: #fff3cd; border-right: 4px solid #ffc107; padding: 15px; margin: 20px 0; border-radius: 5px; direction: rtl; text-align: right;'>
                <strong style='unicode-bidi: embed;'>💬 סיבה:</strong> <span style='unicode-bidi: embed;'>{explanation}</span>
            </div>
            
            <!-- Link to view question -->
            <div style='margin: 25px 0; padding: 20px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); border-radius: 8px; text-align: center;'>
                <a href='{questionViewUrl}' 
                   target='_blank'
                   style='display: inline-block; padding: 15px 30px; background-color: white; color: #667eea; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 18px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); transition: all 0.3s;'>
                    🔍 להצגת השאלה לחץ כאן
                </a>
                <p style='color: white; margin-top: 15px; font-size: 14px; direction: rtl; unicode-bidi: embed;'>
                    הקישור יפתח את השאלה עם כל התשובות בעמוד נפרד באתר
                </p>
            </div>
            
            <hr style='border: none; border-top: 1px solid #eee; margin: 25px 0;'/>
            
            <!-- Footer -->
            <p style='text-align: center; color: #888; font-size: 14px; direction: rtl; unicode-bidi: embed;'>
                <strong>מערכת: Noodles Simulator</strong><br/>
                🎮 Find your limits. Or crash into them.
            </p>
        </div>
    </div>
</body>
</html>";

                bodyBuilder.HtmlBody = htmlBody;
                bodyBuilder.TextBody = null;

                // Fire-and-forget via EmailService to avoid blocking the user response
                try
                {
                    if (_emailService != null && _emailService.IsConfigured)
                    {
                        var html = bodyBuilder.HtmlBody;
                        Console.WriteLine($"[Report] Sending error report email...");
                        var result = _emailService.Send(message.Subject, html);
                        if (result)
                        {
                            Console.WriteLine($"[Report] ✅ Error report email sent successfully");
                        }
                        else
                        {
                            Console.WriteLine($"[Report] ❌ Failed to send error report email");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[Report] Email service not configured, skipping email notification");
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

                // Exclude very recent questions per session to reduce visible repeats
                var recent = GetRecentQuestionsFromSession();

                // Build or reuse a shuffle-bag of group indices to ensure coverage
                int chosenIdx;
                lock (_bagLock)
                {
                    var now = DateTime.UtcNow;
                    var needRebuild = _bagOrder == null || _bagSourceCount != grouped.Count || _bagIndex >= _bagOrder.Count || (now - _bagBuiltAt) > _bagTtl;
                    if (needRebuild)
                    {
                        // Build indices and prefer least-shown groups; shuffle within equal-count buckets
                        var withCounts = new List<(int idx, int count, string key)>();
                        for (int i = 0; i < grouped.Count; i++)
                        {
                            var key = grouped[i].Count > 0 ? grouped[i][0] : $"group-{i}";
                            var cnt = _groupShownCount.TryGetValue(key, out var c) ? c : 0;
                            withCounts.Add((i, cnt, key));
                        }
                        var buckets = withCounts.GroupBy(x => x.count)
                            .OrderBy(g => g.Key)
                            .Select(g => g.ToList()).ToList();
                        var order = new List<int>();
                        foreach (var bucket in buckets)
                        {
                            var indices = bucket.Select(b => b.idx).ToList();
                            FisherYatesShuffle(indices);
                            order.AddRange(indices);
                        }
                        _bagOrder = order;
                        _bagIndex = 0;
                        _bagSourceCount = grouped.Count;
                        _bagBuiltAt = now;
                    }

                    // Advance until we find a non-throttled group, or give up after one full pass
                    int attempts = 0;
                    while (attempts < _bagOrder.Count)
                    {
                        var idx = _bagOrder[_bagIndex % _bagOrder.Count];
                        _bagIndex++;
                        attempts++;
                        var candidate = grouped[idx];
                        if (candidate.Count > 0 && !IsQuestionThrottled(candidate[0]) && !recent.Contains(candidate[0]))
                        {
                            chosenIdx = idx;
                            goto CHOSEN_FOUND;
                        }
                    }

                    // If all candidates are throttled, just take the next in bag anyway
                    chosenIdx = _bagOrder[_bagIndex % _bagOrder.Count];
                    _bagIndex++;
                }
CHOSEN_FOUND:
                var chosen = grouped[chosenIdx];
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

                RecordQuestionShown(QuestionImage);
                IncrementGroupShown(QuestionImage);
                AddRecentQuestionToSession(QuestionImage);
                await PopulateUrlsAsync();
            }
            catch (Exception)
            {
            }
        }

        private static void FisherYatesShuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                if (j != i)
                {
                    var tmp = list[i];
                    list[i] = list[j];
                    list[j] = tmp;
                }
            }
        }

        private static void IncrementGroupShown(string questionImage)
        {
            lock (_bagLock)
            {
                if (!_groupShownCount.ContainsKey(questionImage))
                    _groupShownCount[questionImage] = 0;
                _groupShownCount[questionImage]++;
            }
        }

        private static bool IsQuestionThrottled(string questionImage)
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddHours(-1);
            lock (_questionRateLock)
            {
                if (!_questionShownTimes.TryGetValue(questionImage, out var times))
                    return false;
                times.RemoveAll(t => t < cutoff);
                return times.Count >= 3;
            }
        }

        private static void RecordQuestionShown(string questionImage)
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddHours(-1);
            lock (_questionRateLock)
            {
                if (!_questionShownTimes.TryGetValue(questionImage, out var times))
                {
                    times = new List<DateTime>();
                    _questionShownTimes[questionImage] = times;
                }
                times.RemoveAll(t => t < cutoff);
                times.Add(now);
            }
        }

        private List<string> GetRecentQuestionsFromSession()
        {
            try
            {
                var json = HttpContext.Session.GetString("RecentQuestions");
                if (string.IsNullOrWhiteSpace(json)) return new List<string>();
                var list = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
                return list.TakeLast(10).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private void AddRecentQuestionToSession(string questionImage)
        {
            try
            {
                var list = GetRecentQuestionsFromSession();
                list.Add(questionImage);
                if (list.Count > 20)
                    list = list.TakeLast(20).ToList();
                HttpContext.Session.SetString("RecentQuestions", JsonConvert.SerializeObject(list));
            }
            catch { }
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
                // Store original file names for reporting
                QuestionImageOriginalName = QuestionImage;
                AnswerImageOriginalNames = new Dictionary<string, string>();
                foreach (var kv in ShuffledAnswers ?? new Dictionary<string, string>())
                {
                    if (!string.IsNullOrWhiteSpace(kv.Value))
                        AnswerImageOriginalNames[kv.Key] = kv.Value;
                }

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
                QuestionImageOriginalName = string.Empty;
                AnswerImageOriginalNames = new Dictionary<string, string>();
            }
        }

        private async Task PopulateSignedUrlsAsync()
        {
            try
            {
                // Store original file names for reporting
                QuestionImageOriginalName = QuestionImage;
                AnswerImageOriginalNames = new Dictionary<string, string>();
                foreach (var kv in ShuffledAnswers ?? new Dictionary<string, string>())
                {
                    if (!string.IsNullOrWhiteSpace(kv.Value))
                        AnswerImageOriginalNames[kv.Key] = kv.Value;
                }

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
                QuestionImageOriginalName = string.Empty;
                AnswerImageOriginalNames = new Dictionary<string, string>();
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
