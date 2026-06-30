using Microsoft.Extensions.Configuration;

namespace NoodlesSimulator.Models;

internal static class AdminConfiguration
{
    public static string WidgetToken(IConfiguration configuration)
        => ConfigEnv.First(
            ConfigEnv.Env("ADMIN_WIDGET_TOKEN"),
            configuration["Admin:WidgetToken"]) ?? string.Empty;
}
