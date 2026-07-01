using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public sealed class AdminOtpService
{
    private sealed class OtpState
    {
        public required string Code { get; init; }
        public int Attempts { get; set; }
        public DateTime SentAtUtc { get; init; }
    }

    private readonly IMemoryCache _cache;
    private readonly EmailService _email;
    private readonly IConfiguration _configuration;

    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);
    private const int MaxAttempts = 5;
    private const int CodeLength = 6;

    public AdminOtpService(IMemoryCache cache, EmailService email, IConfiguration configuration)
    {
        _cache = cache;
        _email = email;
        _configuration = configuration;
    }

    public bool CanSendOtp() =>
        _email.IsConfigured && !string.IsNullOrWhiteSpace(AdminConfiguration.OtpEmail(_configuration));

    public (bool Success, string? Error) SendOtp(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return (false, "סשן לא תקין.");

        if (!CanSendOtp())
            return (false, "שליחת קוד אימות לא מוגדרת (EMAIL_TO / Brevo).");

        var cacheKey = CacheKey(sessionId);
        if (_cache.TryGetValue(cacheKey, out OtpState? existing) && existing != null)
        {
            if (DateTime.UtcNow - existing.SentAtUtc < ResendCooldown)
                return (false, "המתן דקה לפני שליחת קוד נוסף.");
        }

        var code = GenerateCode();
        var state = new OtpState
        {
            Code = code,
            Attempts = 0,
            SentAtUtc = DateTime.UtcNow
        };
        _cache.Set(cacheKey, state, CacheEntry(CodeLifetime));

        var to = AdminConfiguration.OtpEmail(_configuration)!;
        var subject = "קוד אימות — Noodles Simulator";
        var body = $"""
            <p>קוד האימות שלך: <strong dir="ltr">{code}</strong></p>
            <p>הקוד תקף ל-{CodeLifetime.TotalMinutes:0} דקות.</p>
            <p>אם לא ביקשת להתחבר — התעלם מהודעה זו.</p>
            """;
        if (!_email.Send(subject, body))
            return (false, "לא הצלחנו לשלוח את קוד האימות. נסה שוב מאוחר יותר.");

        return (true, null);
    }

    public bool Verify(string sessionId, string? code)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(code))
            return false;

        var cacheKey = CacheKey(sessionId);
        if (!_cache.TryGetValue(cacheKey, out OtpState? state) || state == null)
            return false;

        state.Attempts++;
        if (state.Attempts > MaxAttempts)
        {
            _cache.Remove(cacheKey);
            return false;
        }

        _cache.Set(cacheKey, state, CacheEntry(CodeLifetime));

        var normalized = code.Trim();
        if (normalized.Length != CodeLength)
            return false;

        var ok = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(normalized),
            Encoding.UTF8.GetBytes(state.Code));
        if (ok)
            _cache.Remove(cacheKey);

        return ok;
    }

    public void Clear(string sessionId) => _cache.Remove(CacheKey(sessionId));

    public bool HasActiveChallenge(string sessionId) =>
        !string.IsNullOrWhiteSpace(sessionId)
        && _cache.TryGetValue(CacheKey(sessionId), out OtpState? state)
        && state != null;

    internal void SeedTestOtp(string sessionId, string code)
    {
        _cache.Set(
            CacheKey(sessionId),
            new OtpState { Code = code, Attempts = 0, SentAtUtc = DateTime.UtcNow },
            CacheEntry(CodeLifetime));
    }

    private static string CacheKey(string sessionId) => $"admin-otp:{sessionId}";

    private static MemoryCacheEntryOptions CacheEntry(TimeSpan ttl) =>
        new() { AbsoluteExpirationRelativeToNow = ttl, Size = 1 };

    private static string GenerateCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString($"D{CodeLength}");
    }
}
