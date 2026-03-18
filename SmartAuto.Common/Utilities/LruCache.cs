using System.Collections.Concurrent;

namespace SmartAuto.Common.Utilities;

/// <summary>Thread-safe LRU cache with TTL support.</summary>
public sealed class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly TimeSpan _ttl;
    private readonly ConcurrentDictionary<TKey, CacheEntry> _dict;
    private readonly LinkedList<TKey> _order = new();
    private readonly object _lock = new();

    public LruCache(int capacity, TimeSpan ttl)
    {
        _capacity = capacity;
        _ttl = ttl;
        _dict = new ConcurrentDictionary<TKey, CacheEntry>(capacity, capacity);
    }

    public bool TryGet(TKey key, out TValue? value)
    {
        if (_dict.TryGetValue(key, out var entry) && !entry.IsExpired(_ttl))
        {
            lock (_lock)
            {
                _order.Remove(entry.Node);
                _order.AddLast(entry.Node);
            }
            value = entry.Value;
            return true;
        }
        value = default;
        return false;
    }

    public void Set(TKey key, TValue value)
    {
        lock (_lock)
        {
            if (_dict.TryGetValue(key, out var existing))
            {
                existing.Update(value);
                _order.Remove(existing.Node);
                _order.AddLast(existing.Node);
                return;
            }
            if (_dict.Count >= _capacity && _order.First is not null)
            {
                var oldest = _order.First.Value;
                _order.RemoveFirst();
                _dict.TryRemove(oldest, out _);
            }
            var node = new LinkedListNode<TKey>(key);
            _order.AddLast(node);
            _dict[key] = new CacheEntry(value, node);
        }
    }

    public void Invalidate(TKey key)
    {
        if (_dict.TryRemove(key, out var entry))
            lock (_lock) { _order.Remove(entry.Node); }
    }

    private sealed class CacheEntry(TValue value, LinkedListNode<TKey> node)
    {
        private DateTimeOffset _createdAt = DateTimeOffset.UtcNow;
        public TValue Value { get; private set; } = value;
        public LinkedListNode<TKey> Node { get; } = node;
        public bool IsExpired(TimeSpan ttl) => DateTimeOffset.UtcNow - _createdAt > ttl;
        public void Update(TValue newValue) { Value = newValue; _createdAt = DateTimeOffset.UtcNow; }
    }
}
