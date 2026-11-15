using System.Collections.Concurrent;

namespace DrawnUi.Draw;

/// <summary>
/// The memory budget of the image cache, shared by every head.
/// </summary>
public partial class SkiaImageManager
{
    /// <summary>
    /// The most megabytes of decoded pictures the cache keeps, 128 by default. When a new picture goes over it, the
    /// pictures used longest ago leave the cache. A picture a control still shows stays alive with that control and is
    /// served from it again, never decoded twice. 0 or less = no limit.
    /// </summary>
    public static int CacheBudgetMb { get; set; } = 128;

    sealed class Tracked
    {
        public long Bytes;
        public long Used;
        public DateTimeOffset ExpiresAtUtc;

        /// <summary>Set once the budget took the picture out of the cache: found again while a control keeps it.</summary>
        public WeakReference<SKBitmap> Dropped;
    }

    readonly ConcurrentDictionary<string, Tracked> _tracked = new(StringComparer.OrdinalIgnoreCase);
    readonly object _budgetLock = new();
    long _trackedBytes;
    long _useTick;

    [ThreadStatic] static bool _dropping;

    /// <summary>Megabytes of decoded pictures the cache holds now (pictures dropped for the budget not counted).</summary>
    public double CacheMemoryMb => Interlocked.Read(ref _trackedBytes) / 1048576.0;

    /// <summary>A picture went into the cache under <paramref name="key"/> (normalized).</summary>
    void OnCached(string key, SKBitmap bitmap, double longevitySecs)
    {
        if (bitmap == null || string.IsNullOrEmpty(key))
            return;

        lock (_budgetLock)
        {
            if (_tracked.TryGetValue(key, out var old) && old.Dropped == null)
                _trackedBytes -= old.Bytes;

            _tracked[key] = new Tracked
            {
                Bytes = bitmap.ByteCount,
                Used = ++_useTick,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(longevitySecs)
            };
            _trackedBytes += bitmap.ByteCount;
        }

        TrimToBudget();
    }

    void OnUsed(string key)
    {
        if (!string.IsNullOrEmpty(key) && _tracked.TryGetValue(key, out var tracked))
            tracked.Used = Interlocked.Increment(ref _useTick);
    }

    /// <summary>Taken out of the cache on purpose: never served again from what a control keeps.</summary>
    void OnRemoved(string key)
    {
        if (_dropping || string.IsNullOrEmpty(key))
            return;

        lock (_budgetLock)
        {
            if (_tracked.TryRemove(key, out var old) && old.Dropped == null)
                _trackedBytes -= old.Bytes;
        }
    }

    /// <summary>
    /// A picture the budget took out of the cache, while it has not expired and a control still keeps it alive;
    /// put back into the cache. Null otherwise.
    /// </summary>
    SKBitmap FindDropped(string key)
    {
        if (string.IsNullOrEmpty(key) || !_tracked.TryGetValue(key, out var tracked))
            return null;

        if (tracked.Dropped == null || tracked.ExpiresAtUtc <= DateTimeOffset.UtcNow
            || !tracked.Dropped.TryGetTarget(out var bitmap) || bitmap.Handle == IntPtr.Zero)
        {
            if (tracked.Dropped != null || tracked.ExpiresAtUtc <= DateTimeOffset.UtcNow)
                OnRemoved(key);
            return null;
        }

        var left = (tracked.ExpiresAtUtc - DateTimeOffset.UtcNow).TotalSeconds;
        if (!AddToCache(key, bitmap, (int)Math.Max(1, left)))
            return GetFromCacheInternal(key);
        return bitmap;
    }

    void TrimToBudget()
    {
        var budget = (long)CacheBudgetMb << 20;
        if (budget <= 0 || Interlocked.Read(ref _trackedBytes) <= budget)
            return;

        lock (_budgetLock)
        {
            // ponytail: sorts the cached pictures on every arrival over the budget, fine for some hundreds
            foreach (var (key, tracked) in _tracked.Where(x => x.Value.Dropped == null).OrderBy(x => x.Value.Used).ToList())
            {
                if (_trackedBytes <= budget)
                    break;

                var bitmap = GetCachedWithoutUse(key);
                _dropping = true;
                try
                {
                    RemoveFromCache(key);
                }
                finally
                {
                    _dropping = false;
                }

                _trackedBytes -= tracked.Bytes;
                if (bitmap != null)
                    tracked.Dropped = new WeakReference<SKBitmap>(bitmap);
                else
                    _tracked.TryRemove(key, out _);
            }

            // forget the dropped pictures nobody keeps any more
            foreach (var (key, tracked) in _tracked.Where(x => x.Value.Dropped != null).ToList())
            {
                if (!tracked.Dropped.TryGetTarget(out _) || tracked.ExpiresAtUtc <= DateTimeOffset.UtcNow)
                    _tracked.TryRemove(key, out _);
            }
        }
    }

    SKBitmap GetCachedWithoutUse(string key)
    {
        _dropping = true;
        try
        {
            return GetFromCacheInternal(key);
        }
        finally
        {
            _dropping = false;
        }
    }
}
