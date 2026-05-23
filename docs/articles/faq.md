# Frequently Asked Questions

If you are using DrawnUI in the browser or inside a Blazor app, also see the dedicated [Blazor FAQ](blazor/faq.md).

## 🤔 Onboarding

**Q: What is the difference between DrawnUi and other drawn frameworks?**  
A: DrawnUI is an umbrella for multiple .NET targets. It doesn't tend to replace those, but to marry them. It gives you the same drawn layout and controls model across:

- `DrawnUi.Maui` for .NET MAUI
- `DrawnUi.Blazor.Wasm` for browser-side Blazor rendering
- `DrawnUi.Blazor.Server` for server-backed Blazor rendering
- `DrawnUi.Net` for console/server and headless .NET scenarios

If you are choosing a host first, start with [Platforms and Packages](platforms.md).

**Q: Which package do I install?**  
A: Choose by host:

- install `DrawnUi.Maui` for native MAUI apps
- install `DrawnUi.Blazor.Wasm` when DrawnUI should render locally in the browser
- install `DrawnUi.Blazor.Server` for Blazor Server and `InteractiveServer`
- install `DrawnUi.Net` for platform-agnostic, headless, or harness-style .NET usage

See also [Platforms and Packages](platforms.md), [Blazor FAQ](blazor/faq.md), and [DrawnUi.Net](net/index.md).

**Q: Why choose drawn over native UI?**  
A: Rather a freedom choice to draw what you want and how you see it.  
It also can bemore performant to draw a complex UI on just one canvas instead of composing it with many native views.

**Q: Do I need to know how to draw on a canvas??**  
A: No, you can start by using prebuilt drawn controls and customize them. All controls are initially designed to be subclassed, customized, and almost every method is virtual. 

**Q: Can I still use XAML?**  
A: Yes, in the **MAUI** host. DrawnUI on MAUI supports both XAML and code-behind. In **Blazor**, the host surface is Razor with a `Canvas` component instead of XAML. In `DrawnUi.Net`, there is no XAML host at all.

**Q: Can I avoid using XAML at all costs?**  
A: Yes. You can build DrawnUI entirely in C#.

- on MAUI, use code-behind instead of XAML
- on Blazor, build your drawn tree in C# and host it inside Razor `Canvas`
- on `DrawnUi.Net`, use the same drawn control tree without a UI framework host

**Q: How do I create custom controls with DrawnUI?**  
A: Inherit from `SkiaControl` for basic controls or `SkiaLayout` for containers etc. Override the `Paint` method to draw with SkiaSharp.

**Q: Can I embed native MAUI controls inside DrawnUI?**  
A: Yes, on the **MAUI** host. Use `SkiaMauiElement` to embed native MAUI controls like WebView inside your DrawnUI canvas. That answer is MAUI-specific and does not apply to Blazor or `DrawnUi.Net`.

**Q: Can I use DrawnUI in Blazor?**  
A: Yes. Use `DrawnUi.Blazor.Wasm` when the DrawnUI surface should stay local in the browser, and `DrawnUi.Blazor.Server` when the surface should be server-owned. See [Blazor FAQ](blazor/faq.md).

**Q: Can I use DrawnUI without MAUI or Blazor?**  
A: Yes. Use `DrawnUi.Net` for platform-agnostic .NET scenarios such as headless rendering, image/PDF generation, control harnesses, and shared-layout debugging. See [DrawnUi.Net](net/index.md).

**Q: Possible to create a game with DrawnUI?**  
A: Well, since you draw, why not just draw a game instead of a business app. DrawnUI comes with gaming helpers and custom accelerated platform views to assure a smooth display-synched rendering.

**Q: Why is SkiaWhatever control is missing from DrawnUI?**  
A: Initially this library was created to allow one to create custom drawn controls with ease, and it is a toolbox for crafters. Please consider making a  PR with your drawn control or open a discussion about a drawn control to be included along with DrawnUI.


## Thechnical Questions

**Q: How do I create custom controls with DrawnUI?**  
A: Subclass `SkiaControl` for custom, `SkiaLayout` for container etc, . Override the `Paint` method to draw with SkiaSharp on the canvas provided inside drawing context.

