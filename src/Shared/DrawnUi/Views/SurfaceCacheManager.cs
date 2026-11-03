using System.Collections.Concurrent;

namespace DrawnUi.Views
{
    public class SurfaceCacheManager
    {
        private readonly ConcurrentDictionary<SurfaceKey, SurfacePool> _pools = new();
        private readonly ConcurrentDictionary<SurfaceKey, SurfaceStats> _statistics = new();
        private readonly IDisposeManager _disposeManager;

        // Configuration
        private readonly int _minRequestsForPooling = 1;
        private readonly int _maxPoolSize = 10;
        private readonly int _maxTrackedSizes = 100; // Limit dictionary growth
        // Pixels kept for reuse, all sizes together: a resized window leaves its old sizes behind, each with pooled
        // window-sized surfaces nobody asks for again, so the least recently requested sizes go first
        private readonly long _maxPooledBytes = 128L * 1024 * 1024;
        private long _pooledBytes;
        private readonly object _cleanupLock = new object();

        /// <summary>Memory of the surfaces kept for reuse, all sizes together, in bytes.</summary>
        public long PooledBytes => Interlocked.Read(ref _pooledBytes);

        public SurfaceCacheManager(IDisposeManager disposeManager)
        {
            _disposeManager = disposeManager;
        }

        public SKSurface GetSurface(int width, int height)
        {
            var key = new SurfaceKey(width, height);

            // Update statistics and check if we should start pooling
            var shouldPool = UpdateStatisticsAndCheckPooling(key);

            // If we should pool, try to get from pool
            if (shouldPool && _pools.TryGetValue(key, out var pool))
            {
                if (pool.TryTake(out var pooledSurface))
                {
                    Interlocked.Add(ref _pooledBytes, -key.Bytes);
                    //Debug.WriteLine($"Reused surface {key.Width}x{key.Height}");
                    return pooledSurface;
                }
            }

            // Create new surface
            var cacheSurfaceInfo = new SKImageInfo(width, height);
            return SKSurface.Create(cacheSurfaceInfo);
        }

        public void ReturnSurface(SKSurface surface)
        {
            if (surface == null) return;

            var key = new SurfaceKey(surface.Canvas.DeviceClipBounds.Width, surface.Canvas.DeviceClipBounds.Height);

            // Check if we should pool this surface
            if (ShouldPool(key))
            {
                var pool = _pools.GetOrAdd(key, k => new SurfacePool(_maxPoolSize));

                if (FreeBudgetFor(key))
                {
                    // Clear surface for reuse
                    surface.Canvas.Clear();

                    if (pool.TryAdd(surface))
                    {
                        Interlocked.Add(ref _pooledBytes, key.Bytes);
                        return; // Successfully pooled
                    }
                }
            }

            // Send to dispose manager if not pooled
            _disposeManager.DisposeObject(surface);
        }

        // Room for one more surface of this size within the budget, made by dropping the pools of the sizes requested
        // longest ago. False when it can't fit.
        private bool FreeBudgetFor(SurfaceKey key)
        {
            if (Interlocked.Read(ref _pooledBytes) + key.Bytes <= _maxPooledBytes)
                return true;
            if (key.Bytes > _maxPooledBytes)
                return false;

            lock (_cleanupLock)
            {
                var oldest = _pools
                    .Where(kvp => !kvp.Key.Equals(key))
                    .OrderBy(kvp => _statistics.TryGetValue(kvp.Key, out var stats) ? stats.LastRequested : DateTime.MinValue)
                    .Select(kvp => kvp.Key)
                    .ToList();
                foreach (var size in oldest)
                {
                    if (Interlocked.Read(ref _pooledBytes) + key.Bytes <= _maxPooledBytes)
                        break;
                    if (_pools.TryRemove(size, out var pool))
                        DisposePool(size, pool);
                }
            }
            return Interlocked.Read(ref _pooledBytes) + key.Bytes <= _maxPooledBytes;
        }

