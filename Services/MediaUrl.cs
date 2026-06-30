using System;
using System.IO;

namespace NoodlesSimulator.Services;

/// <summary>Stable public URLs for Supabase Storage objects (served via /media proxy).</summary>
public static class MediaUrl
{
    public static string ForStoragePath(string? objectPath)
    {
        if (string.IsNullOrWhiteSpace(objectPath))
            return string.Empty;

        return "/media/" + objectPath.Trim().TrimStart('/').Replace('\\', '/');
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
