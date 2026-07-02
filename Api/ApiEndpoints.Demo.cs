using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Api;

internal static partial class ApiEndpoints
{
  private const string NormalizedPrefix = "normalized";

  private static void MapDemo(RouteGroupBuilder api)
  {
    api.MapGet("/demo/normalize-preview", async context =>
    {
      if (!await ApiHelpers.RequireAuthAdminAsync(context)) return;

      var storage = context.RequestServices.GetService<SupabaseStorageService>();
      if (storage == null)
      {
        await ApiHelpers.WritePlainError(context, 503, "Supabase Storage not available");
        return;
      }

      var originalNames = (await storage.ListFilesAsync(MediaUrl.OriginalsPrefix + "/"))
        .Where(QuestionGroupLoader.IsImageFile)
        .Select(p => Path.GetFileName(p)!)
        .Where(n => !string.IsNullOrEmpty(n))
        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
        .ToList();

      // ponytail: normalized/ grows during batch — bypass 30min list cache so demo count stays current
      var normalizedPaths = await storage.ListFilesAsync(NormalizedPrefix + "/", refresh: true);
      var normalized = new HashSet<string>(
        normalizedPaths
        .Where(QuestionGroupLoader.IsImageFile)
        .Select(p => Path.GetFileName(p)!)
        .Where(n => !string.IsNullOrEmpty(n)),
        StringComparer.OrdinalIgnoreCase);

      var groups = QuestionGroupLoader.GroupSequential(originalNames);
      var ready = groups.Where(g => g.All(normalized.Contains)).ToList();

      var questions = ready.Select((group, i) => new
      {
        index = i + 1,
        id = group[0],
        label = $"שאלה {i + 1}",
        question = MediaUrl.ForStoragePath($"{NormalizedPrefix}/{group[0]}"),
        answers = group.Skip(1).Take(4)
          .Select(n => MediaUrl.ForStoragePath($"{NormalizedPrefix}/{n}"))
          .ToArray(),
        beforeQuestion = MediaUrl.ForStoragePath($"{MediaUrl.OriginalsPrefix}/{group[0]}"),
        beforeAnswers = group.Skip(1).Take(4)
          .Select(n => MediaUrl.ForStoragePath($"{MediaUrl.OriginalsPrefix}/{n}"))
          .ToArray(),
      }).ToList();

      context.Response.Headers.CacheControl = "no-store";
      await ApiHelpers.WriteJson(context, new
      {
        count = questions.Count,
        total_groups = groups.Count,
        ready = ready.Count,
        built_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        storage_prefix = NormalizedPrefix,
        questions,
      });
    });
  }
}
