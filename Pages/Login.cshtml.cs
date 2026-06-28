using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using NoodlesSimulator.Services;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages;

public class LoginModel : PageModel
{
    private readonly LoginPageService _loginPage;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(LoginPageService loginPage, ILogger<LoginModel> logger)
    {
        _loginPage = loginPage;
        _logger = logger;
    }

    [BindProperty]
    public string Username { get; set; }

    [BindProperty]
    public string Password { get; set; }

    public string ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        var result = _loginPage.TryOnGet(HttpContext);
        if (result.RedirectToIndex)
            return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            Username = Username?.Trim();
            Password = Password?.Trim();

            var action = Request.Form["action"];
            var attemptKey = _loginPage.BuildAttemptKey(HttpContext, Username);

            if (LoginThrottle.IsBlocked(attemptKey))
            {
                ErrorMessage = "יותר מדי ניסיונות. נסה שוב מאוחר יותר.";
                return Page();
            }

            LoginFlowResult result;
            if (action == "login")
                result = await _loginPage.TryLoginAsync(HttpContext, Username, Password, attemptKey, _logger);
            else if (action == "register")
                result = await _loginPage.TryRegisterAsync(HttpContext, Username, Password, attemptKey, _logger);
            else
                result = LoginFlowResult.Error("בקשה לא תקינה.");

            if (result.RedirectToIndex)
                return RedirectToPage("/Index");

            ErrorMessage = result.ErrorMessage;
            return Page();
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in login");
            ErrorMessage = "שגיאת מערכת. נסה שוב מאוחר יותר.";
            return Page();
        }
    }
}
