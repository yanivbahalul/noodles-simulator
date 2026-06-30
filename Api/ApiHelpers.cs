using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NoodlesSimulator.Models;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Api;

internal static class ApiHelpers
{
    internal static bool IsAdminSession(HttpContext context) =>
        string.Equals(context.Session.GetString("IsAdmin"), "1", StringComparison.Ordinal);

    internal static bool IsAuthenticated(HttpContext context) =>
        !string.IsNullOrWhiteSpace(context.Session.GetString("Username"));

    internal static bool IsWidgetAuthorized(HttpContext context, IConfiguration config)
    {
        var expected = AdminConfiguration.WidgetToken(config);
        if (string.IsNullOrWhiteSpace(expected))
            return false;

        var auth = context.Request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = auth["Bearer ".Length..].Trim();
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(token),
                Encoding.UTF8.GetBytes(expected));
        }

        if (context.Request.Headers.TryGetValue("X-Noodles-Widget-Token", out var headerValues))
        {
            var token = headerValues.ToString();
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(token),
                Encoding.UTF8.GetBytes(expected));
        }

        return false;
    }

    internal static async Task<bool> RequireAdminAsync(HttpContext context)
    {
        if (IsAdminSession(context)) return true;
        await WritePlainError(context, 401, "Unauthorized");
        return false;
    }

    internal static async Task<bool> RequireAuthAsync(HttpContext context)
    {
        if (IsAuthenticated(context)) return true;
        await WritePlainError(context, 401, "Unauthorized");
        return false;
    }

    internal static Task WritePlainError(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsync(message);
    }

    internal static Task WriteJson(HttpContext context, object payload)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }

    internal static Task WriteServerError(HttpContext context, string prefix, Exception ex)
    {
        Console.WriteLine($"[{prefix}] {ex}");
        return WritePlainError(context, 500, "Server error");
    }

    internal static bool TryResolveAuthService(HttpContext context, out AuthService authService)
    {
        authService = context.RequestServices.GetService<AuthService>();
        return authService != null;
    }

    internal static void InvalidateDashboardCaches(IServiceProvider services)
    {
        services.GetService<DashboardDataService>()?.InvalidateCache();
        services.GetService<UserStatsService>()?.InvalidateCache();
    }

    internal static async Task RestoreUserStatsFromProgressAsync(HttpContext context, User user)
    {
        var progress = context.RequestServices.GetService<UserProgressService>();
        if (progress == null) return;

        var (total, correct) = await progress.GetAnswerTotalsAsync(user.Username);
        if (total > user.TotalAnswered)
            user.TotalAnswered = total;
        if (correct > user.CorrectAnswers)
            user.CorrectAnswers = correct;

        var data = await progress.LoadAsync(user.Username);
        if (data.Xp > user.Xp)
            user.Xp = data.Xp;
        if (user.Xp > 0)
            user.Level = QuizGamification.LevelFromXp(user.Xp);
    }
}
