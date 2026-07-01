using System;
using Microsoft.Extensions.Caching.Memory;

namespace NoodlesSimulator.Services;

public sealed class LoginThrottleService
{
    private sealed class AttemptState
    {
        public int Failures { get; set; }
        public DateTime LastFailureUtc { get; set; }
        public DateTime? BlockedUntilUtc { get; set; }
    }

    private readonly IMemoryCache _cache;
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan BlockDuration = TimeSpan.FromMinutes(15);
    private const int MaxFailuresBeforeBlock = 8;

    public LoginThrottleService(IMemoryCache cache) => _cache = cache;

    public bool IsBlocked(string key)
    {
        if (!_cache.TryGetValue(key, out AttemptState? state) || state == null)
            return false;

        if (state.BlockedUntilUtc.HasValue && state.BlockedUntilUtc.Value > DateTime.UtcNow)
            return true;

        if (DateTime.UtcNow - state.LastFailureUtc > AttemptWindow)
        {
            _cache.Remove(key);
            return false;
        }

        return false;
    }

    public void RecordFailure(string key)
    {
        var now = DateTime.UtcNow;
        var ttl = AttemptWindow + BlockDuration;
        var state = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ttl;
            entry.Size = 1;
            return new AttemptState();
        })!;
        _cache.Set(key, state, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Size = 1
        });

        if (now - state.LastFailureUtc > AttemptWindow)
        {
            state.Failures = 1;
            state.BlockedUntilUtc = null;
        }
        else
        {
            state.Failures++;
        }

        state.LastFailureUtc = now;
        if (state.Failures >= MaxFailuresBeforeBlock)
            state.BlockedUntilUtc = now.Add(BlockDuration);
    }

    public void RecordSuccess(string key) => _cache.Remove(key);
}
