using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Pages
{
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
        public bool ShowExamFixNotice { get; set; }

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
                catch (Exception ex) { Console.WriteLine($"[OnGetAsync CheckConnection Error] {ex.Message}"); }
                ConnectionStatus = isUp ? "Supabase connection OK" : "Supabase connection FAILED";

                if (HttpContext.Session.GetString("SessionStart") == null)
                {
                    HttpContext.Session.SetString("SessionStart", DateTime.UtcNow.ToString());
                    HttpContext.Session.SetInt32("RapidTotal", 0);
                    HttpContext.Session.SetInt32("RapidCorrect", 0);
                }

                User user = null;
                try { user = await _authService.GetUser(Username); }
                catch (Exception ex) { Console.WriteLine($"[OnGetAsync GetUser Error] {ex.Message}"); }
                if (user != null)
                {
                    if (user.IsBanned)
                    {
                        HttpContext.Session.Clear();
                        Response.Cookies.Delete("Username");
                        return RedirectToPage("/Login");
                    }
                    try { await _authService.TouchLastSeen(user.Username, DateTime.UtcNow); } catch (Exception ex) { Console.WriteLine($"[OnGetAsync UpdateLastSeen Error] {ex.Message}"); }
                    ShowExamFixNotice = !_authService.HasDismissedNotice(user, AppNotices.ExamFix);
                }

                try
                {
                    OnlineCount = await _authService.GetOnlineUserCount();
                }
                catch (Exception) { OnlineCount = 0; }

                // Preload next question faster
                try { await LoadRandomQuestionAsync(); } catch (Exception ex) { Console.WriteLine($"[OnGetAsync PreloadQuestion Error] {ex.Message}"); }
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
                    return HandleLogoutPost();

                var auth = await TryRequireAuthenticatedUserAsync();
                if (auth.Redirect != null)
                    return auth.Redirect;

                if (Request.Form.ContainsKey("reset"))
                    return await HandleResetPostAsync(auth.User);

                if (string.IsNullOrEmpty(Request.Form["answersJson"]))
                {
                    try { await LoadRandomQuestionAsync(); }
                    catch (Exception ex) { Console.WriteLine($"[OnPostAsync ReloadQuestion Error] {ex.Message}"); }
                    return Page();
                }

                await ProcessSubmittedAnswerAsync(auth.User);
                var cheaterRedirect = await TryHandleCheaterDetectionAsync(auth.User);
                if (cheaterRedirect != null)
                    return cheaterRedirect;

                _ = Task.Run(async () =>
                {
                    try { OnlineCount = await _authService.GetOnlineUserCount(); }
                    catch { OnlineCount = 0; }
                });

                await PopulateUrlsAsync();
                return Page();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OnPostAsync Error] {ex}");
                return StatusCode(500, "Server error");
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

                using var doc = JsonDocument.Parse(body);
                var payload = ErrorReportBuilder.TryParse(doc, HttpContext.Session.GetString("Username"));
                if (payload == null)
                    return new JsonResult(new { error = "Invalid body" }) { StatusCode = 400 };

                var request = HttpContext.Request;
                var baseUrl = $"{request.Scheme}://{request.Host}";
                var htmlBody = ErrorReportBuilder.BuildHtmlBody(payload, baseUrl);
                var reportSubject = ErrorReportBuilder.BuildSubject(payload.Username);

                await TrySendReportEmailAsync(reportSubject, htmlBody);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OnPostReportErrorAsync Error] {ex}");
                return new JsonResult(new { error = "Server error" }) { StatusCode = 500 };
            }
        }

        private IActionResult HandleLogoutPost()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete("Username");
            return RedirectToPage("/Index");
        }

        private async Task<(User User, IActionResult Redirect)> TryRequireAuthenticatedUserAsync()
        {
            Username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(Username))
                return (null, RedirectToPage("/Login"));

            User user = null;
            try { user = await _authService.GetUser(Username); }
            catch (Exception ex) { Console.WriteLine($"[OnPostAsync GetUser Error] {ex.Message}"); }
            if (user == null)
                return (null, RedirectToPage("/Login"));

            if (user.IsBanned)
            {
                HttpContext.Session.Clear();
                Response.Cookies.Delete("Username");
                return (null, RedirectToPage("/Login"));
            }

            return (user, null);
        }

        private async Task<IActionResult> HandleResetPostAsync(User user)
        {
            user.CorrectAnswers = 0;
            user.TotalAnswered = 0;
            user.IsCheater = false;
            try { await _authService.UpdateUser(user); }
            catch (Exception ex) { Console.WriteLine($"[OnPostAsync Reset UpdateUser Error] {ex.Message}"); }
            return RedirectToPage("/Index");
        }

        private async Task ProcessSubmittedAnswerAsync(User user)
        {
            var answer = Request.Form["answer"];
            var questionImage = Request.Form["questionImage"];
            var answersJson = Request.Form["answersJson"];

            SelectedAnswer = answer;
            AnswerChecked = true;
            QuestionImage = questionImage;
            try { ShuffledAnswers = JsonSerializer.Deserialize<Dictionary<string, string>>(answersJson, AppJson.Options); }
            catch (Exception) { ShuffledAnswers = new Dictionary<string, string>(); }
            IsCorrect = answer == "correct";

            user.TotalAnswered++;
            if (IsCorrect)
            {
                user.CorrectAnswers++;
                if (_storage == null)
                {
                    try { MoveCorrectImagesLocal(); }
                    catch (Exception ex) { Console.WriteLine($"[MoveCorrectImagesLocal Error] {ex}"); }
                }
            }

            try { _stats?.Record(QuestionImage, IsCorrect); }
            catch (Exception ex) { Console.WriteLine($"[OnPostAsync RecordStats Error] {ex.Message}"); }

            try { await _authService.UpdateUser(user); }
            catch (Exception ex) { Console.WriteLine($"[OnPostAsync UpdateUser Error] {ex.Message}"); }

            UpdateRapidAnswerCounters();
        }

        private void UpdateRapidAnswerCounters()
        {
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
        }

        private async Task<IActionResult> TryHandleCheaterDetectionAsync(User user)
        {
            var rapidTotal = HttpContext.Session.GetInt32("RapidTotal") ?? 0;
            var rapidCorrect = HttpContext.Session.GetInt32("RapidCorrect") ?? 0;
            if (rapidTotal < 20 && rapidCorrect < 15)
                return null;

            Console.WriteLine($"[CHEATER DETECTED] User: {user.Username} | RapidTotal: {rapidTotal} | RapidCorrect: {rapidCorrect}");
            user.CorrectAnswers = 0;
            user.TotalAnswered = 0;
            user.IsCheater = true;
            try { await _authService.UpdateUser(user); }
            catch (Exception ex) { Console.WriteLine($"[OnPostAsync CheaterMark UpdateUser Error] {ex.Message}"); }

            var cheaterCount = (HttpContext.Session.GetInt32("CheaterCount") ?? 0) + 1;
            HttpContext.Session.SetInt32("CheaterCount", cheaterCount);

            if (cheaterCount >= 3)
            {
                user.IsBanned = true;
                try { await _authService.UpdateUser(user); }
                catch (Exception ex) { Console.WriteLine($"[OnPostAsync Ban UpdateUser Error] {ex.Message}"); }
                HttpContext.Session.Clear();
                Response.Cookies.Delete("Username");
                return RedirectToPage("/Login");
            }

            HttpContext.Session.SetInt32("RapidTotal", 0);
            HttpContext.Session.SetInt32("RapidCorrect", 0);
            return RedirectToPage("/Cheater");
        }

        private Task TrySendReportEmailAsync(string reportSubject, string htmlBody)
        {
            try
            {
                if (_emailService == null || !_emailService.IsConfigured)
                {
                    Console.WriteLine("[Report] Email service not configured, skipping email notification");
                    return Task.CompletedTask;
                }

                Console.WriteLine("[Report] Sending error report email...");
                var result = _emailService.Send(reportSubject, htmlBody);
                if (result)
                    Console.WriteLine("[Report] Error report email sent successfully");
                else
                    Console.WriteLine("[Report] Failed to send error report email");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ReportEmail Dispatch Error] {ex}");
            }

            return Task.CompletedTask;
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
                var list = JsonSerializer.Deserialize<List<string>>(json, AppJson.Options) ?? new List<string>();
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
                HttpContext.Session.SetString("RecentQuestions", JsonSerializer.Serialize(list, AppJson.Options));
            }
            catch (Exception ex) { Console.WriteLine($"[AddRecentQuestionToSession Error] {ex.Message}"); }
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