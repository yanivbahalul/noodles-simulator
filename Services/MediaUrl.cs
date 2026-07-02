using System;
using System.IO;

namespace NoodlesSimulator.Services;

/// <summary>Stable public URLs for Supabase Storage objects (served via /media proxy).</summary>
public static class MediaUrl
{
    /// <summary>Quiz screenshot originals (mirrored from former bucket root).</summary>
    public const string OriginalsPrefix = "original";

    /// <summary>Quiz screenshots after normalize-questions batch (served to users).</summary>
    public const string NormalizedPrefix = "normalized";

    /// <summary>Bare filename → storage path; paths with a folder (explanations/, sessions/) unchanged.</summary>
    public static string ResolveObjectPath(string? fileOrPath)
    {
        if (string.IsNullOrWhiteSpace(fileOrPath))
            return string.Empty;

        var p = fileOrPath.Trim().TrimStart('/').Replace('\\', '/');
        if (p.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Invalid path", nameof(fileOrPath));

        return p.Contains('/') ? p : $"{NormalizedPrefix}/{p}";
    }

    public static string ForStoragePath(string? objectPath)
    {
        var resolved = ResolveObjectPath(objectPath);
        return string.IsNullOrWhiteSpace(resolved) ? string.Empty : "/media/" + resolved;
    }

    public static bool TryNormalizePath(string? path, out string objectPath)
    {
        objectPath = "";
        if (string.IsNullOrWhiteSpace(path))
            return false;

        path = path.Replace('\\', '/').TrimStart('/');
        if (path.Contains("..", StringComparison.Ordinal))
            return false;

        objectPath = path;
        return true;
    }

    public static string ContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }
}
