using System;
using Microsoft.Extensions.Configuration;

namespace NoodlesSimulator.Models;

internal static class AdminConfiguration
{
    public static string WidgetToken(IConfiguration configuration)
        => FirstNonEmpty(
            Env("ADMIN_WIDGET_TOKEN"),
            Config(configuration, "Admin:WidgetToken")) ?? string.Empty;

    private static string? Env(string name) => Environment.GetEnvironmentVariable(name);

    private static string? Config(IConfiguration configuration, string key)
        => configuration[key];

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }
        return null;
    }
}
