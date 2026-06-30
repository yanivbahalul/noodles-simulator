using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NoodlesSimulator.Models;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Api;

internal static partial class ApiEndpoints
{
    private static void MapDashboard(RouteGroupBuilder api)
    {
        api.MapGet("/dashboard-data", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            var dashboard = context.RequestServices.GetService<DashboardDataService>();
            if (dashboard == null)
            {
                await ApiHelpers.WritePlainError(context, 503, "Dashboard service not available");
                return;
            }

            var forceRefresh = context.Request.Query.ContainsKey("fresh");
            var snapshot = await dashboard.GetSnapshotAsync(forceRefresh);
            await ApiHelpers.WriteJson(context, dashboard.ToApiPayload(snapshot));
        });


        api.MapGet("/dashboard-widget", async context =>
        {
            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            if (!ApiHelpers.IsWidgetAuthorized(context, config))
            {
                await ApiHelpers.WritePlainError(context, 401, "Unauthorized");
                return;
            }

            var dashboard = context.RequestServices.GetService<DashboardDataService>();
            if (dashboard == null)
            {
                await ApiHelpers.WritePlainError(context, 503, "Dashboard service not available");
                return;
            }

            var forceRefresh = context.Request.Query.ContainsKey("fresh");
            var snapshot = await dashboard.GetSnapshotAsync(forceRefresh);
            await ApiHelpers.WriteJson(context, dashboard.ToWidgetPayload(snapshot));
        });


