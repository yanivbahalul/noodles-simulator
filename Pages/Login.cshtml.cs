using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using NoodlesSimulator.Models;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System;
using System.Collections.Concurrent;

namespace NoodlesSimulator.Pages
{
    public class LoginModel : PageModel
    {
        private class LoginAttemptState
        {
            public int Failures { get; set; }
            public DateTime LastFailureUtc { get; set; }
            public DateTime? BlockedUntilUtc { get; set; }
        }

        private static readonly ConcurrentDictionary<string, LoginAttemptState> _attempts = new();
        private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan BlockDuration = TimeSpan.FromMinutes(15);
        private const int MaxFailuresBeforeBlock = 8;

        private readonly AuthService _authService;
        private readonly ILogger<LoginModel> _logger;
        private readonly IConfiguration _configuration;

        public LoginModel(AuthService authService, ILogger<LoginModel> logger, IConfiguration configuration)
        {
            _authService = authService;
            _logger = logger;
            _configuration = configuration;
        }

        [BindProperty]
        public string Username { get; set; }

        [BindProperty]
        public string Password { get; set; }

        public string ErrorMessage { get; set; }

        public void OnGet() { }

        private string GetAttemptKey()
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
            var normalizedUsername = (Username ?? string.Empty).Trim().ToLowerInvariant();
            return $"{ip}:{normalizedUsername}";
        }

        private bool IsBlocked(string key)
        {
            if (!_attempts.TryGetValue(key, out var state))
            {
                return false;
            }

            if (state.BlockedUntilUtc.HasValue && state.BlockedUntilUtc.Value > DateTime.UtcNow)
            {
                return true;
            }

            if (DateTime.UtcNow - state.LastFailureUtc > AttemptWindow)
            {
                _attempts.TryRemove(key, out _);
            }

            return false;
        }

        private void RecordFailure(string key)
        {
            var now = DateTime.UtcNow;
            _attempts.AddOrUpdate(
                key,
                _ => new LoginAttemptState
                {
                    Failures = 1,
                    LastFailureUtc = now
                },
                (_, existing) =>
                {
                    if (now - existing.LastFailureUtc > AttemptWindow)
                    {
                        existing.Failures = 1;
                        existing.BlockedUntilUtc = null;
                    }
                    else
                    {
                        existing.Failures++;
                    }

                    existing.LastFailureUtc = now;
                    if (existing.Failures >= MaxFailuresBeforeBlock)
                    {
                        existing.BlockedUntilUtc = now.Add(BlockDuration);
                    }
                    return existing;
                });
        }

        private void RecordSuccess(string key)
        {
            _attempts.TryRemove(key, out _);
        }

        private void RotateSessionForLogin()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete(".Noodles.Session.v2");
            Response.Cookies.Delete(".Noodles.Session.v3");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                var action = Request.Form["action"];
                var attemptKey = GetAttemptKey();

                if (IsBlocked(attemptKey))
                {
                    ErrorMessage = "יותר מדי ניסיונות. נסה שוב מאוחר יותר.";
                    return Page();
                }

