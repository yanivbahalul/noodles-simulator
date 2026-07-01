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
    private static void MapQuestions(RouteGroupBuilder api)
    {
        api.MapGet("/question-difficulty", async (HttpContext context) =>
        {
            var svc = context.RequestServices.GetService<QuestionDifficultyService>();
            if (svc == null) return Results.Problem("Difficulty service unavailable", statusCode: 503);
            var items = await svc.GetAllQuestionsAsync();
            return Results.Json(new { items });
        });


        api.MapGet("/question-explanation", async (HttpContext context) =>
        {
            if (!await ApiHelpers.RequireAuthAsync(context)) return;

            var questionId = context.Request.Query["questionId"].ToString();
            if (string.IsNullOrWhiteSpace(questionId))
            {
                await ApiHelpers.WritePlainError(context, 400, "questionId required");
                return;
            }

            var svc = context.RequestServices.GetService<QuestionExplanationService>();
            if (svc == null || !svc.IsEnabled)
            {
                await ApiHelpers.WriteJson(context, new { hasExplanation = false, videoUrl = (string?)null });
                return;
            }

            var (hasExplanation, videoUrl) = await svc.GetVideoUrlAsync(questionId.Trim());
            await ApiHelpers.WriteJson(context, new { hasExplanation, videoUrl });
        });


        api.MapGet("/question-explanations-status", async (HttpContext context) =>
        {
            if (!await ApiHelpers.RequireAuthAdminAsync(context)) return;

            var svc = context.RequestServices.GetService<QuestionExplanationService>();
            var groups = context.RequestServices.GetService<QuestionGroupLoader>();
            if (svc == null || !svc.IsEnabled)
            {
                await ApiHelpers.WriteJson(context, new { enabled = false, summary = new { }, items = Array.Empty<object>() });
                return;
            }

            var statusFilter = context.Request.Query["status"].ToString();
            var summary = await svc.GetStatusSummaryAsync();
            var items = await svc.ListAsync(string.IsNullOrWhiteSpace(statusFilter) ? null : statusFilter, 500);
            var totalQuestions = groups != null ? (await groups.ListAllGroupsAsync()).Count : 0;

            await ApiHelpers.WriteJson(context, new
            {
                enabled = true,
                totalQuestions,
                summary = new
                {
                    ready = summary.Ready,
                    pending = summary.Pending,
                    failed = summary.Failed,
                    needsReview = summary.NeedsReview,
                    recorded = summary.Total,
                    missing = Math.Max(0, totalQuestions - summary.Total)
                },
                items = items.Select(i => new
                {
                    questionFile = i.QuestionFile,
                    questionLabel = QuestionLabel.Format(i.QuestionFile),
                    status = i.Status,
                    videoPath = i.VideoPath,
                    errorMessage = i.ErrorMessage,
                    generatedAt = i.GeneratedAt?.ToUniversalTime().ToString("o")
                })
            });
        });


        api.MapPost("/question-explanation-rating", async (HttpContext context) =>
        {
            if (!await ApiHelpers.RequireAuthAsync(context)) return;

            using var doc = await JsonDocument.ParseAsync(context.Request.Body);
            var body = doc.RootElement;
            var questionId = body.TryGetProperty("questionId", out var qEl)
                ? qEl.GetString()?.Trim() : null;
            if (string.IsNullOrWhiteSpace(questionId))
            {
                await ApiHelpers.WritePlainError(context, 400, "questionId required");
                return;
            }

            if (!body.TryGetProperty("stars", out var sEl) || !sEl.TryGetInt32(out var stars) || stars is < 1 or > 5)
            {
                await ApiHelpers.WritePlainError(context, 400, "stars must be 1-5");
                return;
            }

            var feedback = body.TryGetProperty("feedback", out var fEl) ? fEl.GetString() : null;
            var username = context.Session.GetString("Username") ?? "Unknown";
            var svc = context.RequestServices.GetService<QuestionExplanationRatingService>();
            if (svc == null || !svc.IsEnabled)
            {
                await ApiHelpers.WritePlainError(context, 503, "ratings unavailable");
                return;
            }

            var ok = await svc.SubmitAsync(questionId, username, stars, feedback);
            if (!ok)
            {
                await ApiHelpers.WritePlainError(context, 500, "save failed");
                return;
            }

            await ApiHelpers.WriteJson(context, new { ok = true });
        });


        api.MapGet("/question-explanation-ratings", async (HttpContext context) =>
        {
            if (!await ApiHelpers.RequireAuthAdminAsync(context)) return;

            var svc = context.RequestServices.GetService<QuestionExplanationRatingService>();
            if (svc == null || !svc.IsEnabled)
            {
                await ApiHelpers.WriteJson(context, new { enabled = false, items = Array.Empty<object>() });
                return;
            }

            var items = await svc.ListSummariesAsync(100);
            await ApiHelpers.WriteJson(context, new
            {
                enabled = true,
                items = items.Select(i => new
                {
                    questionFile = i.QuestionFile,
                    questionLabel = QuestionLabel.Format(i.QuestionFile),
                    avgStars = i.AvgStars,
                    ratingCount = i.RatingCount,
                    lowCount = i.LowCount,
                    urgencyScore = i.UrgencyScore,
                    recentFeedback = i.RecentFeedback
                })
            });
        });

    }
}
