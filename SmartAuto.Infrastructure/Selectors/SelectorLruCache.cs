using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SmartAuto.Abstractions;

namespace SmartAuto.Infrastructure.Selectors;

/// <summary>
/// Thread-safe LRU cache for selector results.
/// TTL = <see cref="AppConstants.LruCacheTtlSeconds"/> seconds per entry (per-window scope).
/// Evicts stale entries on every access to keep memory bounded.
/// </summary>
internal sealed class SelectorLruCache
{
    private readonly record struct CacheKey(nint WindowHandle, string StrategyName, string CriteriaHash);
    private readonly record struct CacheEntry(SelectorResult Result, DateTime ExpiresAt);

    private readonly ConcurrentDictionary<CacheKey, CacheEntry> _cache = new();
    private readonly int _ttlSeconds;
    private readonly int _maxEntries;

    public SelectorLruCache(int ttlSeconds = Common.Constants.AppConstants.LruCacheTtlSeconds,
                            int maxEntries = Common.Constants.AppConstants.LruCacheMaxEntries)
    {
        _ttlSeconds = ttlSeconds;
        _maxEntries = maxEntries;
    }

    /// <summary>Attempts to retrieve a cached result.</summary>
    public bool TryGet(nint windowHandle, string strategyName, string criteriaHash,
                       out SelectorResult? result)
    {
        var key = new CacheKey(windowHandle, strategyName, criteriaHash);
        if (_cache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
        {
            result = entry.Result;
            return true;
        }
        result = null;
        return false;
    }

    /// <summary>Stores a result in the cache, evicting stale entries first.</summary>
    public void Set(nint windowHandle, string strategyName, string criteriaHash, SelectorResult result)
    {
        EvictStale();

        // If at capacity, remove oldest entries until we have room.
        while (_cache.Count >= _maxEntries)
        {
            var oldest = _cache.MinBy(kv => kv.Value.ExpiresAt);
            _cache.TryRemove(oldest.Key, out _);
        }

        var key = new CacheKey(windowHandle, strategyName, criteriaHash);
        _cache[key] = new CacheEntry(result, DateTime.UtcNow.AddSeconds(_ttlSeconds));
    }

    /// <summary>Removes all cache entries for a specific window (called on window close/move).</summary>
    public void InvalidateWindow(nint windowHandle)
    {
        foreach (var key in _cache.Keys.Where(k => k.WindowHandle == windowHandle).ToList())
            _cache.TryRemove(key, out _);
    }

    /// <summary>Removes all expired entries.</summary>
    public void EvictStale()
    {
        var now = DateTime.UtcNow;
        foreach (var key in _cache.Keys.ToList())
        {
            if (_cache.TryGetValue(key, out var entry) && entry.ExpiresAt <= now)
                _cache.TryRemove(key, out _);
        }
    }
}
