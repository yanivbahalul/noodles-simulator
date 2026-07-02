using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NoodlesSimulator.Services;

/// <summary>Persistent on-disk cache for Supabase Storage objects served via /media.</summary>
public sealed class MediaDiskCache
{
    private readonly string _root;

    public MediaDiskCache(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("root is required.", nameof(root));

        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    public static string DefaultRoot(bool isProd) =>
        isProd
            ? "/data-keys/media-cache"
            : Path.Combine(Directory.GetCurrentDirectory(), "data-keys", "media-cache");

    public bool TryGetFilePath(string objectPath, out string filePath)
    {
        filePath = ResolveFilePath(objectPath);
        if (filePath.Length == 0)
            return false;

        if (!File.Exists(filePath))
            return false;

        try
        {
            return new FileInfo(filePath).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task WriteAsync(string objectPath, byte[] bytes, CancellationToken cancellationToken = default)
    {
        if (bytes == null || bytes.Length == 0)
            return;

        var dest = ResolveFilePath(objectPath);
        if (dest.Length == 0)
            throw new ArgumentException("Invalid object path.", nameof(objectPath));

        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        var tmp = dest + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(tmp, bytes, cancellationToken);
            File.Move(tmp, dest, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp))
            {
                try { File.Delete(tmp); }
                catch { /* ponytail: best-effort temp cleanup */ }
            }
        }
    }

    private string ResolveFilePath(string objectPath)
    {
        if (!MediaUrl.TryNormalizePath(objectPath, out var normalized))
            return string.Empty;

        var relative = normalized.Replace('\\', '/').TrimStart('/');
        if (relative.Contains("..", StringComparison.Ordinal))
            return string.Empty;

        var parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return string.Empty;

        var combined = _root;
        foreach (var part in parts)
        {
            if (part is "." or "..")
                return string.Empty;
            combined = Path.Combine(combined, part);
        }

        var full = Path.GetFullPath(combined);
        var rootWithSep = _root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSep, StringComparison.Ordinal) && !string.Equals(full, _root, StringComparison.Ordinal))
            return string.Empty;

        return full;
    }
}
