using System;
using Microsoft.Extensions.Configuration;

namespace NoodlesSimulator.Models;

internal static class ConfigEnv
{
    internal static string? Env(string name) => Environment.GetEnvironmentVariable(name);

    internal static string? Config(IConfiguration configuration, string key) => configuration[key];

    internal static string? First(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }
        return null;
    }

    internal static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal))
            trimmed = trimmed[1..^1].Trim();

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
