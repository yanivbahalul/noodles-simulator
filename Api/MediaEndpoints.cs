using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Api;

internal static class MediaEndpoints
{
    private const string CacheControl = "public, max-age=31536000, immutable";
    private static readonly TimeSpan ServerCacheTtl = TimeSpan.FromDays(7);

    internal static void Map(WebApplication app)
    {
        app.MapGet("/media/{**path}", ServeAsync);
    }

    private static async Task ServeAsync(
        HttpContext ctx,
        string path,
        SupabaseStorageService? storage,
        IMemoryCache cache)
    {
        if (storage == null)
        {
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        if (!MediaUrl.TryNormalizePath(path, out var objectPath))
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var cacheKey = "media:" + objectPath;
        if (!cache.TryGetValue(cacheKey, out byte[]? bytes))
        {
            try
            {
                bytes = await storage.DownloadBytesAsync(objectPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Media] Download failed for {objectPath}: {ex.Message}");
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (bytes != null && bytes.Length > 0)
            {
                cache.Set(cacheKey, bytes, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ServerCacheTtl,
                    Size = bytes.Length
                });
            }
        }

        if (bytes == null || bytes.Length == 0)
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        ctx.Response.Headers.CacheControl = CacheControl;
        ctx.Response.ContentType = MediaUrl.ContentType(objectPath);
        await ctx.Response.Body.WriteAsync(bytes);
    }
}
