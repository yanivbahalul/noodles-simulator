using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Models;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
            var action = Request.Form["action"];

            if (action == "login")
            {
                var user = await _authService.Authenticate(Username, Password);
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

                var existingUser = await _authService.GetUser(Username);
                if (existingUser != null)
                {
                    ErrorMessage = "שם המשתמש כבר קיים במערכת.";
                    return Page();
                }

                var success = await _authService.Register(Username, Password);
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
    }
}