                if (action == "login")
                {
                    if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
                    {
                        ErrorMessage = "שם המשתמש והסיסמה לא יכולים להיות ריקים.";
                        return Page();
                    }

                    User user = null;
                    try 
                    { 
                        user = await _authService.Authenticate(Username, Password); 
                    } 
                    catch (Exception ex) 
                    { 
                        _logger.LogError($"Authentication error: {ex}");
                        ErrorMessage = "שגיאה בהתחברות. נסה שוב מאוחר יותר.";
                        RecordFailure(attemptKey);
                        return Page();
                    }

                    if (user != null)
                    {
                        if (user.IsBanned)
                        {
                            _logger.LogWarning($"Banned user attempted login: {Username}");
                            ErrorMessage = "המשתמש הזה נחסם מהמערכת.";
                            return Page();
                        }

                        try
                        {
                            _ = _authService.TouchLastSeen(user.Username, DateTime.UtcNow);

                            RotateSessionForLogin();
                            var isAdminUser = user.IsAdmin || string.Equals(user.Username, "Admin", StringComparison.OrdinalIgnoreCase);
                            HttpContext.Session.SetString("Username", user.Username);
                            HttpContext.Session.SetString("IsAdmin", isAdminUser ? "1" : "0");
                            Response.Cookies.Append("Username", user.Username, new CookieOptions
                            {
                                HttpOnly = true,
                                Secure = true,
                                SameSite = SameSiteMode.Lax,
                                MaxAge = TimeSpan.FromHours(1)
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Session/Cookie error: {ex}");
                            ErrorMessage = "שגיאה בשמירת הפרטים. נסה שוב.";
                            return Page();
                        }

                        RecordSuccess(attemptKey);
                        return RedirectToPage("/Index");
                    }

                    RecordFailure(attemptKey);
                    ErrorMessage = "שם המשתמש או הסיסמה שגויים.";
                    return Page();
                }

                if (action == "register")
                {
                    if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
                    {
                        ErrorMessage = "שם המשתמש והסיסמה לא יכולים להיות ריקים.";
                        return Page();
                    }
                    if (Username.Length < 5 || Password.Length < 5)
                    {
                        ErrorMessage = "שם המשתמש והסיסמה חייבים להיות באורך של לפחות 5 תווים.";
                        return Page();
                    }

                    if (!Regex.IsMatch(Username, @"^[a-zA-Z0-9׳-׳×]+$"))
                    {
                        ErrorMessage = "שם המשתמש יכול להכיל רק אותיות (עברית/אנגלית) ומספרים.";
                        return Page();
                    }

                    User existingUser = null;
                    try 
                    { 
                        existingUser = await _authService.GetUser(Username); 
                    } 
                    catch (Exception ex) 
                    { 
                        _logger.LogError($"GetUser error during registration: {ex}");
                        ErrorMessage = "שגיאה בבדיקת המשתמש. נסה שוב מאוחר יותר.";
                        RecordFailure(attemptKey);
                        return Page();
                    }

                    if (existingUser != null)
                    {
                        ErrorMessage = "שם המשתמש כבר קיים במערכת.";
                        RecordFailure(attemptKey);
                        return Page();
                    }

                    bool success = false;
                    try 
                    { 
                        success = await _authService.Register(Username, Password); 
                    } 
                    catch (Exception ex) 
                    { 
                        _logger.LogError($"Registration error: {ex}");
                        ErrorMessage = "שגיאה בהרשמה. נסה שוב מאוחר יותר.";
                        RecordFailure(attemptKey);
                        return Page();
                    }

                    if (success)
                    {
                        try
                        {
                            RotateSessionForLogin();
                            HttpContext.Session.SetString("Username", Username);
                            HttpContext.Session.SetString("IsAdmin", "0");
                            Response.Cookies.Append("Username", Username, new CookieOptions
                            {
                                HttpOnly = true,
                                Secure = true,
                                SameSite = SameSiteMode.Lax,
                                MaxAge = TimeSpan.FromHours(1)
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Session/Cookie error after registration: {ex}");
                            ErrorMessage = "ההרשמה הצליחה אך יש שגיאה בשמירת הפרטים. נסה להתחבר שוב.";
                            return Page();
                        }

                        RecordSuccess(attemptKey);
                        return RedirectToPage("/Index");
                    }

                    RecordFailure(attemptKey);
                    ErrorMessage = "אירעה שגיאה במהלך ההרשמה.";
                    return Page();
                }

                ErrorMessage = "בקשה לא תקינה.";
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unhandled error in login: {ex}");
                ErrorMessage = "שגיאת מערכת. נסה שוב מאוחר יותר.";
                return Page();
            }
        }
    }
}