using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Api;

internal static class MediaEndpoints
{
    private const string CacheControl = "public, max-age=31536000, immutable";

    internal static void Map(WebApplication app)
    {
        app.MapGet("/media/{**path}", ServeAsync);
    }

    private static async Task ServeAsync(
        HttpContext ctx,
        string path,
        SupabaseStorageService? storage,
        MediaDiskCache? diskCache)
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

        var contentType = MediaUrl.ContentType(objectPath);

        if (diskCache != null && diskCache.TryGetFilePath(objectPath, out var cachedPath))
        {
            await WriteFileAsync(ctx, cachedPath, contentType);
            return;
        }

        byte[] bytes;
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

        if (bytes == null || bytes.Length == 0)
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (diskCache != null)
        {
            try
            {
                await diskCache.WriteAsync(objectPath, bytes, ctx.RequestAborted);
                if (diskCache.TryGetFilePath(objectPath, out cachedPath))
                {
                    await WriteFileAsync(ctx, cachedPath, contentType);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Media] Disk cache write failed for {objectPath}: {ex.Message}");
            }
        }

        ctx.Response.Headers.CacheControl = CacheControl;
        ctx.Response.ContentType = contentType;
        await ctx.Response.Body.WriteAsync(bytes, ctx.RequestAborted);
    }

    private static Task WriteFileAsync(HttpContext ctx, string filePath, string contentType)
    {
        ctx.Response.Headers.CacheControl = CacheControl;
        ctx.Response.ContentType = contentType;
        // ponytail: SendFile streams from disk; enables Range for video without RAM buffer
        return ctx.Response.SendFileAsync(filePath);
    }
}
