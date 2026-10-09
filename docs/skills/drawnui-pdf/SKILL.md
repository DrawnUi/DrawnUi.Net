---
name: drawnui-pdf
description: Render a DrawnUI layout (XAML or C#) into a PDF, a JPEG/PNG or any Skia canvas without a screen — a report template bound to a view model, measured and drawn by hand, split into real paper pages between table rows. Covers the Pdf helper API, points vs pixels, waiting for async images, the warm-up passes, multi-page export, image export, a desktop preview window with hot reload, and headless server-side rendering with DrawnUi.Net. Trigger on "pdf", "report", "print", "SKDocument", "export to image", "render offscreen", "Pdf.SplitStackToPages".
---

# DrawnUI to PDF

A DrawnUI control does not know where it is drawn. On screen the `Canvas` measures it, arranges it and hands it a Skia canvas. Hand it a PDF page canvas instead and the same XAML prints. Text, shapes and custom `Paint` code stay vectors in the PDF, images stay images.

Load `drawnui` with this skill (and `drawnui-fluent` when the template is C#). Reference implementation: the RepoReport sample linked from https://taublast.github.io/posts/DrawnPdf/ (`Export/ReportExporter.cs`, `Reports/ReportView.xaml`), plus the Sandbox pages `src/Maui/Samples/Sandbox/Views/MainPageXaml2Pdf*.xaml.cs` in the DrawnUI repo (older, single page and fixed slices). Needs `DrawnUi.Maui` 1.10.7.1 or newer for correct page splitting (see Pitfalls).

## The template

A normal DrawnUI layout in a XAML file with code-behind (or a C# class), like a `ContentView`:

```xml
<draw:SkiaLayout
    x:Class="MyApp.Reports.ReportView"
    x:DataType="vm:ReportViewModel"
    Type="Column"
    Padding="48"
    Spacing="28"
    BackgroundColor="White"
    HorizontalOptions="Fill">
```

Rules that differ from a screen:

- **No `UseCache` anywhere inside the template.** A cached subtree becomes a bitmap in the PDF: text stops being selectable, the file grows. The on-screen instance of the same template may be cached from code (`UseCache = SkiaCacheType.Image` on that instance only).
- **Root is a Column** when the report must break into pages; the table is a templated `SkiaLayout Type="Column"` with `ItemsSource`, `RecyclingTemplate="Disabled"` and `MeasureItemsStrategy="MeasureAll"`: there is no viewport, every row must exist and be measured.
- **Units are pixels of the chosen dpi**, `scale = 1`. At 150 dpi a portrait A4 is 1240 x 1753 units and `FontSize="19"` prints at about 9 pt. Design margins and font sizes for that.
- **Bindings work as usual**, set `BindingContext` at export time. Fonts are the ones registered at startup, the Skia PDF backend embeds them.
- **Images load async** (urls, files, map tiles). The template implements a small `IContentReadyAware { bool ContentIsLoaded { get; } }`. Decide readiness from STATE, not from counted events: keep the `SkiaImage` instances in a list and report ready when `list.Count >= expected && list.All(i => i.LoadedSource != null || i.HasError || !i.IsLoading)`. Counting `Success`/`Error` events misses two cases and then the export sits on its timeout: a cached source raises `Success` synchronously inside the `Source` setter (before a handler attached after it in an object initializer), and a load cancelled mid-flight (`ReloadSource`, a second `Source` set) returns without any event. If you do subscribe to events, subscribe before setting `Source`. For map tiles use the control's own signal (`SkiaMapsUi.LoadingChanged`).
- Custom charts and bars as small `SkiaControl` subclasses overriding `Paint(DrawingContext ctx)` draw with raw Skia and land in the PDF as paths.

## Export, step by step

```csharp
const int Dpi = 150;
const float MarginInches = 0.3f;
static readonly TimeSpan ContentTimeout = TimeSpan.FromSeconds(15);

var scale = 1f;
var inches = Pdf.GetPaperSizeInInches(PaperFormat.A4);      // swap W/H for landscape
var paper = Pdf.GetPaperSizePixels(inches, Dpi);
var margins = MarginInches * Dpi;
var pageInner = new SKSize(paper.Width - margins * 2, paper.Height - margins * 2);

// 1. create, never attach to a Canvas
var content = new ReportView { BindingContext = data, HorizontalOptions = LayoutOptions.Fill };

// 2. measure at page width with unlimited height, warm up off the PDF
await WarmUpAsync(content, pageInner.Width, float.PositiveInfinity, scale);

// 3. page offsets that break between rows (needs the render tree from the warm-up)
var pages = Pdf.SplitStackToPages(content, isTemplated: true, pageInner, scale);

// 4. a window over the content: padding = margins, scroll = viewport
SkiaScroll viewport = null;
var wrapper = new SkiaLayout
{
    BindingContext = data,
    Padding = new Thickness(margins),
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    Children = new List<SkiaControl>
    {
        new SkiaScroll
        {
            BackgroundColor = Colors.White,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = content,
        }.Assign(out viewport)
    }
};
wrapper.Measure(paper.Width, paper.Height, scale);
wrapper.Arrange(new SKRect(0, 0, paper.Width, paper.Height),
    wrapper.MeasuredSize.Pixels.Width, wrapper.MeasuredSize.Pixels.Height, scale);

// 5. the document: pages in POINTS, canvas scaled from our dpi
var pagePoints = new SKSize(inches.Width * 72, inches.Height * 72);
using var ms = new MemoryStream();
using (var stream = new SKManagedWStream(ms))
using (var document = SKDocument.CreatePdf(stream, new SKDocumentPdfMetadata
{
    RasterDpi = Dpi, EncodingQuality = 85, Author = "...", Producer = "DrawnUI", Title = "...",
}))
{
    foreach (var page in pages)
    {
        viewport.ViewportOffsetY = -page.Position.Y;
        RenderOnce(wrapper, paper, scale);                 // settle the scroll off the PDF
        using (var canvas = document.BeginPage(pagePoints.Width, pagePoints.Height))
        {
            canvas.Scale(72f / Dpi);
            canvas.Clear(SKColors.White);
            var ctx = new SkiaDrawingContext { Canvas = canvas, Width = paper.Width, Height = paper.Height };
            wrapper.Render(new DrawingContext(ctx, new SKRect(0, 0, paper.Width, paper.Height), scale));
        }
        document.EndPage();
    }
    document.Close();
}
await File.WriteAllBytesAsync(fullFilename, ms.ToArray());
```

Warm-up: the first `Render` starts the async loads, then wait, render again, and keep rendering while something still invalidates. All of it into a throwaway recording, because a PDF canvas keeps every drawing command and `Clear` only paints over them.

```csharp
static async Task WarmUpAsync(SkiaControl layout, float width, float height, float scale)
{
    layout.Measure(width, height, scale);
    layout.Arrange(new SKRect(0, 0, layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height),
        layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height, scale);
    var size = new SKSize(layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height);

    RenderOnce(layout, size, scale);                        // starts image loads
    var started = DateTime.UtcNow;
    if (layout is IContentReadyAware aware)
        while (!aware.ContentIsLoaded && DateTime.UtcNow - started < ContentTimeout)
            await Task.Delay(50);

    RenderOnce(layout, size, scale);                        // reflects loaded images
    var frames = 0;
    while (layout.NeedUpdate && frames++ < 60)             // settle, capped
    {
        await Task.Delay(16);
        RenderOnce(layout, size, scale);
    }
    layout.Arrange(new SKRect(0, 0, layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height),
        layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height, scale);
}

static void RenderOnce(SkiaControl layout, SKSize size, float scale)
{
    using var recorder = new SKPictureRecorder();
    var canvas = recorder.BeginRecording(new SKRect(0, 0, size.Width, size.Height));
    var ctx = new SkiaDrawingContext { Canvas = canvas, Width = size.Width, Height = size.Height };
    layout.Render(new DrawingContext(ctx, new SKRect(0, 0, size.Width, size.Height), scale));
    using var picture = recorder.EndRecording();
}
```

Save to the app cache folder, then `Launcher.Default.OpenAsync(new OpenFileRequest(...))` on Windows/Mac, `Share.Default.RequestAsync(new ShareFileRequest(...))` on mobile. No storage permission needed for the cache folder.

## Pdf helper API (`DrawnUi.Infrastructure`, every head)

| Member | Meaning |
|---|---|
| `PaperFormat` | `A4`, `A5`, `A6`, `Letter`, `Legal`; `Custom` throws, pass an `SKSize` instead |
| `Pdf.GetPaperSizeInInches(format)` | A4 = 8.27 x 11.69 in |
| `Pdf.GetPaperSizePixels(format or SKSize inches, dpi)` / `GetPaperSizePixelsFromMillimeters` | paper in pixels at dpi |
| `Pdf.SplitStackToPages(control, isTemplated, paper, scale = 1)` | page offsets breaking between the children of the first (templated) Column found; a child taller than a page is sliced; needs a prior render at unlimited height; offsets relative to `control` |
| `Pdf.SplitToPages(contentSize, paper)` | fixed slices of the paper height, can cut through a row; fallback when there is no stack |
| `PdfPagePosition` | `Index`, `Position` (content offset, apply as `ViewportOffsetY = -Position.Y`), `Height` (printable height of that page) |

## Image instead of PDF

Same warm-up at the page width, then one render into a surface the size of the content:

```csharp
var w = (int)Math.Ceiling(content.MeasuredSize.Pixels.Width);
var h = (int)Math.Ceiling(content.MeasuredSize.Pixels.Height);
using var surface = SKSurface.Create(new SKImageInfo(w, h));
surface.Canvas.Clear(SKColors.White);
var ctx = new SkiaDrawingContext { Canvas = surface.Canvas, Surface = surface, Width = w, Height = h };
content.Render(new DrawingContext(ctx, new SKRect(0, 0, w, h), scale));
surface.Flush();
using var image = surface.Snapshot();
using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
```

## Preview window with hot reload (Windows / Mac Catalyst)

Open a second `Window` with a `BasePageReloadable` page holding `Canvas > SkiaScroll > factory()`. The factory builds a fresh template on every hot reload, the window is sized to the paper proportions (`inches * Dpi * 0.75` fits most screens). Position it next to the main window with `Application.Current.Windows[0].X + Width + 20`, open with `Application.Current.OpenWindow(window)`.

## Pitfalls

- `SKDocument.BeginPage` takes POINTS (72 per inch). Passing the dpi pixel size makes an "A4" 2.08x too large; viewers fit it to the window so it looks right until printed. Open the page in points and `canvas.Scale(72f / dpi)`.
- A PDF canvas records every pass. Warm up into `SKPictureRecorder`, render once into the page.
- `SplitStackToPages` reads `RenderTree`, which holds only the rows that were drawn: render at unlimited height first, not through a page-sized scroll.
- Before DrawnUi.Maui 1.10.7.1 `SplitStackToPages` added the ancestors' `Margin.Top + Padding.Top` to already absolute row rects: any padding above the table shifted every page. On older packages keep `Padding.Top = 0` and no margins on every ancestor between the paginated control and the table, or update.
- Image wait without a timeout hangs the export on a dead url; count `Error` as done and cap the wait.
- `RasterDpi` in the metadata only affects what Skia must rasterize (image filters, shaders), `EncodingQuality` is the JPEG quality of embedded bitmaps; 85 is a good size/quality ratio.
- Export runs on the main thread in the samples (bindings), the awaits keep the UI alive; a very large report blocks per pass, keep passes few.

## Headless, server side

The `DrawnUi.Net` package (also inside `DrawnUi.Blazor.Server`) runs the same engine without MAUI: the exact export code above with a C# template (XAML needs MAUI). Startup, once:

```csharp
new DrawnUiBuilder()
    .ConfigureFonts(fonts =>
    {
        fonts.AddFont("OpenSans-Regular.ttf", "FontText");   // file next to the app (AppContext.BaseDirectory)
        fonts.AddFont("OpenSans-Semibold.ttf", "FontTextTitle");
    })
    .Build();
```

No `Super.Init()` and no screen density are needed: the layouts you measure by hand take the scale you pass to `Measure(w, h, scale)`. (Grid did not before the fix after 1.10.7.4: it measured its cells with each child's own `RenderingScale`, which falls back to the global density, 0 in a bare process, so a detached grid came out 0x0 with NaN cells while rows and columns worked. On 1.10.7.4 or older call `Super.Init()` once at startup as the workaround.)

- Fonts: copy the `.ttf` files next to the app (`<Content Include="Resources\Fonts\**" Link="%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />`) and register them by file name; same for `SkiaSvg` sources.
- The Net head has its own `Color` (`global using Color = DrawnUi.Color;`): `Parse`, `FromRgba`, `FromHsla(float...)`, `ToSKColor()`, no `AddLuminosity`. `BindableProperty`, `DataTemplate`, `Command`, `Thickness`, `LayoutOptions` exist as shims.
- `ItemTemplate` before `ItemsSource` in a C# object initializer, or the templated layout creates no cells.
- Blazor Server: `builder.Services.AddDrawnUiBlazorServer()`, a `<Canvas Content="@report" Width="1100" HeightRequest="@measuredHeight" JpegQuality="95" />` shows the same template as server-rendered image frames (its auto height stops around 1200 px, measure the report yourself for `HeightRequest`), and a minimal API endpoint returns `Results.File(bytes, "application/pdf", name)`. Reference: the RepoReportBlazor sample linked from the article.

## References

- Article: https://taublast.github.io/posts/DrawnPdf/ (the full walkthrough, with the RepoReport sample)
- Lib: `src/Shared/DrawnUi/Features/Pdf/Pdf.cs`, tests `src/Net/Tests/DrawnUi.Net.Tests/PdfSplitToPagesTests.cs`
- Discussion with the original snippet: https://github.com/DrawnUi/DrawnUi.Net/discussions/257
- Multi-page thread: https://github.com/DrawnUi/DrawnUi.Net/issues/127
