using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace NoodlesSimulator.Services;

public static class AuthCookieNames
{
    public const string Remember = ".Noodles.Remember";
    public const string LegacyUsername = "Username";
    public const string SessionV3 = ".Noodles.Session.v3";
    public const string SessionV2 = ".Noodles.Session.v2";
}

public class RememberMeService
{
    private readonly IDataProtector _protector;
    public static readonly TimeSpan Duration = TimeSpan.FromDays(30);

    public RememberMeService(IDataProtectionProvider dataProtection)
    {
        _protector = dataProtection.CreateProtector("NoodlesSimulator.RememberMe.v1");
    }

    public CookieOptions BuildOptions(HttpContext context)
    {
        var secure = context.Request.IsHttps;
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            MaxAge = Duration,
            Path = "/",
            IsEssential = true
        };
    }

    public void Set(HttpContext context, string username, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(username)) return;
        var payload = $"{username.Trim()}\t{(isAdmin ? "1" : "0")}";
        var token = _protector.Protect(payload);
        context.Response.Cookies.Append(AuthCookieNames.Remember, token, BuildOptions(context));
        // Legacy cookie for older clients / app.js logout helper
        context.Response.Cookies.Append(AuthCookieNames.LegacyUsername, username.Trim(), BuildOptions(context));
    }

    public (string Username, bool IsAdmin)? TryRead(HttpRequest request)
    {
        if (request.Cookies.TryGetValue(AuthCookieNames.Remember, out var token) && !string.IsNullOrWhiteSpace(token))
        {
            try
            {
                var payload = _protector.Unprotect(token);
                var parts = payload.Split('\t', 2);
                if (parts.Length >= 1 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    var isAdmin = parts.Length > 1 && parts[1] == "1";
                    return (parts[0], isAdmin);
                }
            }
            catch
            {
                /* tampered or key rotated */
            }
        }

        if (request.Cookies.TryGetValue(AuthCookieNames.LegacyUsername, out var legacy) && !string.IsNullOrWhiteSpace(legacy))
        {
            // ponytail: legacy cookie is username-only; admin requires the protected Remember cookie.
            return (legacy.Trim(), false);
        }

        return null;
    }

    public static void ClearAuthCookies(HttpResponse response)
    {
        response.Cookies.Delete(AuthCookieNames.Remember);
        response.Cookies.Delete(AuthCookieNames.LegacyUsername);
    }

    public static void Clear(HttpResponse response)
    {
        ClearAuthCookies(response);
        response.Cookies.Delete(AuthCookieNames.SessionV2);
        response.Cookies.Delete(AuthCookieNames.SessionV3);
    }
}
