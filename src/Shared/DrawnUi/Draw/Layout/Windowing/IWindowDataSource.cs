namespace DrawnUi.Draw;

/// <summary>
/// The data backing a <see cref="WindowedSource{T}"/> — typically a remote API or a local DB. The
/// windowed source NEVER holds the full collection; it pulls bounded ranges from here on demand, in both
/// directions, and to jump. Implementations should be safe to call off the UI thread.
///
/// Ranges are requested and returned in NATURAL ascending GLOBAL order (index 0 = oldest); the windowed
/// source owns any inversion (newest-first) for display.
/// </summary>
public interface IWindowDataSource<T>
{
    /// <summary>Total item count (history length). Read once at init; live appends grow it locally.</summary>
    Task<int> GetCountAsync(CancellationToken cancel = default);

    /// <summary>Items for global indices [<paramref name="from"/>, <paramref name="from"/> + <paramref name="count"/>),
    /// ascending. May simulate/incur latency — the windowed source shows a loading state while it awaits.</summary>
    Task<IReadOnlyList<T>> GetRangeAsync(int from, int count, CancellationToken cancel = default);
}
