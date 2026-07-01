using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NoodlesSimulator.Models;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Api;

internal static class ApiHelpers
{
    internal static bool IsAdminSession(HttpContext context, IConfiguration configuration)
    {
        if (!string.Equals(context.Session.GetString("IsAdmin"), "1", StringComparison.Ordinal))
            return false;

        return AdminConfiguration.IsAdminUsername(configuration, context.Session.GetString("Username"));
    }

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
        var config = context.RequestServices.GetRequiredService<IConfiguration>();
        if (IsAdminSession(context, config)) return true;
        await WritePlainError(context, 401, "Unauthorized");
        return false;
    }

    internal static async Task<bool> RequireAuthAsync(HttpContext context)
    {
        if (IsAuthenticated(context)) return true;
        await WritePlainError(context, 401, "Unauthorized");
        return false;
    }

    internal static async Task<bool> RequireAuthAdminAsync(HttpContext context)
    {
        var config = context.RequestServices.GetRequiredService<IConfiguration>();
        if (IsAuthenticated(context) && IsAdminSession(context, config)) return true;
        await WritePlainError(context, 403, "Forbidden");
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

    /// <summary>Validates campaignId for the logged-in user. Writes 400 and returns null on failure.</summary>
    internal static async Task<(string Username, string CampaignId)?> TryResolveActiveFeedbackCampaignAsync(
        HttpContext context,
        string? campaignId)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
        {
            await WritePlainError(context, 400, "Invalid or inactive campaign");
            return null;
        }

        var username = context.Session.GetString("Username")!;
        var progress = context.RequestServices.GetService<UserProgressService>();
        var achievementCount = progress != null
            ? (await progress.LoadAsync(username))?.Achievements?.Count ?? 0
            : 0;
        var expected = FeedbackCampaigns.GetActiveCampaignIdForUser(
            DateTime.UtcNow, IsAdminSession(context), achievementCount);
        if (string.IsNullOrWhiteSpace(expected) ||
            !string.Equals(campaignId, expected, StringComparison.Ordinal))
        {
            await WritePlainError(context, 400, "Invalid or inactive campaign");
            return null;
        }

        return (username, campaignId);
    }

    internal static async Task<UserFeedbackService?> RequireFeedbackServiceAsync(HttpContext context)
    {
        var feedbackService = context.RequestServices.GetService<UserFeedbackService>();
        if (feedbackService != null && feedbackService.IsEnabled)
            return feedbackService;

        await WritePlainError(context, 503, "Feedback service not available");
        return null;
    }

    internal static async Task<bool> HandleFeedbackWriteResultAsync(
        HttpContext context,
        bool success,
        bool alreadyResponded,
        string failureMessage)
    {
        if (alreadyResponded)
        {
            await WritePlainError(context, 409, "Already responded");
            return false;
        }

        if (!success)
        {
            await WritePlainError(context, 500, failureMessage);
            return false;
        }

        return true;
    }
}
