using System;
using Microsoft.Extensions.Configuration;

namespace NoodlesSimulator.Models;

/// <summary>
/// Resolves Supabase URL and API keys from Railway env vars (config + Environment).
/// Auth uses anon/publishable keys; storage/DB services use service role when set.
/// </summary>
internal static class SupabaseConfiguration
{
    public static string? Url(IConfiguration configuration)
        => ConfigEnv.First(
            Config(configuration, "SUPABASE_URL"),
            ConfigEnv.Env("SUPABASE_URL"));

    /// <summary>Low-privilege key for login / RLS-backed reads.</summary>
    public static string? AnonApiKey(IConfiguration configuration)
        => ConfigEnv.First(
            Config(configuration, "SUPABASE_ANON_KEY"),
            Config(configuration, "ANON_PUBLIC"),
            Config(configuration, "SUPABASE_KEY"),
            ConfigEnv.Env("SUPABASE_ANON_KEY"),
            ConfigEnv.Env("ANON_PUBLIC"),
            ConfigEnv.Env("SUPABASE_KEY"));

    /// <summary>Elevated key for storage and server-side DB writes.</summary>
    public static string? ServiceRoleApiKey(IConfiguration configuration)
        => ConfigEnv.First(
            Config(configuration, "SUPABASE_SERVICE_ROLE_KEY"),
            Config(configuration, "SERVICE_ROLE_SECRET"),
            Config(configuration, "SUPABASE_KEY"),
            ConfigEnv.Env("SUPABASE_SERVICE_ROLE_KEY"),
            ConfigEnv.Env("SERVICE_ROLE_SECRET"),
            ConfigEnv.Env("SUPABASE_KEY"));

    public static string Bucket(IConfiguration configuration)
        => ConfigEnv.First(
            Config(configuration, "SUPABASE_BUCKET"),
            ConfigEnv.Env("SUPABASE_BUCKET")) ?? "noodles-images";

    public static int SignedUrlTtlSeconds(IConfiguration configuration)
    {
        var ttlStr = ConfigEnv.First(
            Config(configuration, "SUPABASE_SIGNED_URL_TTL"),
            ConfigEnv.Env("SUPABASE_SIGNED_URL_TTL"));
        return int.TryParse(ttlStr, out var ttl) && ttl > 0 ? ttl : 3600;
    }

    private static string? Config(IConfiguration configuration, string key)
        => ConfigEnv.Normalize(configuration[key]);
}
