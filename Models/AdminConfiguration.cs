using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace NoodlesSimulator.Models;

internal static class AdminConfiguration
{
    public const string PendingOtpSessionKey = "PendingAdminOtp";
    public const string PendingUsernameSessionKey = "PendingAdminUsername";

    public static string WidgetToken(IConfiguration configuration)
        => ConfigEnv.First(
            ConfigEnv.Env("ADMIN_WIDGET_TOKEN"),
            configuration["Admin:WidgetToken"]) ?? string.Empty;

    public static string? Username(IConfiguration configuration)
        => ConfigEnv.First(
            ConfigEnv.Env("ADMIN_USERNAME"),
            configuration["Admin:Username"]);

    public static string? Password(IConfiguration configuration)
        => ConfigEnv.First(
            ConfigEnv.Env("ADMIN_PASSWORD"),
            configuration["Admin:Password"]);

    public static string? OtpEmail(IConfiguration configuration)
        => ConfigEnv.First(
            ConfigEnv.Env("ADMIN_OTP_EMAIL"),
            ConfigEnv.Env("EMAIL_TO"),
            configuration["Admin:OtpEmail"],
            configuration["Email:To"]);

    public static bool IsAdminUsername(IConfiguration configuration, string? username)
    {
        var admin = Username(configuration);
        if (string.IsNullOrWhiteSpace(admin) || string.IsNullOrWhiteSpace(username))
            return false;

        return string.Equals(username.Trim(), admin.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsReservedUsername(IConfiguration configuration, string? username) =>
        IsAdminUsername(configuration, username);

    public static bool IsAdminLoginConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(Username(configuration))
        && !string.IsNullOrWhiteSpace(Password(configuration));

    public static bool IsAdminSession(IConfiguration configuration, string? username, string? isAdminFlag) =>
        string.Equals(isAdminFlag, "1", StringComparison.Ordinal)
        && IsAdminUsername(configuration, username);

    public static bool VerifyPassword(IConfiguration configuration, string password)
    {
        var expected = Password(configuration);
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(password))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(password.Trim()),
            Encoding.UTF8.GetBytes(expected.Trim()));
    }
}
