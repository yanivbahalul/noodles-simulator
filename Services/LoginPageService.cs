using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class LoginPageService
{
    private readonly AuthService _authService;
    private readonly RememberMeService _rememberMe;
    private readonly LoginThrottleService _throttle;
    private readonly AdminOtpService _adminOtp;
    private readonly IConfiguration _configuration;
    private readonly ActivityEventService? _activityEvents;

    public LoginPageService(
        AuthService authService,
        RememberMeService rememberMe,
        LoginThrottleService throttle,
        AdminOtpService adminOtp,
        IConfiguration configuration,
        ActivityEventService? activityEvents = null)
    {
        _authService = authService;
        _rememberMe = rememberMe;
        _throttle = throttle;
        _adminOtp = adminOtp;
        _configuration = configuration;
        _activityEvents = activityEvents;
    }

    public LoginFlowResult TryOnGet(HttpContext http)
    {
        if (http.Session.GetString(AdminConfiguration.PendingOtpSessionKey) == "1")
            return LoginFlowResult.AdminOtp();

        if (!string.IsNullOrEmpty(http.Session.GetString("Username")))
            return LoginFlowResult.Index();

        var restored = _rememberMe.TryRead(http.Request);
        if (restored != null)
        {
            http.Session.SetString("Username", restored.Value.Username);
            http.Session.SetString("IsAdmin", restored.Value.IsAdmin ? "1" : "0");
            return LoginFlowResult.Index();
        }

        return LoginFlowResult.Page();
    }

    public string BuildAttemptKey(HttpContext http, string? username)
    {
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        var normalizedUsername = (username ?? string.Empty).Trim().ToLowerInvariant();
        return $"{ip}:{normalizedUsername}";
    }

    public async Task<LoginFlowResult> TryLoginAsync(
        HttpContext http,
        string username,
        string password,
        string attemptKey,
        ILogger logger)
    {
        var emptyError = LoginValidation.CredentialsEmptyError(username, password);
        if (emptyError != null)
            return LoginFlowResult.Error(emptyError);

        if (AdminConfiguration.IsAdminUsername(_configuration, username))
            return await TryAdminPasswordStepAsync(http, password, attemptKey, logger);

        User? user = null;
        try
        {
            user = await _authService.AuthenticateAsync(username, password);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Authentication error for {Username}", username);
            _throttle.RecordFailure(attemptKey);
            return LoginFlowResult.Error("שגיאה בהתחברות. נסה שוב מאוחר יותר.");
        }

        if (user == null)
        {
            _throttle.RecordFailure(attemptKey);
            return LoginFlowResult.Error("שם המשתמש או הסיסמה שגויים.");
        }

        if (user.IsBanned)
        {
            logger.LogWarning("Banned user attempted login: {Username}", username);
            return LoginFlowResult.Error("המשתמש הזה נחסם מהמערכת.");
        }

        try
        {
            _throttle.RecordSuccess(attemptKey);
            await CompleteLoginAsync(http, user, isAdmin: false);
            return LoginFlowResult.Index();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Session/cookie error after login for {Username}", username);
            return LoginFlowResult.Error("שגיאה בשמירת הפרטים. נסה שוב.");
        }
    }

    public async Task<LoginFlowResult> TryVerifyAdminOtpAsync(
        HttpContext http,
        string? otpCode,
        string attemptKey,
        ILogger logger)
    {
        if (http.Session.GetString(AdminConfiguration.PendingOtpSessionKey) != "1")
            return LoginFlowResult.Error("אין התחברות ממתינה. התחבר מחדש.");

        var adminUsername = http.Session.GetString(AdminConfiguration.PendingUsernameSessionKey);
        if (string.IsNullOrWhiteSpace(adminUsername))
            return LoginFlowResult.Error("סשן לא תקין. התחבר מחדש.");

        if (string.IsNullOrWhiteSpace(otpCode))
            return LoginFlowResult.Error("הזן את קוד האימות שנשלח למייל.");

        if (!_adminOtp.Verify(http.Session.Id, otpCode))
        {
            _throttle.RecordFailure(attemptKey);
            return LoginFlowResult.Error("קוד האימות שגוי או שפג תוקפו.");
        }

        try
        {
            _throttle.RecordSuccess(attemptKey);
            var user = await _authService.GetUserAsync(adminUsername)
                       ?? new User { Username = adminUsername };
            await CompleteLoginAsync(http, user, isAdmin: true);
            return LoginFlowResult.Index();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Session/cookie error after admin OTP for {Username}", adminUsername);
            return LoginFlowResult.Error("שגיאה בשמירת הפרטים. נסה שוב.");
        }
    }

    public Task<LoginFlowResult> TryResendAdminOtpAsync(HttpContext http, ILogger logger)
    {
        if (http.Session.GetString(AdminConfiguration.PendingOtpSessionKey) != "1")
            return Task.FromResult(LoginFlowResult.Error("אין התחברות ממתינה. התחבר מחדש."));

        var (ok, error) = _adminOtp.SendOtp(http.Session.Id);
        if (!ok)
        {
            logger.LogWarning("Admin OTP resend failed: {Error}", error);
            return Task.FromResult(LoginFlowResult.Error(error ?? "לא הצלחנו לשלוח קוד אימות."));
        }

        return Task.FromResult(LoginFlowResult.AdminOtp());
    }

    public async Task<LoginFlowResult> TryRegisterAsync(
        HttpContext http,
        string username,
        string password,
        string attemptKey,
        ILogger logger)
    {
        var validationError = LoginValidation.RegistrationError(_configuration, username, password);
        if (validationError != null)
            return LoginFlowResult.Error(validationError);

        User? existingUser = null;
        try
        {
            existingUser = await _authService.GetUserAsync(username);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetUserAsync error during registration for {Username}", username);
            _throttle.RecordFailure(attemptKey);
            return LoginFlowResult.Error("שגיאה בבדיקת המשתמש. נסה שוב מאוחר יותר.");
        }

        if (existingUser != null)
        {
            _throttle.RecordFailure(attemptKey);
            return LoginFlowResult.Error("שם המשתמש כבר קיים במערכת.");
        }

        bool success;
        try
        {
            success = await _authService.RegisterAsync(username, password);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Registration error for {Username}", username);
            _throttle.RecordFailure(attemptKey);
            return LoginFlowResult.Error("שגיאה בהרשמה. נסה שוב מאוחר יותר.");
        }

        if (!success)
        {
            _throttle.RecordFailure(attemptKey);
            return LoginFlowResult.Error("לא ניתן להשלים הרשמה כרגע. נסה שוב מאוחר יותר.");
        }

        try
        {
            _throttle.RecordSuccess(attemptKey);
            await CompleteRegistrationAsync(http, username);
            return LoginFlowResult.Index();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Session/cookie error after registration for {Username}", username);
            return LoginFlowResult.Error("ההרשמה הצליחה אך יש שגיאה בשמירת הפרטים. נסה להתחבר שוב.");
        }
    }

    private async Task<LoginFlowResult> TryAdminPasswordStepAsync(
        HttpContext http,
        string password,
        string attemptKey,
        ILogger logger)
    {
        if (!AdminConfiguration.IsAdminLoginConfigured(_configuration))
        {
            logger.LogWarning("Admin login attempted but ADMIN_USERNAME/ADMIN_PASSWORD are not configured");
            _throttle.RecordFailure(attemptKey);
            return LoginFlowResult.Error("התחברות לא מוגדרת בשרת.");
        }

        if (!AdminConfiguration.VerifyPassword(_configuration, password))
        {
            _throttle.RecordFailure(attemptKey);
            return LoginFlowResult.Error("שם המשתמש או הסיסמה שגויים.");
        }

        if (!_adminOtp.CanSendOtp())
        {
            logger.LogWarning("Admin login blocked: email OTP is not configured");
            return LoginFlowResult.Error("נדרש מייל לקוד אימות (EMAIL_TO / Brevo).");
        }

        try
        {
            RotateSessionForLogin(http);
            var adminUsername = AdminConfiguration.Username(_configuration)!;
            http.Session.SetString(AdminConfiguration.PendingOtpSessionKey, "1");
            http.Session.SetString(AdminConfiguration.PendingUsernameSessionKey, adminUsername);

            var (sent, sendError) = _adminOtp.SendOtp(http.Session.Id);
            if (!sent)
            {
                ClearPendingAdmin(http);
                return LoginFlowResult.Error(sendError ?? "לא הצלחנו לשלוח קוד אימות.");
            }

            await http.Session.CommitAsync();
            _throttle.RecordSuccess(attemptKey);
            return LoginFlowResult.AdminOtp();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Admin OTP challenge setup failed");
            return LoginFlowResult.Error("שגיאה בהתחברות. נסה שוב.");
        }
    }

    private void RotateSessionForLogin(HttpContext http)
    {
        http.Session.Clear();
        RememberMeService.ClearAuthCookies(http.Response);
    }

    private void ClearPendingAdmin(HttpContext http)
    {
        http.Session.Remove(AdminConfiguration.PendingOtpSessionKey);
        http.Session.Remove(AdminConfiguration.PendingUsernameSessionKey);
        _adminOtp.Clear(http.Session.Id);
    }

    private async Task CompleteLoginAsync(HttpContext http, User user, bool isAdmin)
    {
        _ = _authService.TouchLastSeenAsync(user.Username, DateTime.UtcNow);
        _activityEvents?.Log(user.Username, "login");

        var pending = http.Session.GetString(AdminConfiguration.PendingOtpSessionKey) == "1";
        RotateSessionForLogin(http);
        if (pending)
            _adminOtp.Clear(http.Session.Id);

        http.Session.SetString("Username", user.Username);
        http.Session.SetString("IsAdmin", isAdmin ? "1" : "0");
        _rememberMe.Set(http, user.Username, isAdmin);
        await http.Session.CommitAsync();
    }

    private async Task CompleteRegistrationAsync(HttpContext http, string username)
    {
        _activityEvents?.Log(username, ActivityEventCatalog.Register);
        RotateSessionForLogin(http);
        http.Session.SetString("Username", username);
        http.Session.SetString("IsAdmin", "0");
        http.Session.SetString(WelcomePrompt.SessionKey, "1");
        _rememberMe.Set(http, username, false);
        await http.Session.CommitAsync();
    }
}

public sealed class LoginFlowResult
{
    public bool RedirectToIndex { get; init; }
    public bool ShowPage { get; init; }
    public bool ShowAdminOtp { get; init; }
    public string? ErrorMessage { get; init; }

    public static LoginFlowResult Page() => new() { ShowPage = true };
    public static LoginFlowResult Index() => new() { RedirectToIndex = true };
    public static LoginFlowResult AdminOtp() => new() { ShowPage = true, ShowAdminOtp = true };
    public static LoginFlowResult Error(string message) => new() { ShowPage = true, ErrorMessage = message };
}