        private void DisposePool(SurfaceKey key, SurfacePool pool)
        {
            var count = pool.Dispose(_disposeManager);
            Interlocked.Add(ref _pooledBytes, -key.Bytes * count);
        }

        private bool UpdateStatisticsAndCheckPooling(SurfaceKey key)
        {
            // Check if we need to cleanup before adding new entries
            if (_statistics.Count >= _maxTrackedSizes)
            {
                CleanupOldEntries();
            }

            var stats = _statistics.AddOrUpdate(key,
                new SurfaceStats { RequestCount = 1, LastRequested = DateTime.UtcNow },
                (k, existingStats) =>
                {
                    existingStats.RequestCount++;
                    existingStats.LastRequested = DateTime.UtcNow;
                    return existingStats;
                });

            // Return true if we've hit the threshold for pooling
            return stats.RequestCount >= _minRequestsForPooling;
        }

        private bool ShouldPool(SurfaceKey key)
        {
            return _statistics.TryGetValue(key, out var stats) &&
                   stats.RequestCount >= _minRequestsForPooling;
        }

        private void CleanupOldEntries()
        {
            lock (_cleanupLock)
            {
                // Double-check after acquiring lock
                if (_statistics.Count < _maxTrackedSizes)
                    return;

                // Remove oldest 25% of entries
                var entriesToRemove = _statistics.Count / 4;
                var oldestEntries = _statistics
                    .OrderBy(kvp => kvp.Value.LastRequested)
                    .Take(entriesToRemove)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in oldestEntries)
                {
                    _statistics.TryRemove(key, out _);

                    // Also remove the pool if it exists
                    if (_pools.TryRemove(key, out var pool))
                    {
                        DisposePool(key, pool);
                    }
                }
            }
        }

        public void Dispose()
        {
            // Dispose all pools
            foreach (var pool in _pools.Values)
            {
                pool.Dispose(_disposeManager);
            }

            _pools.Clear();
            Interlocked.Exchange(ref _pooledBytes, 0);
            _statistics.Clear();
        }
    }

    public struct SurfaceKey : IEquatable<SurfaceKey>
    {
        public readonly int Width;
        public readonly int Height;

        public SurfaceKey(int width, int height)
        {
            Width = width;
            Height = height;
        }

        /// <summary>The memory of a surface this size, 4 bytes a pixel.</summary>
        public long Bytes => 4L * Width * Height;

        public bool Equals(SurfaceKey other)
        {
            return Width == other.Width && Height == other.Height;
        }

        public override bool Equals(object obj)
        {
            return obj is SurfaceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Width, Height);
        }
    }

    public class SurfaceStats
    {
        public int RequestCount { get; set; }
        public DateTime LastRequested { get; set; }
    }

    public class SurfacePool
    {
        private readonly ConcurrentQueue<SKSurface> _surfaces = new();
        private readonly int _maxSize;
        private int _currentSize;

        public SurfacePool(int maxSize)
        {
            _maxSize = maxSize;
        }

        public bool TryTake(out SKSurface surface)
        {
            if (_surfaces.TryDequeue(out surface))
            {
                Interlocked.Decrement(ref _currentSize);
                return true;
            }

            surface = null;
            return false;
        }

        public bool TryAdd(SKSurface surface)
        {
            if (_currentSize >= _maxSize)
            {
                return false;
            }

            _surfaces.Enqueue(surface);
            Interlocked.Increment(ref _currentSize);
            return true;
        }

        /// <summary>Disposes the pooled surfaces and returns how many there were.</summary>
        public int Dispose(IDisposeManager disposeManager)
        {
            var count = 0;
            while (_surfaces.TryDequeue(out var surface))
            {
                disposeManager.DisposeObject(surface);
                count++;
            }
            _currentSize = 0;
            return count;
        }
    }
}
