using System.Collections.Concurrent;
using DrawnUi.Draw;

namespace DrawnUi.Views;

/// <summary>
/// Per-Canvas shared cache registry. Accessible via DrawnView.Cache.
/// Controls with CacheSharing=Shared store one CachedObject per type here instead of per instance.
/// </summary>
public class CacheManager
{
    private readonly DrawnView _owner;
    private readonly ConcurrentDictionary<Type, CachedObject> _shared = new();

    public CacheManager(DrawnView owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// Returns the shared CachedObject for this control type, or null if not yet created.
    /// </summary>
    public CachedObject Get(Type type)
    {
        _shared.TryGetValue(type, out var obj);
        return obj;
    }

    /// <summary>
    /// Stores a new shared cache for the given type, disposing the previous entry if different.
    /// </summary>
    public void Set(Type type, CachedObject obj)
    {
        if (_shared.TryGetValue(type, out var old) && old != null && !ReferenceEquals(old, obj))
        {
            ReturnAndDispose(old);
        }

        if (obj != null)
            _shared[type] = obj;
        else
            _shared.TryRemove(type, out _);
    }

    /// <summary>
    /// Removes and schedules disposal of the shared cache for the given type.
    /// Controls sharing this type will re-render on next draw.
    /// </summary>
    public void Free(Type type)
    {
        if (_shared.TryRemove(type, out var obj) && obj != null)
            ReturnAndDispose(obj);
    }

    /// <summary>
    /// Removes and schedules disposal of the shared cache for control type T.
    /// </summary>
    public void Free<T>() => Free(typeof(T));

    /// <summary>
    /// Removes and schedules disposal of all shared cache entries on this Canvas.
    /// </summary>
    public void Free()
    {
        foreach (var key in _shared.Keys.ToList())
            Free(key);
    }

    private void ReturnAndDispose(CachedObject obj)
    {
        if (obj.Surface != null)
        {
            _owner.ReturnSurface(obj.Surface);
            obj.Surface = null;
        }
        _owner.DisposeObject(obj);
    }

    /// <summary>
    /// Disposes all shared cache entries. Called by DrawnView on canvas disposal.
    /// </summary>
    public void Dispose() => Free();
}
