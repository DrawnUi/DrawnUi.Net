namespace DrawnUi.Draw;

/// <summary>
/// Controls whether a control shares its cache with other instances of the same type on the same Canvas.
/// Only effective when UseCache is Image or GPU.
/// </summary>
public enum CacheSharingType
{
    /// <summary>
    /// Each instance maintains its own independent cache (default).
    /// </summary>
    Default,

    /// <summary>
    /// All instances of the same control type on the same Canvas share a single CachedObject.
    /// The first instance to render creates the cache; others reuse it directly.
    /// Disposing an individual control does not release the shared cache.
    /// Use SuperView.Cache.Free&lt;T&gt;() to release explicitly, or let the Canvas dispose it.
    /// </summary>
    Shared
}
