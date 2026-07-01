using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using NoodlesSimulator.Models;
using NoodlesSimulator.Services;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages;

public class LoginModel : PageModel
{
    private readonly LoginPageService _loginPage;
    private readonly LoginThrottleService _throttle;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(LoginPageService loginPage, LoginThrottleService throttle, ILogger<LoginModel> logger)
    {
        _loginPage = loginPage;
        _throttle = throttle;
        _logger = logger;
    }

    [BindProperty]
    public string Username { get; set; }

    [BindProperty]
    public string Password { get; set; }

    [BindProperty]
    public string OtpCode { get; set; }

    public string ErrorMessage { get; set; }
    public bool ShowAdminOtp { get; set; }

    public IActionResult OnGet()
    {
        var result = _loginPage.TryOnGet(HttpContext);
        return FromResult(result);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            Username = Username?.Trim();
            Password = Password?.Trim();
            OtpCode = OtpCode?.Trim();

            var action = Request.Form["action"];
            var attemptKey = _loginPage.BuildAttemptKey(HttpContext, Username);

            if (_throttle.IsBlocked(attemptKey))
            {
                ErrorMessage = "יותר מדי ניסיונות. נסה שוב מאוחר יותר.";
                return Page();
            }

            LoginFlowResult result;
            if (action == "login")
                result = await _loginPage.TryLoginAsync(HttpContext, Username, Password, attemptKey, _logger);
            else if (action == "register")
                result = await _loginPage.TryRegisterAsync(HttpContext, Username, Password, attemptKey, _logger);
            else if (action == "verify-admin-otp")
                result = await _loginPage.TryVerifyAdminOtpAsync(HttpContext, OtpCode, attemptKey, _logger);
            else if (action == "resend-admin-otp")
                result = await _loginPage.TryResendAdminOtpAsync(HttpContext, _logger);
            else if (action == "cancel-admin-otp")
                result = await _loginPage.TryCancelAdminOtpAsync(HttpContext);
            else
                result = LoginFlowResult.Error("בקשה לא תקינה.");

            return FromResult(result);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in login");
            ErrorMessage = "שגיאת מערכת. נסה שוב מאוחר יותר.";
            return Page();
        }
    }

    private IActionResult FromResult(LoginFlowResult result)
    {
        if (result.RedirectToIndex)
            return RedirectToPage("/Index");

        ShowAdminOtp = result.ShowAdminOtp
            || HttpContext.Session.GetString(AdminConfiguration.PendingOtpSessionKey) == "1";
        ErrorMessage = result.ErrorMessage;
        return Page();
    }
}
