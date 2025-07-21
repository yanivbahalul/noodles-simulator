using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using NoodlesSimulator.Models;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace NoodlesSimulator.Pages
{
    public class LoginModel : PageModel
    {
        private readonly AuthService _authService;
        private readonly ILogger<LoginModel> _logger;
        private readonly IConfiguration _configuration;
        private int _maxLoginAttempts;
        private static readonly Dictionary<string, (int attempts, DateTime lastAttempt)> _loginAttempts = new();
        private static readonly SemaphoreSlim _loginThrottler = new(10); // max 10 concurrent logins

        public LoginModel(AuthService authService, ILogger<LoginModel> logger, IConfiguration configuration)
        {
            _authService = authService;
            _logger = logger;
            _configuration = configuration;
            _maxLoginAttempts = configuration.GetValue<int>("Security:MaxLoginAttempts", 5);
        }

        [BindProperty]
        public string Username { get; set; }

        [BindProperty]
        public string Password { get; set; }

        public string ErrorMessage { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                // Rate limiting
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                if (_loginAttempts.ContainsKey(ip))
                {
                    var (attempts, lastAttempt) = _loginAttempts[ip];
                    if (attempts >= _maxLoginAttempts && DateTime.UtcNow.Subtract(lastAttempt).TotalMinutes < 15)
                    {
                        _logger.LogWarning($"Too many login attempts from IP: {ip}");
                        ErrorMessage = "נסיונות התחברות רבים מדי. נסה שוב בעוד 15 דקות.";
                        return Page();
                    }
                    if (DateTime.UtcNow.Subtract(lastAttempt).TotalMinutes >= 15)
                    {
                        _loginAttempts[ip] = (1, DateTime.UtcNow);
                    }
                    else
                    {
                        _loginAttempts[ip] = (attempts + 1, DateTime.UtcNow);
                    }
                }
                else
                {
                    _loginAttempts[ip] = (1, DateTime.UtcNow);
                }

                // Throttle concurrent logins
                if (!await _loginThrottler.WaitAsync(TimeSpan.FromSeconds(5)))
                {
                    _logger.LogWarning("Login throttled due to high concurrent requests");
                    ErrorMessage = "המערכת עמוסה, נסה שוב בעוד מספר שניות.";
                    return Page();
                }

                try
                {
                    var action = Request.Form["action"];

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
                            return Page();
                        }

                        if (user != null)
                        {
                            if (user.IsBanned)
                            {
                                _logger.LogWarning($"Banned user attempted login: {Username}");
                                ErrorMessage = "🚫 המשתמש הזה נחסם מהמערכת.";
                                return Page();
                            }

                            // Clear login attempts on successful login
                            if (_loginAttempts.ContainsKey(ip))
                            {
                                _loginAttempts.Remove(ip);
                            }

                            try
                            {
                                HttpContext.Session.SetString("Username", user.Username);
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

                            return RedirectToPage("/Index");
                        }

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
                            ErrorMessage = "שם המשתמש והסיסמה חייבים להיות לפחות באורך של 5 תווים.";
                            return Page();
                        }

                        if (!Regex.IsMatch(Username, @"^[a-zA-Z0-9א-ת]+$"))
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
                            return Page();
                        }

                        if (existingUser != null)
                        {
                            ErrorMessage = "שם המשתמש כבר קיים במערכת.";
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
                            return Page();
                        }

                        if (success)
                        {
                            try
                            {
                                HttpContext.Session.SetString("Username", Username);
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

                            return RedirectToPage("/Index");
                        }

                        ErrorMessage = "אירעה שגיאה במהלך ההרשמה.";
                        return Page();
                    }

                    ErrorMessage = "בקשה לא תקינה.";
                    return Page();
                }
                finally
                {
                    _loginThrottler.Release();
                }
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
