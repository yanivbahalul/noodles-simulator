using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class LoginPageService
{
    private readonly AuthService _authService;
    private readonly RememberMeService _rememberMe;
    private readonly ActivityEventService? _activityEvents;

    public LoginPageService(
        AuthService authService,
        RememberMeService rememberMe,
        ActivityEventService? activityEvents = null)
    {
        _authService = authService;
        _rememberMe = rememberMe;
        _activityEvents = activityEvents;
    }

    public LoginFlowResult TryOnGet(HttpContext http)
    {
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

        User? user = null;
        try
        {
            user = await _authService.AuthenticateAsync(username, password);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Authentication error for {Username}", username);
            LoginThrottle.RecordFailure(attemptKey);
            return LoginFlowResult.Error("שגיאה בהתחברות. נסה שוב מאוחר יותר.");
        }

        if (user == null)
        {
            LoginThrottle.RecordFailure(attemptKey);
            return LoginFlowResult.Error("שם המשתמש או הסיסמה שגויים.");
        }

        if (user.IsBanned)
        {
            logger.LogWarning("Banned user attempted login: {Username}", username);
            return LoginFlowResult.Error("המשתמש הזה נחסם מהמערכת.");
        }

        try
        {
            LoginThrottle.RecordSuccess(attemptKey);
            await CompleteLoginAsync(http, user);
            return LoginFlowResult.Index();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Session/cookie error after login for {Username}", username);
            return LoginFlowResult.Error("שגיאה בשמירת הפרטים. נסה שוב.");
        }
    }

    public async Task<LoginFlowResult> TryRegisterAsync(
        HttpContext http,
        string username,
        string password,
        string attemptKey,
        ILogger logger)
    {
        var validationError = LoginValidation.RegistrationError(username, password);
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
            LoginThrottle.RecordFailure(attemptKey);
            return LoginFlowResult.Error("שגיאה בבדיקת המשתמש. נסה שוב מאוחר יותר.");
        }

        if (existingUser != null)
        {
            LoginThrottle.RecordFailure(attemptKey);
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
            LoginThrottle.RecordFailure(attemptKey);
            return LoginFlowResult.Error("שגיאה בהרשמה. נסה שוב מאוחר יותר.");
        }

        if (!success)
        {
            LoginThrottle.RecordFailure(attemptKey);
            return LoginFlowResult.Error("לא ניתן להשלים הרשמה כרגע. נסה שוב מאוחר יותר.");
        }

        try
        {
            LoginThrottle.RecordSuccess(attemptKey);
            await CompleteRegistrationAsync(http, username);
            return LoginFlowResult.Index();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Session/cookie error after registration for {Username}", username);
            return LoginFlowResult.Error("ההרשמה הצליחה אך יש שגיאה בשמירת הפרטים. נסה להתחבר שוב.");
        }
    }

    private void RotateSessionForLogin(HttpContext http)
    {
        http.Session.Clear();
        RememberMeService.ClearAuthCookies(http.Response);
    }

    private async Task CompleteLoginAsync(HttpContext http, User user)
    {
        _ = _authService.TouchLastSeenAsync(user.Username, DateTime.UtcNow);
        _activityEvents?.Log(user.Username, "login");

        RotateSessionForLogin(http);
        var isAdminUser = user.IsAdmin || string.Equals(user.Username, "Admin", StringComparison.OrdinalIgnoreCase);
        http.Session.SetString("Username", user.Username);
        http.Session.SetString("IsAdmin", isAdminUser ? "1" : "0");
        _rememberMe.Set(http, user.Username, isAdminUser);
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
    public string? ErrorMessage { get; init; }

    public static LoginFlowResult Page() => new() { ShowPage = true };
    public static LoginFlowResult Index() => new() { RedirectToIndex = true };
    public static LoginFlowResult Error(string message) => new() { ShowPage = true, ErrorMessage = message };
}
