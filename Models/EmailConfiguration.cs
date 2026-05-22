using System;
using Microsoft.Extensions.Configuration;

namespace NoodlesSimulator.Models
{
    /// <summary>
    /// Single place for email env vars. Set each value once in Railway / shell:
    /// EMAIL_TO, Email__SmtpUser, Email__SmtpPass (legacy alias: EMAIL_SMTP_PASS).
    /// </summary>
    internal static class EmailConfiguration
    {
        public static string SmtpUser(IConfiguration configuration)
            => FirstNonEmpty(
                Env("Email__SmtpUser"),
                Config(configuration, "Email:SmtpUser"));

        public static string SmtpPass(IConfiguration configuration)
            => FirstNonEmpty(
                Env("Email__SmtpPass"),
                Env("EMAIL_SMTP_PASS"),
                Config(configuration, "Email:SmtpPass"))
               ?.Replace(" ", "") ?? string.Empty;

        public static string EmailTo(IConfiguration configuration)
            => FirstNonEmpty(Env("EMAIL_TO"), Config(configuration, "Email:To"));

        public static string EmailFrom(IConfiguration configuration)
        {
            var from = FirstNonEmpty(Env("EMAIL_FROM"), Config(configuration, "Email:From"));
            return string.IsNullOrWhiteSpace(from) ? SmtpUser(configuration) : from;
        }

        public static string EmailFromName(IConfiguration configuration)
            => FirstNonEmpty(Env("EMAIL_FROM_NAME"), Config(configuration, "Email:FromName"));

        public static string SmtpHost(IConfiguration configuration)
        {
            var host = FirstNonEmpty(
                Env("Email__SmtpHost"),
                Env("EmailSmtpHost"),
                Config(configuration, "Email:SmtpHost"),
                Config(configuration, "Email:SmtpServer"));
            return string.IsNullOrWhiteSpace(host) ? "smtp.gmail.com" : host;
        }

        public static int SmtpPort(IConfiguration configuration)
        {
            var portStr = FirstNonEmpty(
                Env("Email__SmtpPort"),
                Env("EmailSmtpPort"),
                Config(configuration, "Email:SmtpPort"));
            return int.TryParse(portStr, out var p) ? p : 587;
        }

        public static bool UseSsl(IConfiguration configuration)
        {
            var v = FirstNonEmpty(Env("Email__UseSsl"), Env("EmailUseSsl"), Config(configuration, "Email:UseSsl"));
            return string.IsNullOrWhiteSpace(v) || v.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static string BrevoApiKey() => Env("BREVO_API_KEY");

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
}
