using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class QuestionGroupLoader
{
    private readonly SupabaseStorageService? _storage;
    private List<List<string>>? _storageGroupsCache;
    private DateTime _storageGroupsCachedAt = DateTime.MinValue;
    private static readonly TimeSpan StorageGroupsTtl = TimeSpan.FromMinutes(5);
    private List<string>? _localImagesCache;
    private DateTime _localImagesCachedAt = DateTime.MinValue;
    private static readonly TimeSpan LocalImagesTtl = TimeSpan.FromMinutes(2);

    public QuestionGroupLoader(SupabaseStorageService? storage = null)
    {
        _storage = storage;
    }

    public static bool IsImageFile(string path) =>
        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);

    public async Task<List<string>> ListImageFilesAsync()
    {
        if (_storage != null)
        {
            return (await _storage.ListFilesAsync(MediaUrl.OriginalsPrefix + "/"))
                .Where(IsImageFile)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList()!;
        }

        var imagesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
        if (!Directory.Exists(imagesDir))
            return new List<string>();

        if (_localImagesCache != null && (DateTime.UtcNow - _localImagesCachedAt) < LocalImagesTtl)
            return _localImagesCache;

        _localImagesCache = Directory.GetFiles(imagesDir)
            .Where(IsImageFile)
            .Select(Path.GetFileName)!
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
        _localImagesCachedAt = DateTime.UtcNow;
        return _localImagesCache;
    }

    public async Task<List<List<string>>> ListAllGroupsAsync()
    {
        if (_storage != null && _storageGroupsCache != null && (DateTime.UtcNow - _storageGroupsCachedAt) < StorageGroupsTtl)
            return _storageGroupsCache;

        var grouped = GroupSequential(await ListImageFilesAsync());

        if (_storage != null && grouped.Count > 0)
        {
            _storageGroupsCache = grouped;
            _storageGroupsCachedAt = DateTime.UtcNow;
        }

        return grouped;
    }

    public async Task<List<List<string>>> ListGroupsForDifficultyAsync(
        string? difficulty,
        QuestionDifficultyService? difficultyService)
    {
        var allImages = await ListImageFilesAsync();
        if (string.IsNullOrEmpty(difficulty) || difficultyService == null)
            return GroupSequential(allImages);

        var allowedQuestions = await ListQuestionFilesForDifficultyAsync(difficulty, difficultyService);
        if (allowedQuestions.Count == 0)
            return GroupSequential(allImages);

        var grouped = new List<List<string>>();
        foreach (var questionFile in allowedQuestions)
        {
            if (string.IsNullOrWhiteSpace(questionFile))
                continue;

            var idx = FindImageIndex(allImages, questionFile);
            if (idx >= 0 && idx + 4 < allImages.Count)
                grouped.Add(allImages.GetRange(idx, 5));
        }

        return grouped;
    }

    public async Task<List<string>> ListQuestionFilesForDifficultyAsync(
        string difficulty,
        QuestionDifficultyService? difficultyService)
    {
        if (difficultyService != null)
        {
            var fromDb = await difficultyService.GetQuestionsByDifficultyAsync(difficulty);
            if (fromDb.Count > 0)
                return fromDb;
        }

        var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "difficulty", $"{difficulty}.json");
        if (!File.Exists(path))
            return new List<string>();

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var data = JsonSerializer.Deserialize<DifficultyConfig>(json, AppJson.Options);
            return data?.Questions ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public async Task<List<string>?> FindGroupByQuestionIdAsync(string questionId)
    {
        if (string.IsNullOrWhiteSpace(questionId))
            return null;

        return FindGroupByQuestionId(await ListImageFilesAsync(), questionId);
    }

    public static List<string>? FindGroupByQuestionId(List<string> sortedImages, string questionId)
    {
        for (var i = 0; i + 4 < sortedImages.Count; i += 5)
        {
            var group = sortedImages.GetRange(i, 5);
            if (string.Equals(group[0], questionId, StringComparison.OrdinalIgnoreCase))
                return group;
        }

        return null;
    }

    private static int FindImageIndex(List<string> allImages, string questionFile)
    {
        var idx = allImages.IndexOf(questionFile);
        if (idx >= 0)
            return idx;

        idx = allImages.FindIndex(img => string.Equals(img, questionFile, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
            return idx;

        var trimmed = questionFile.Trim();
        return allImages.FindIndex(img => img.Trim().Equals(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private static List<List<string>> GroupSequential(List<string> files)
    {
        var grouped = new List<List<string>>();
        for (var i = 0; i + 4 < files.Count; i += 5)
            grouped.Add(files.GetRange(i, 5));
        return grouped;
    }
}
