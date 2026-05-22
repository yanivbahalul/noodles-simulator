using System;
using Microsoft.Extensions.Configuration;

namespace NoodlesSimulator.Models
{
    /// <summary>
    /// Resolves Supabase URL and API keys from Railway env vars (config + Environment).
    /// Auth uses anon/publishable keys; storage/DB services use service role when set.
    /// </summary>
    internal static class SupabaseConfiguration
    {
        public static string? Url(IConfiguration configuration)
            => First(
                Config(configuration, "SUPABASE_URL"),
                Env("SUPABASE_URL"));

        /// <summary>Low-privilege key for login / RLS-backed reads.</summary>
        public static string? AnonApiKey(IConfiguration configuration)
            => First(
                Config(configuration, "SUPABASE_ANON_KEY"),
                Config(configuration, "ANON_PUBLIC"),
                Config(configuration, "SUPABASE_KEY"),
                Env("SUPABASE_ANON_KEY"),
                Env("ANON_PUBLIC"),
                Env("SUPABASE_KEY"));

        /// <summary>Elevated key for storage and server-side DB writes.</summary>
        public static string? ServiceRoleApiKey(IConfiguration configuration)
            => First(
                Config(configuration, "SUPABASE_SERVICE_ROLE_KEY"),
                Config(configuration, "SERVICE_ROLE_SECRET"),
                Config(configuration, "SUPABASE_KEY"),
                Env("SUPABASE_SERVICE_ROLE_KEY"),
                Env("SERVICE_ROLE_SECRET"),
                Env("SUPABASE_KEY"));

        public static string Bucket(IConfiguration configuration)
            => First(
                Config(configuration, "SUPABASE_BUCKET"),
                Env("SUPABASE_BUCKET")) ?? "noodles-images";

        public static int SignedUrlTtlSeconds(IConfiguration configuration)
        {
            var ttlStr = First(
                Config(configuration, "SUPABASE_SIGNED_URL_TTL"),
                Env("SUPABASE_SIGNED_URL_TTL"));
            return int.TryParse(ttlStr, out var ttl) && ttl > 0 ? ttl : 3600;
        }

        private static string? Config(IConfiguration configuration, string key)
            => Normalize(configuration[key]);

        private static string? Env(string name)
            => Normalize(Environment.GetEnvironmentVariable(name));

        internal static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();
            if (trimmed.Length >= 2 && trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal))
                trimmed = trimmed[1..^1].Trim();

            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private static string? First(params string?[] values)
        {
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v))
                    return v;
            }
            return null;
        }
    }
}
