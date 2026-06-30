using System;
using Microsoft.Extensions.Configuration;

namespace NoodlesSimulator.Models;

/// <summary>
/// Single place for email env vars. Set each value once in Railway / shell:
/// EMAIL_TO, Email__SmtpUser, Email__SmtpPass.
/// </summary>
internal static class EmailConfiguration
{
    public static string SmtpUser(IConfiguration configuration)
        => ConfigEnv.First(
            ConfigEnv.Env("Email__SmtpUser"),
            configuration["Email:SmtpUser"]);

    public static string SmtpPass(IConfiguration configuration)
        => ConfigEnv.First(
            ConfigEnv.Env("Email__SmtpPass"),
            configuration["Email:SmtpPass"])
           ?.Replace(" ", "") ?? string.Empty;

    public static string EmailTo(IConfiguration configuration)
        => ConfigEnv.First(ConfigEnv.Env("EMAIL_TO"), configuration["Email:To"]);

    public static string EmailFrom(IConfiguration configuration)
    {
        var from = ConfigEnv.First(ConfigEnv.Env("EMAIL_FROM"), configuration["Email:From"]);
        return string.IsNullOrWhiteSpace(from) ? SmtpUser(configuration) : from;
    }

    public static string EmailFromName(IConfiguration configuration)
        => ConfigEnv.First(ConfigEnv.Env("EMAIL_FROM_NAME"), configuration["Email:FromName"]);

    public static string SmtpHost(IConfiguration configuration)
    {
        var host = ConfigEnv.First(
            ConfigEnv.Env("Email__SmtpHost"),
            ConfigEnv.Env("EmailSmtpHost"),
            configuration["Email:SmtpHost"],
            configuration["Email:SmtpServer"]);
        return string.IsNullOrWhiteSpace(host) ? "smtp.gmail.com" : host;
    }

    public static int SmtpPort(IConfiguration configuration)
    {
        var portStr = ConfigEnv.First(
            ConfigEnv.Env("Email__SmtpPort"),
            ConfigEnv.Env("EmailSmtpPort"),
            configuration["Email:SmtpPort"]);
        return int.TryParse(portStr, out var p) ? p : 587;
    }

    public static bool UseSsl(IConfiguration configuration)
    {
        var v = ConfigEnv.First(
            ConfigEnv.Env("Email__UseSsl"),
            ConfigEnv.Env("EmailUseSsl"),
            configuration["Email:UseSsl"]);
        return string.IsNullOrWhiteSpace(v) || v.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public static string BrevoApiKey() => ConfigEnv.Env("BREVO_API_KEY");
}