**Q: Can I embed native MAUI controls inside DrawnUI?**  
A: Yes, on the **MAUI** host. Use `SkiaMauiElement` to embed native MAUI controls like WebView inside your DrawnUI canvas.

**Q: Can I use MAUI's default `Resources/Images` folder?**  
A: Sorry, no, drawn resources lives inside `Resources/Raw` and subfolders. Note that MAUI supports only lowercase filenames of resources and while uppercase might works for you on some plaforms they will not be read on iOS.

**Q: How do I change SkiaSvg source not from file/url?**  
A: set `SvgString` property to svg text string.

**Q: Why my scroll is resetting at all time while Iproperly use ObservableCollection for LoadMore?**  
A: Check that your custom ObservableRangeCollection is sending Reset event when adding range of items.

**Q: How do I change SkiaImage source not from file/url?**  
A: set directly: `mySkiaImage.SetImageInternal(skiaImage)`.

**Q: How do I prevent touch events from passing through overlapping controls?**  
A: Use the `BlockGesturesBelow="True"` property on the top control. Note that `InputTransparent` makes the control itself avoid gestures, not controls below.

**Q: How do I internally rebuild the ItemsSource?**  
A: Directly call `layout.ApplyItemsSource()`.

**Q: How to make images to Fade-In when loaded?**
A: Subclass `SkiaImage` to define your animation:

```csharp
public class BannerImage : SkiaImage
{
    public override void OnSuccess(string source)
    {
        base.OnSuccess(source);

        this.Opacity = 0.01;
        _ = this.FadeToAsync(1, 300, Easing.SinIn);
    }
}
```

**Q: How to expand button hitbox?**

A: Every drawn control can do that:

```csharp
public override SKRect CreateHitRect()
{
    var ret = base.CreateHitRect();
    ret.Inflate(10*RenderingScale, 10*RenderingScale);
    return ret;
}
```

**Q: How to reduce battery drain/heat on iPhone in constant sceen updates scenarios?**
A: Might be Apple Metal specifics, cap FPS:

```csharp
#if IOS //spare battery because apple metal is draining much. android is not affected at the same level.
            Super.MaxFps = 30;
#endif
```

## Troubleshooting
 
**Q: Why is scrolling/rendering slow?**  

**Solutions:**
1. Always use cache for layers of controls:
   * Do NOT cache scrolls/heavily animated controls and above
   * `UseCache = SkiaCacheType.Operations` for labels and svg
   * `UseCache = SkiaCacheType.Image` for complex layouts, buttons etc
   * `UseCache = SkiaCacheType.ImageComposite` for complex layouts where a region changes while others remain static, like a stack with different user-handled controls.
   * `UseCache = SkiaCacheType.ImageDoubleBuffered` for equally sized recycled cells. Will show old cache while preparing new one in background.
   * `UseCache = SkiaCacheType.GPU` for small static overlays like headers, navbars.
   * **PROHIBITED:** Never use `Operations` or `GPU` cache for controls with GPU-surface shaders — use `Image`, `ImageDoubleBuffered`, or `ImageComposite` instead.
   * **PROHIBITED:** Never nest children that use GPU-backed cache types (`GPU`, `ImageCompositeGPU`) inside a parent cached with `Operations`.
2. Check that you do not have some logging running for every rendering frame.

**Q: Why isn't my UI updating when ViewModel properties change:**  

**Solutions:**
1. Ensure ViewModel implements `INotifyPropertyChanged`
2. Check that property either has a static bindable property or calls `OnPropertyChanged()` in the setter.
3. Check that property names match exactly; use `nameof()`.
4. Ensure all your overrides, if any, of `void OnPropertyChanged([CallerMemberName] string propertyName = null)` have a `[CallerMemberName]` attribute.

---


**Can't find the answer to your question?** → 
* Please start with [Platforms and Packages](platforms.md) to pick the correct host and sample lane.
* For MAUI-oriented walkthroughs, check out [MAUI Tutorials and Host Notes](maui/tutorials.md).
* For browser-hosted questions, check out [Blazor FAQ](blazor/faq.md).
* [Ask in GitHub Discussions](https://github.com/taublast/DrawnUi/discussions)** - The community is here to help!


