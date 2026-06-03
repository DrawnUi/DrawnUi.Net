namespace DrawnUi.Draw;

public enum LoadMoreDirection
{
    Bottom,
    Top
}

public interface IInsideViewport : IVisibilityAware, IDisposable
{
    /// <summary>
    /// Will be called when viewport containing this view has changed
    /// </summary>
    /// <param name="viewport"></param>
    public void OnViewportWasChanged(ScaledRect viewport);

    /// <summary>
    /// IInsideViewport interface: loaded is called when the view is created, but not yet visible
    /// </summary>
    void OnLoaded();

    /// <summary>
    /// Determines whether LoadMore should be triggered based on viewport position and internal measurement state.
    /// This allows the layout to make intelligent decisions about when to load more data.
    /// </summary>
    /// <param name="viewport">Current viewport rectangle</param>
    /// <returns>True if LoadMore should be triggered, false otherwise</returns>
    bool ShouldTriggerLoadMore(ScaledRect viewport);

    /// <summary>
    /// Determines whether LoadMore should be triggered for a specific edge direction.
    /// Default behavior routes to the legacy bottom-only API for backward compatibility.
    /// </summary>
    /// <param name="viewport">Current viewport rectangle</param>
    /// <param name="direction">Requested load direction</param>
    /// <returns>True if LoadMore should be triggered for the given direction</returns>
    bool ShouldTriggerLoadMore(ScaledRect viewport, LoadMoreDirection direction)
    {
        return direction == LoadMoreDirection.Bottom && ShouldTriggerLoadMore(viewport);
    }
}
