# Recycled Cells: Advanced Performance Techniques

Recycled cells are a powerful performance optimization technique in DrawnUI that allows you to reuse cell instances when scrolling through large lists. This dramatically reduces memory usage and improves scrolling performance by avoiding the creation and destruction of UI elements.

## What Are Recycled Cells?

When you scroll through a list with hundreds or thousands of items, creating a new cell for each item would be extremely inefficient. Instead, DrawnUI creates a small pool of cell instances and reuses them as items scroll in and out of view.

### How It Works

1. **Cell Pool**: DrawnUI maintains a pool of cell instances (typically 10-20 cells for a screen)
2. **Recycling**: When a cell scrolls out of view, it's returned to the pool
3. **Reuse**: When a new item needs to be displayed, a cell from the pool is reused
4. **Content Update**: The recycled cell's content is updated to display the new item

## Basic Setup

Enable recycling with the `RecyclingTemplate` property:

```xml
<draw:SkiaLayout
    Type="Column"
    ItemsSource="{Binding Items}"
    RecyclingTemplate="Enabled"
    MeasureItemsStrategy="MeasureFirst">
    
    <draw:SkiaLayout.ItemTemplate>
        <DataTemplate>
            <views:MyCell />
        </DataTemplate>
    </draw:SkiaLayout.ItemTemplate>
    
</draw:SkiaLayout>
```

## SkiaDynamicDrawnCell: The Recommended Approach

While you can use any `SkiaLayout` as a cell, `SkiaDynamicDrawnCell` is specifically designed for recycling scenarios and provides several advantages:

### Why Use SkiaDynamicDrawnCell?

- **Automatic Size Refresh**: Detects when cell content changes and refreshes auto-sized controls
- **Context Change Handling**: Provides clean override methods for content updates
- **Prevents Sizing Bugs**: Avoids issues where recycled cells keep old sizes with new content
- **Cleaner Code**: Eliminates manual height calculations and context management

### Basic Implementation

```csharp
public partial class MyCell : SkiaDynamicDrawnCell
{
    public MyCell()
    {
        InitializeComponent();
    }

    protected override void SetContent(object ctx)
    {
        base.SetContent(ctx);

        if (ctx is MyDataItem item)
        {
            // Update cell content based on the data item
            TitleLabel.Text = item.Title;
            DescriptionLabel.Text = item.Description;
            ItemImage.Source = item.ImageUrl;
        }
    }
}
```

## Measure Strategies for Different Scenarios

The `MeasureItemsStrategy` property controls how DrawnUI measures cell heights, which is crucial for performance:

### MeasureFirst (Default)
```xml
MeasureItemsStrategy="MeasureFirst"
```
- **Best for**: Lists with consistent or similar item heights
- **How it works**: Measures the first few items and uses that height for all items
- **Performance**: Fastest, but can cause layout issues with varying heights

### MeasureAll
```xml
MeasureItemsStrategy="MeasureAll"
```
- **Best for**: Small to medium lists where accuracy is important
- **How it works**: Measures every item before displaying
- **Performance**: Slower initial load, but accurate layout

### MeasureVisible (Experimental)
```xml
MeasureItemsStrategy="MeasureVisible"
```
- **Best for**: Large lists with uneven row heights
- **How it works**: Measures only visible items initially, then progressively measures off-screen items in background
- **Performance**: Instant scrolling even with thousands of items
- **Use case**: Perfect for news feeds, social media feeds, or any list with varying content sizes

## MeasureVisible: Deep Dive

The experimental `MeasureVisible` strategy is a game-changer for large lists with uneven heights:

### How MeasureVisible Works

1. **Initial Load**: Only measures items currently visible on screen
2. **Progressive Measurement**: Measures off-screen items in background during idle time
3. **Smart Estimation**: Uses measured items to estimate heights for unmeasured items
4. **Dynamic Updates**: Adjusts layout as more accurate measurements become available

### Benefits

