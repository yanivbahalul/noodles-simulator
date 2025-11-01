using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using NoodlesSimulator.Models;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System;

namespace NoodlesSimulator.Pages
{
    public class LoginModel : PageModel
    {
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

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                var action = Request.Form["action"];

                if (action == "login")
                {
                    if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
                    {
                        ErrorMessage = "׳©׳ ׳”׳׳©׳×׳׳© ׳•׳”׳¡׳™׳¡׳׳” ׳׳ ׳™׳›׳•׳׳™׳ ׳׳”׳™׳•׳× ׳¨׳™׳§׳™׳.";
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
                        ErrorMessage = "׳©׳’׳™׳׳” ׳‘׳”׳×׳—׳‘׳¨׳•׳×. ׳ ׳¡׳” ׳©׳•׳‘ ׳׳׳•׳—׳¨ ׳™׳•׳×׳¨.";
                        return Page();
                    }

                    if (user != null)
                    {
                        if (user.IsBanned)
                        {
                            _logger.LogWarning($"Banned user attempted login: {Username}");
                            ErrorMessage = "נ« ׳”׳׳©׳×׳׳© ׳”׳–׳” ׳ ׳—׳¡׳ ׳׳”׳׳¢׳¨׳›׳×.";
                            return Page();
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
                            ErrorMessage = "׳©׳’׳™׳׳” ׳‘׳©׳׳™׳¨׳× ׳”׳₪׳¨׳˜׳™׳. ׳ ׳¡׳” ׳©׳•׳‘.";
                            return Page();
                        }

                        return RedirectToPage("/Index");
                    }

                    ErrorMessage = "׳©׳ ׳”׳׳©׳×׳׳© ׳׳• ׳”׳¡׳™׳¡׳׳” ׳©׳’׳•׳™׳™׳.";
                    return Page();
                }

                if (action == "register")
                {
                    if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
                    {
                        ErrorMessage = "׳©׳ ׳”׳׳©׳×׳׳© ׳•׳”׳¡׳™׳¡׳׳” ׳׳ ׳™׳›׳•׳׳™׳ ׳׳”׳™׳•׳× ׳¨׳™׳§׳™׳.";
                        return Page();
                    }
                    if (Username.Length < 5 || Password.Length < 5)
                    {
                        ErrorMessage = "׳©׳ ׳”׳׳©׳×׳׳© ׳•׳”׳¡׳™׳¡׳׳” ׳—׳™׳™׳‘׳™׳ ׳׳”׳™׳•׳× ׳׳₪׳—׳•׳× ׳‘׳׳•׳¨׳ ׳©׳ 5 ׳×׳•׳•׳™׳.";
                        return Page();
                    }

                    if (!Regex.IsMatch(Username, @"^[a-zA-Z0-9׳-׳×]+$"))
                    {
                        ErrorMessage = "׳©׳ ׳”׳׳©׳×׳׳© ׳™׳›׳•׳ ׳׳”׳›׳™׳ ׳¨׳§ ׳׳•׳×׳™׳•׳× (׳¢׳‘׳¨׳™׳×/׳׳ ׳’׳׳™׳×) ׳•׳׳¡׳₪׳¨׳™׳.";
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
                        ErrorMessage = "׳©׳’׳™׳׳” ׳‘׳‘׳“׳™׳§׳× ׳”׳׳©׳×׳׳©. ׳ ׳¡׳” ׳©׳•׳‘ ׳׳׳•׳—׳¨ ׳™׳•׳×׳¨.";
                        return Page();
                    }

                    if (existingUser != null)
                    {
                        ErrorMessage = "׳©׳ ׳”׳׳©׳×׳׳© ׳›׳‘׳¨ ׳§׳™׳™׳ ׳‘׳׳¢׳¨׳›׳×.";
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
                        ErrorMessage = "׳©׳’׳™׳׳” ׳‘׳”׳¨׳©׳׳”. ׳ ׳¡׳” ׳©׳•׳‘ ׳׳׳•׳—׳¨ ׳™׳•׳×׳¨.";
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
                            ErrorMessage = "׳”׳”׳¨׳©׳׳” ׳”׳¦׳׳™׳—׳” ׳׳ ׳™׳© ׳©׳’׳™׳׳” ׳‘׳©׳׳™׳¨׳× ׳”׳₪׳¨׳˜׳™׳. ׳ ׳¡׳” ׳׳”׳×׳—׳‘׳¨ ׳©׳•׳‘.";
                            return Page();
                        }

                        return RedirectToPage("/Index");
                    }

                    ErrorMessage = "׳׳™׳¨׳¢׳” ׳©׳’׳™׳׳” ׳‘׳׳”׳׳ ׳”׳”׳¨׳©׳׳”.";
                    return Page();
                }

                ErrorMessage = "׳‘׳§׳©׳” ׳׳ ׳×׳§׳™׳ ׳”.";
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unhandled error in login: {ex}");
                ErrorMessage = "׳©׳’׳™׳׳× ׳׳¢׳¨׳›׳×. ׳ ׳¡׳” ׳©׳•׳‘ ׳׳׳•׳—׳¨ ׳™׳•׳×׳¨.";
                return Page();
            }
        }
    }
}