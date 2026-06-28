using System;
using System.Collections.Concurrent;

namespace NoodlesSimulator.Services;

public static class LoginThrottle
{
    private class AttemptState
    {
        public int Failures { get; set; }
        public DateTime LastFailureUtc { get; set; }
        public DateTime? BlockedUntilUtc { get; set; }
    }

    private static readonly ConcurrentDictionary<string, AttemptState> Attempts = new();
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan BlockDuration = TimeSpan.FromMinutes(15);
    private const int MaxFailuresBeforeBlock = 8;

    public static bool IsBlocked(string key)
    {
        if (!Attempts.TryGetValue(key, out var state))
            return false;

        if (state.BlockedUntilUtc.HasValue && state.BlockedUntilUtc.Value > DateTime.UtcNow)
            return true;

        if (DateTime.UtcNow - state.LastFailureUtc > AttemptWindow)
            Attempts.TryRemove(key, out _);

        return false;
    }

    public static void RecordFailure(string key)
    {
        var now = DateTime.UtcNow;
        Attempts.AddOrUpdate(
            key,
            _ => new AttemptState { Failures = 1, LastFailureUtc = now },
            (_, existing) =>
            {
                if (now - existing.LastFailureUtc > AttemptWindow)
                {
                    existing.Failures = 1;
                    existing.BlockedUntilUtc = null;
                }
                else
                {
                    existing.Failures++;
                }

                existing.LastFailureUtc = now;
                if (existing.Failures >= MaxFailuresBeforeBlock)
                    existing.BlockedUntilUtc = now.Add(BlockDuration);
                return existing;
            });
    }

    public static void RecordSuccess(string key) => Attempts.TryRemove(key, out _);
}