        api.MapGet("/dashboard-activity", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            var activity = context.RequestServices.GetService<ActivityEventService>();
            if (activity == null || !activity.IsEnabled)
            {
                await ApiHelpers.WriteJson(context, new { items = Array.Empty<object>() });
                return;
            }

            var limit = 50;
            if (int.TryParse(context.Request.Query["limit"], out var parsed) && parsed > 0 && parsed <= 100)
                limit = parsed;

            var events = await activity.GetRecentAsync(limit);
            await ApiHelpers.WriteJson(context, new
            {
                items = events.Select(e => new
                {
                    id = e.Id,
                    username = e.Username,
                    eventType = e.EventType,
                    payload = e.Payload,
                    createdAt = e.CreatedAt.ToUniversalTime().ToString("o")
                })
            });
        });


        api.MapGet("/dashboard-user", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            var username = context.Request.Query["username"].ToString();
            if (string.IsNullOrWhiteSpace(username))
            {
                await ApiHelpers.WritePlainError(context, 400, "Missing username");
                return;
            }

            var dashboard = context.RequestServices.GetService<DashboardDataService>();
            if (dashboard == null)
            {
                await ApiHelpers.WritePlainError(context, 503, "Dashboard service not available");
                return;
            }

            var detail = await dashboard.GetUserDetailAsync(username);
            if (detail == null)
            {
                await ApiHelpers.WritePlainError(context, 404, "User not found");
                return;
            }

            await ApiHelpers.WriteJson(context, dashboard.ToApiUserDetail(detail));
        });


        api.MapPost("/dashboard-user-action", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            if (!ApiHelpers.TryResolveAuthService(context, out var authService))
            {
                await ApiHelpers.WritePlainError(context, 503, "AuthService not available");
                return;
            }

            using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
            var root = doc.RootElement;
            var username = root.TryGetProperty("username", out var u) ? u.GetString() : null;
            if (string.IsNullOrWhiteSpace(username))
            {
                await ApiHelpers.WritePlainError(context, 400, "Missing username");
                return;
            }

            var user = await authService.GetUserAsync(username);
            if (user == null)
            {
                await ApiHelpers.WritePlainError(context, 404, "User not found");
                return;
            }

            var activity = context.RequestServices.GetService<ActivityEventService>();
            var adminUsername = context.Session.GetString("Username") ?? "Admin";

            if (root.TryGetProperty("isCheater", out var cheaterEl) &&
                (cheaterEl.ValueKind == System.Text.Json.JsonValueKind.True ||
                 cheaterEl.ValueKind == System.Text.Json.JsonValueKind.False))
            {
                var wasCheater = user.IsCheater;
                user.IsCheater = cheaterEl.GetBoolean();
                if (wasCheater != user.IsCheater)
                {
                    activity?.Log(
                        user.Username,
                        ActivityEventCatalog.AdminAction,
                        new Dictionary<string, object>
                        {
                            ["action"] = user.IsCheater ? "cheater_mark" : "cheater_unmark",
                            ["admin"] = adminUsername ?? "Admin"
                        });
                }
                if (wasCheater && !user.IsCheater)
                    await ApiHelpers.RestoreUserStatsFromProgressAsync(context, user);
            }

            if (root.TryGetProperty("isBanned", out var bannedEl) &&
                (bannedEl.ValueKind == System.Text.Json.JsonValueKind.True ||
                 bannedEl.ValueKind == System.Text.Json.JsonValueKind.False))
            {
                var wasBanned = user.IsBanned;
                user.IsBanned = bannedEl.GetBoolean();
                if (wasBanned != user.IsBanned)
                {
                    activity?.Log(
                        user.Username,
                        ActivityEventCatalog.AdminAction,
                        new Dictionary<string, object>
                        {
                            ["action"] = user.IsBanned ? "ban" : "unban",
                            ["admin"] = adminUsername ?? "Admin"
                        });
                }
            }

            var ok = await authService.UpdateUserAsync(user);
            if (ok)
                ApiHelpers.InvalidateDashboardCaches(context.RequestServices);

            await ApiHelpers.WriteJson(context, new { success = ok, username = user.Username, isCheater = user.IsCheater, isBanned = user.IsBanned });
        });


        api.MapPost("/dashboard-user-delete", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            var admin = context.RequestServices.GetService<AdminUserService>();
            if (admin == null)
            {
                await ApiHelpers.WritePlainError(context, 503, "Admin service not available");
                return;
            }

            using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
            var root = doc.RootElement;
            var username = root.TryGetProperty("username", out var u) ? u.GetString() : null;
            if (string.IsNullOrWhiteSpace(username))
            {
                await ApiHelpers.WritePlainError(context, 400, "Missing username");
                return;
            }

            var (success, error) = await admin.DeleteUserCompletelyAsync(username);
            if (!success)
            {
                var status = string.Equals(error, "User not found", StringComparison.Ordinal) ? 404 : 400;
                await ApiHelpers.WritePlainError(context, status, error ?? "Delete failed");
                return;
            }

            ApiHelpers.InvalidateDashboardCaches(context.RequestServices);
            await ApiHelpers.WriteJson(context, new { success = true, username = username.Trim() });
        });


        api.MapPost("/dashboard-report-status", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            var reports = context.RequestServices.GetService<QuestionReportService>();
            if (reports == null)
            {
                await ApiHelpers.WritePlainError(context, 503, "Report service not available");
                return;
            }

            using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
            var root = doc.RootElement;
            var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var status = root.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(status))
            {
                await ApiHelpers.WritePlainError(context, 400, "Missing id or status");
                return;
            }

            var ok = false;
            if (string.Equals(status, QuestionReportService.StatusResolved, StringComparison.OrdinalIgnoreCase))
                ok = reports.MarkResolved(id);
            else if (string.Equals(status, QuestionReportService.StatusOpen, StringComparison.OrdinalIgnoreCase))
                ok = reports.Reopen(id);

            if (!ok)
            {
                await ApiHelpers.WritePlainError(context, 404, "Report not found or invalid status");
                return;
            }

            ApiHelpers.InvalidateDashboardCaches(context.RequestServices);
            await ApiHelpers.WriteJson(context, new { success = true, id, status = status.ToLowerInvariant() });
        });


        api.MapPost("/dashboard-user-reset", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            var admin = context.RequestServices.GetService<AdminUserService>();
            if (admin == null)
            {
                await ApiHelpers.WritePlainError(context, 503, "Admin service not available");
                return;
            }

            using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
            var username = doc.RootElement.TryGetProperty("username", out var u) ? u.GetString() : null;
            if (string.IsNullOrWhiteSpace(username))
            {
                await ApiHelpers.WritePlainError(context, 400, "Missing username");
                return;
            }

            var (success, error) = await admin.ResetUserProgressAsync(username);
            if (!success)
            {
                var status = string.Equals(error, "User not found", StringComparison.Ordinal) ? 404 : 400;
                await ApiHelpers.WritePlainError(context, status, error ?? "Reset failed");
                return;
            }

            ApiHelpers.InvalidateDashboardCaches(context.RequestServices);
            await ApiHelpers.WriteJson(context, new { success = true, username = username.Trim() });
        });


        api.MapPost("/dashboard-exam-expire", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            var admin = context.RequestServices.GetService<AdminUserService>();
            if (admin == null)
            {
                await ApiHelpers.WritePlainError(context, 503, "Admin service not available");
                return;
            }

            using var doc = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body);
            var token = doc.RootElement.TryGetProperty("token", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(token))
            {
                await ApiHelpers.WritePlainError(context, 400, "Missing token");
                return;
            }

            var (success, error) = await admin.ExpireExamAsync(token);
            if (!success)
            {
                var status = string.Equals(error, "Exam not found", StringComparison.Ordinal) ? 404 : 400;
                await ApiHelpers.WritePlainError(context, status, error ?? "Expire failed");
                return;
            }

            ApiHelpers.InvalidateDashboardCaches(context.RequestServices);
            await ApiHelpers.WriteJson(context, new { success = true, token = token.Trim() });
        });


        api.MapGet("/dashboard-feedback", async context =>
        {
            if (!await ApiHelpers.RequireAdminAsync(context)) return;

            var feedbackService = await ApiHelpers.RequireFeedbackServiceAsync(context);
            if (feedbackService == null) return;

            var entries = await feedbackService.GetSubmittedFeedbackAsync();
            await ApiHelpers.WriteJson(context, new
            {
                campaignId = FeedbackCampaigns.MilestoneCampaignPrefix,
                entries = entries.Select(e => new
                {
                    e.Id,
                    e.Username,
                    e.Rating,
                    e.Message,
                    e.Milestone,
                    isLater = e.IsLater,
                    createdAt = e.CreatedAt.ToString("o")
                })
            });
        });

    }
}
