# Advanced Scrolling with SkiaScroll in DrawnUi.Maui

DrawnUi.Maui’s SkiaScroll control provides high-performance, flexible scrolling for custom UIs, games, dashboards, and data-heavy apps. This article covers advanced usage, virtualization, customization, and best practices for SkiaScroll and related controls.

## Why SkiaScroll?
- **Smooth, pixel-perfect scrolling** on all platforms
- **Supports both vertical, horizontal, and bidirectional scrolling**
- **Virtualization** for large data sets
- **Customizable headers, footers, and overlays**
- **Pinch-to-zoom and gesture support**
- **Works with any DrawnUi content: layouts, images, shapes, etc.**

## Basic Usage

```xml
<draw:SkiaScroll Orientation="Vertical" WidthRequest="400" HeightRequest="600">
    <draw:SkiaLayout Type="Column" Spacing="10">
        <draw:SkiaLabel Text="Item 1" />
        <draw:SkiaLabel Text="Item 2" />
        <!-- More items -->
    </draw:SkiaLayout>
</draw:SkiaScroll>
```

## Multi-Directional and Zoomable Scrolling

```xml
<draw:SkiaScroll Orientation="Both" ZoomLocked="False" ZoomMin="1" ZoomMax="3">
    <draw:SkiaLayout>
        <draw:SkiaImage Source="large_map.jpg" />
    </draw:SkiaLayout>
</draw:SkiaScroll>
```

## Virtualization for Large Data Sets

Enable virtualization for smooth performance with thousands of items:

```xml
<draw:SkiaScroll Virtualisation="Enabled" Orientation="Vertical">
    <draw:SkiaLayout
        Type="Column"
        ItemsSource="{Binding LargeItemCollection}"
        Virtualisation="Enabled">
        <draw:SkiaLayout.ItemTemplate>
            <DataTemplate>
                <draw:SkiaLabel Text="{Binding Title}" />
            </DataTemplate>
        </draw:SkiaLayout.ItemTemplate>
    </draw:SkiaLayout>
</draw:SkiaScroll>
```

- `Virtualisation` on SkiaScroll controls viewport-based rendering.
- `Virtualisation` on SkiaLayout controls the strategy (Enabled, Disabled).
- Combine with `RecyclingTemplate` for template reuse.
- Use `VirtualisationInflated` to control how much content outside the viewport is still rendered.

## Custom Headers, Footers, and Overlays

```xml
<draw:SkiaScroll HeaderSticky="True" HeaderBehind="False">
    <draw:SkiaScroll.Header>
        <draw:SkiaShape Type="Rectangle" BackgroundColor="#3498DB" HeightRequest="80">
            <draw:SkiaLabel Text="Sticky Header" TextColor="White" FontSize="18" />
        </draw:SkiaShape>
    </draw:SkiaScroll.Header>
    <draw:SkiaLayout Type="Column">
        <!-- Content -->
    </draw:SkiaLayout>
    <draw:SkiaScroll.Footer>
        <draw:SkiaShape Type="Rectangle" BackgroundColor="#2C3E50" HeightRequest="60">
            <draw:SkiaLabel Text="Footer" TextColor="White" />
        </draw:SkiaShape>
    </draw:SkiaScroll.Footer>
</draw:SkiaScroll>
```

## Pull-to-Refresh

```xml
<draw:SkiaScroll x:Name="MyScrollView" Refreshing="OnRefreshing">
    <draw:SkiaScroll.RefreshIndicator>
        <draw:RefreshIndicator />
    </draw:SkiaScroll.RefreshIndicator>
    <draw:SkiaLayout Type="Column">
        <!-- Content items -->
    </draw:SkiaLayout>
</draw:SkiaScroll>
```

In code-behind:
```csharp
private async void OnRefreshing(object sender, EventArgs e)
{
    // Perform refresh operation
    await LoadDataAsync();
    ((SkiaScroll)sender).EndRefresh();
}
```

## Scroll Bar Indicator

```xml
<draw:SkiaScroll ScrollBarsVisibility="Vertical" Orientation="Vertical">
    <!-- Content -->
</draw:SkiaScroll>
```

`ScrollBarsVisibility` (`[Flags]`: `None`, `Vertical`, `Horizontal`) auto-creates a default `SkiaScrollBar` per axis flagged — one bar slot per axis (`ScrollBar` for vertical, `ScrollBarHorizontal` for horizontal), so `Orientation="Both"` can show both at once with `ScrollBarsVisibility="Vertical,Horizontal"`. Default `None`, zero cost when unused.

`SkiaScroll.ScrollBarThumbColor` / `ScrollBarTrackColor` tint auto-created (or assigned) `SkiaScrollBar` instances on both axes without writing code; defaults match `SkiaScrollBar`'s own.

For a custom bar, assign `ScrollBar` / `ScrollBarHorizontal` directly (shows regardless of the flag) — set `SkiaScrollBar.Dock="Start"` for RTL or a 180-rotated (inverted chat) scroll — or implement `IScrollBar` on any `SkiaControl` for a fully custom indicator. See [Scroll Controls](../controls/scroll.md#scroll-bar-indicator) for the full property table and `IScrollBar` example.

## Infinite and Looped Scrolling

Use SkiaScrollLooped for banners, carousels, or infinite galleries:

```xml
<draw:SkiaScrollLooped Orientation="Horizontal" IsBanner="True" CycleSpace="100">
    <draw:SkiaLayout Type="Row">
        <draw:SkiaImage Source="image1.jpg" />
        <draw:SkiaImage Source="image2.jpg" />
        <!-- More images -->
    </draw:SkiaLayout>
</draw:SkiaScrollLooped>
```

## Programmatic Scrolling and Position Tracking

```csharp
// Scroll to a specific position
myScroll.ScrollToPosition(0, 500, true); // Animated scroll to Y=500

// Scroll to a child element
myScroll.ScrollToView(targetElement, true);

// Track scroll position
float y = myScroll.ViewportOffsetY;
```

## Performance Tips
- Enable virtualization for large lists
- Use `Cache="Operations"` for static or rarely-changing content
- Avoid nesting too many scrolls; prefer flat layouts
- Use SkiaLabelFps to monitor performance
- For custom drawing, override OnDraw in your content controls

## Advanced: Custom Scroll Effects and Gestures
- Implement parallax, sticky headers, or custom scroll physics by extending SkiaScroll
- Use gesture listeners for advanced input (drag, swipe, pinch)
- Combine with SkiaDrawer for overlay panels

## Summary
SkiaScroll and related controls provide a robust, high-performance foundation for any scrolling UI in DrawnUi.Maui. With support for virtualization, zoom, custom overlays, and advanced gestures, you can build everything from chat apps to dashboards and games with smooth, responsive scrolling.