- **Instant Scrolling**: No waiting for measurement of thousands of items
- **Memory Efficient**: Only keeps measurements for visible and nearby items
- **Adaptive**: Becomes more accurate as user scrolls and more items are measured
- **Background Processing**: Measurement happens during idle time without blocking UI

### When to Use MeasureVisible

✅ **Perfect for:**
- News feeds with mixed content (text, images, videos)
- Social media feeds
- Product catalogs with varying descriptions
- Any list with 100+ items of varying heights

❌ **Avoid for:**
- Lists with consistent item heights (use MeasureFirst instead)
- Small lists (< 50 items)
- Lists where exact positioning is critical from the start

## Template Reservation

Use `ReserveTemplates` to pre-allocate cell instances for smoother scrolling:

```xml
<draw:SkiaLayout
    Type="Column"
    ItemsSource="{Binding Items}"
    RecyclingTemplate="Enabled"
    ReserveTemplates="10"
    MeasureItemsStrategy="MeasureVisible">
```

- **ReserveTemplates="10"**: Pre-creates 10 cell instances
- **Smoother Scrolling**: Reduces cell creation during fast scrolling
- **Memory Trade-off**: Uses more memory but provides better performance

## Performance Best Practices

### 1. Use Appropriate Caching

Pick the cell root cache from the measuring strategy:

```xml
<!-- MeasureVisible: plain Image is enough — measurement runs in background,
     so the latency ImageDoubleBuffered hides is not on the hot path,
     and you save the second surface per cell -->
<draw:SkiaDynamicDrawnCell UseCache="Image">

<!-- MeasureFirst / MeasureAll, even-height rows with large cells -->
<draw:SkiaDynamicDrawnCell UseCache="GPU">

<!-- MeasureFirst / MeasureAll, other cases (varying heights etc.) -->
<draw:SkiaDynamicDrawnCell UseCache="ImageDoubleBuffered">

<!-- For complex layouts within cells -->
<draw:SkiaLayout UseCache="Image">

<!-- For text-only content -->
<draw:SkiaLabel UseCache="Operations">
```

### 2. Optimize Content Updates
```csharp
protected override void SetContent(object ctx)
{
    base.SetContent(ctx);

    if (ctx is MyDataItem item)
    {
        // Reset visibility states first
        HideAllContent();
        
        // Then configure based on content type
        ConfigureForContentType(item);
    }
}

private void HideAllContent()
{
    ImageContent.IsVisible = false;
    VideoContent.IsVisible = false;
    TextContent.IsVisible = false;
}
```

### 3. Handle Async Content
```csharp
protected override void SetContent(object ctx)
{
    base.SetContent(ctx);

    if (ctx is MyDataItem item)
    {
        // Set immediate content
        TitleLabel.Text = item.Title;
        
        // Handle async image loading
        if (!string.IsNullOrEmpty(item.ImageUrl))
        {
            ItemImage.Source = item.ImageUrl;
            ItemImage.IsVisible = true;
        }
        else
        {
            ItemImage.IsVisible = false;
        }
    }
}
```

## Windowed ItemsSource: Infinite Lists With Bounded Memory

With `MeasureVisible`, a list can present a virtually unlimited dataset while keeping only a window of items in memory. The app holds `[windowStart, windowStart + count)` of the full dataset and moves that window as the user scrolls:

- **Forward load** (`SkiaScroll.LoadMoreCommand`): append the next batch at the window end.
- **Backward load** (`SkiaScroll.LoadMoreTopCommand`): prepend the previous batch — the layout measures the block in background and keeps the viewport visually pinned while content appears above.
- **Memory cap**: before loading, trim the same amount from the opposite end of the window. The layout removes the block structure-preservingly — no reset, no remeasure, no visual jump.
- **Jump anywhere**: rebase the window to the target index and raise a Reset — the target renders at the top instantly, without loading anything between the old and new window.

The ItemsSource must raise a **single truthful collection event per batch**, carrying the items and the exact index: `ObservableRangeCollection<T>` from AppoMobi.Specials (10.0.2+) provides `AddRange`, `InsertRange(index, items)`, `RemoveRange(index, count)` and `ReplaceRangeReset`. Plain `ObservableCollection` per-item loops or range collections that degrade batches to Reset will force full rebuilds instead.

