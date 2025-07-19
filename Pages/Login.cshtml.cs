using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Models;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System;

namespace NoodlesSimulator.Pages
{
    public class LoginModel : PageModel
    {
        private readonly AuthService _authService;

        public LoginModel(AuthService authService)
        {
            _authService = authService;
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
                    User user = null;
                    try { user = await _authService.Authenticate(Username, Password); } catch (Exception ex) { Console.WriteLine($"[Login Authenticate Error] {ex}"); }
                    if (user != null)
                    {
                        if (user.IsBanned)
                        {
                            ErrorMessage = "🚫 המשתמש הזה נחסם מהמערכת.";
                            return Page();
                        }

                        HttpContext.Session.SetString("Username", user.Username);
                        Response.Cookies.Append("Username", user.Username);
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
                    try { existingUser = await _authService.GetUser(Username); } catch (Exception ex) { Console.WriteLine($"[Login GetUser Error] {ex}"); }
                    if (existingUser != null)
                    {
                        ErrorMessage = "שם המשתמש כבר קיים במערכת.";
                        return Page();
                    }

                    bool success = false;
                    try { success = await _authService.Register(Username, Password); } catch (Exception ex) { Console.WriteLine($"[Login Register Error] {ex}"); }
                    if (success)
                    {
                        HttpContext.Session.SetString("Username", Username);
                        Response.Cookies.Append("Username", Username);
                        return RedirectToPage("/Index");
                    }

                    ErrorMessage = "אירעה שגיאה במהלך ההרשמה.";
                    return Page();
                }

                ErrorMessage = "בקשה לא תקינה.";
                return Page();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Login OnPostAsync Error] {ex}");
                ErrorMessage = $"שגיאת שרת: {ex.Message}";
                return Page();
            }
        }
    }
}