```csharp
// trim BEFORE loading, at the opposite end
void LoadMoreForward()
{
    int over = _items.Count + batchSize - MaxItemsInMemory;
    if (over > 0) { _items.RemoveRange(0, over); _windowStart += over; }
    _items.AddRange(BuildBatch(_windowStart + _items.Count, batchSize));
}

void LoadMoreBackward()
{
    int over = _items.Count + batchSize - MaxItemsInMemory;
    if (over > 0) _items.RemoveRange(_items.Count - over, over);
    _windowStart -= batchSize;
    _items.InsertRange(0, BuildBatch(_windowStart, batchSize));
}
```

Trim ordering matters: forward load trims the head, backward load trims the tail, and the trim happens before the load so it cannot race the prepend measurement. Keep `MaxItemsInMemory × rowHeight` well above `2 × LoadMoreOffset` so a trim never touches rows near the viewport. A full working sample: `LoadMoreRepro` (Blazor samples).

### ViewsAdapter.ApplyRemoveShift

When item indices shift because of a collection change, the recycled views currently on screen are keyed by their old indices. The adapter must be realigned **synchronously with the structure change, in the same frame** — otherwise visible cells resolve to wrong items for a frame, and their remeasure poisons cached heights. Two public `ViewsAdapter` methods do this:

- `ApplyInsertShift(source, startIndex, count)` — for insertions: rekeys in-use views at and after the insert point and swaps in a fresh data-contexts snapshot.
- `ApplyRemoveShift(source, startIndex, count)` — for removals: **releases** views bound to the removed range back to the pool, rekeys the views after it, and refreshes the snapshot. Plain index shifting is wrong for removals — views inside the removed range would land on surviving indices and show stale content.

The framework calls these internally for the windowed LoadMore paths; custom layout code mutating a recycled-cells structure must follow the same rule: adapter realignment and structure index shift happen together, on the render thread, never split across frames.

## Common Pitfalls and Solutions

### Problem: Old Content Showing
**Cause**: Not properly resetting cell state when recycling
**Solution**: Always reset all dynamic content in `SetContent()`

### Problem: Incorrect Heights
**Cause**: Using wrong measure strategy for your content type
**Solution**: Choose appropriate `MeasureItemsStrategy` based on your data

### Problem: Poor Scrolling Performance
**Cause**: Not using proper caching or too many complex operations in `SetContent()`
**Solution**: Use appropriate `UseCache` values and optimize content updates

## Advanced Example: Multi-Type Cell

```csharp
public partial class NewsCell : SkiaDynamicDrawnCell
{
    protected override void SetContent(object ctx)
    {
        base.SetContent(ctx);

        if (ctx is NewsItem news)
        {
            // Reset all content visibility
            HideAllContent();
            
            // Configure based on content type
            switch (news.Type)
            {
                case NewsType.Text:
                    ConfigureTextPost(news);
                    break;
                case NewsType.Image:
                    ConfigureImagePost(news);
                    break;
                case NewsType.Video:
                    ConfigureVideoPost(news);
                    break;
            }
        }
    }
    
    private void HideAllContent()
    {
        TextContent.IsVisible = false;
        ImageContent.IsVisible = false;
        VideoContent.IsVisible = false;
    }
    
    private void ConfigureTextPost(NewsItem news)
    {
        TextContent.Text = news.Content;
        TextContent.IsVisible = true;
    }
    
    // ... other configuration methods
}
```

## Conclusion

Recycled cells with `SkiaDynamicDrawnCell` and the experimental `MeasureVisible` strategy provide the foundation for building high-performance lists in DrawnUI. By understanding these concepts and applying the best practices outlined here, you can create smooth, efficient scrolling experiences even with large datasets and complex cell layouts.

For a complete working example, see the [News Feed Tutorial](../news-feed-tutorial.md) which demonstrates all these concepts in action.
