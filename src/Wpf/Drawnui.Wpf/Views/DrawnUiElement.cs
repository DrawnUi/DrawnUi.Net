using System.Windows;
using System.Windows.Input;
using AppoMobi.Gestures;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;
using Canvas = DrawnUi.Views.Canvas;
using WpfDrawingContext = System.Windows.Media.DrawingContext;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfSize = System.Windows.Size;
using WpfMouseButton = System.Windows.Input.MouseButton;
using WpfContextMenuEventArgs = System.Windows.Controls.ContextMenuEventArgs;

namespace DrawnUi.Wpf;

/// <summary>
/// Hosts a DrawnUI <see cref="Canvas"/> inside a WPF window. Put it anywhere a
/// <see cref="FrameworkElement"/> goes and set <see cref="Content"/> to the drawn root:
/// <code>
/// &lt;drawn:DrawnUiElement&gt;
///     &lt;draw:SkiaStack&gt; ... &lt;/draw:SkiaStack&gt;
/// &lt;/drawn:DrawnUiElement&gt;
/// </code>
/// <para>
/// <see cref="RenderingMode"/> picks the surface. <c>Default</c> paints into a
/// <see cref="WriteableBitmap"/>; <c>Accelerated</c> paints on the GPU through
/// <see cref="SkiaViewAccelerated"/>, which also gives GPU cache types a real GPU context. Both
/// composite with the rest of the WPF tree like any other visual.
/// </para>
/// </summary>
[System.Windows.Markup.ContentProperty(nameof(Content))]
public class DrawnUiElement : FrameworkElement, IDisposable
{
    private readonly WpfDrawable _drawable = new();
    private WriteableBitmap _bitmap;
    private SKSurface _surface;
    private int _pixelWidth;
    private int _pixelHeight;
    private bool _running;
    private bool _pointerDown;
    private SkiaViewAccelerated _gpuView;
    private Window _window;
    private WpfPoint _lastPointer;
    private bool _disposed;
    private static bool _superInitialized;

    /// <summary>The drawn canvas this element hosts. Configure it before or after adding content.</summary>
    public Canvas Canvas { get; }

    /// <summary>Root of the drawn tree.</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(object), typeof(DrawnUiElement),
        new FrameworkPropertyMetadata(null, OnContentChanged));

    /// <summary>Root of the drawn tree. Accepts a single control or a collection of controls.</summary>
    public object Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var element = (DrawnUiElement)d;
        element.Canvas.Content = e.NewValue;
        element.Canvas.BindingContext = element.DataContext;
    }

    /// <summary>Software or GPU rendering. Read when the element loads; later changes are ignored.</summary>
    public static readonly DependencyProperty RenderingModeProperty = DependencyProperty.Register(
        nameof(RenderingMode), typeof(RenderingModeType), typeof(DrawnUiElement),
        new FrameworkPropertyMetadata(RenderingModeType.Default));

    /// <summary>
    /// Software (<c>Default</c>) or GPU (<c>Accelerated</c>) rendering. Read when the element loads;
    /// later changes are ignored, because GPU cache surfaces already created on one context cannot
    /// be carried over to another surface type.
    /// </summary>
    public RenderingModeType RenderingMode
    {
        get => (RenderingModeType)GetValue(RenderingModeProperty);
        set => SetValue(RenderingModeProperty, value);
    }

    /// <summary>Creates the host and its canvas.</summary>
    public DrawnUiElement()
    {
        EnsureSuperInitialized();

        Canvas = new Canvas();

        // The canvas joins the WPF logical tree so DataContext flows into it the ordinary WPF way;
        // DrawnUI then propagates that context down its own tree (SkiaControl.ApplyBindingContext).
        AddLogicalChild(Canvas);

        Focusable = true;
        FocusVisualStyle = null;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += (_, e) => Canvas.BindingContext = e.NewValue;
    }

    private void EnsureSuperInitialized()
    {
        if (_superInitialized)
            return;

        _superInitialized = true;

        MainThread.Configure(
            action => Dispatcher.BeginInvoke(action),
            () => Dispatcher.CheckAccess());

        Super.Init();
    }

    private double DpiScale => VisualTreeHelper.GetDpi(this).DpiScaleX;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_running)
            return;

        _running = true;

        Super.Screen.Density = (float)DpiScale;

        // WPF has no "destroyed" event; the hosting window closing is the end of this canvas' life.
        // Unloaded is not: it also fires on tab switches and re-parenting, after which we come back.
        if (_window == null && Window.GetWindow(this) is { } window)
        {
            _window = window;
            _window.Closed += OnWindowClosed;
        }

        if (_gpuView == null)
        {
            // Before connecting: with a handler present, changing it recreates the canvas view.
            Canvas.RenderingMode = RenderingMode;

            if (Canvas.IsUsingHardwareAcceleration)
            {
                _gpuView = new SkiaViewAccelerated
                {
                    // Input is handled here, on the host, for both modes.
                    IsHitTestVisible = false,
                    OnDraw = DrawGpuFrame,
                };
                AddVisualChild(_gpuView);
                Canvas.ConnectDesktopDrawable(_gpuView);
                InvalidateMeasure();
                InvalidateVisual();
            }
            else
            {
                Canvas.ConnectDesktopDrawable(_drawable);
            }
        }

        Canvas.Update();
        CompositionTarget.Rendering += OnCompositionRendering;
    }

    private bool DrawGpuFrame(SKSurface surface, SKRect rect)
    {
        Canvas.WidthRequest = ActualWidth;
        Canvas.HeightRequest = ActualHeight;
        Canvas.RenderExternalSurface(surface, rect, _gpuView.FrameTime);
        return false;
    }

    /// <inheritdoc/>
    protected override int VisualChildrenCount => _gpuView != null ? 1 : 0;

    /// <inheritdoc/>
    protected override Visual GetVisualChild(int index) =>
        _gpuView != null && index == 0 ? _gpuView : throw new ArgumentOutOfRangeException(nameof(index));

    /// <inheritdoc/>
    protected override WpfSize MeasureOverride(WpfSize availableSize)
    {
        _gpuView?.Measure(availableSize);
        return base.MeasureOverride(availableSize);
    }

    /// <inheritdoc/>
    protected override WpfSize ArrangeOverride(WpfSize finalSize)
    {
        _gpuView?.Arrange(new WpfRect(finalSize));
        return finalSize;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_running)
            return;

        _running = false;
        CompositionTarget.Rendering -= OnCompositionRendering;
        ReleaseSurface();
    }

    private void OnWindowClosed(object sender, EventArgs e) => Dispose();

    /// <summary>
    /// Stops rendering and releases the drawn tree, its animators and every GPU object. Called
    /// automatically when the hosting window closes; call it yourself when discarding the element
    /// earlier than that.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _running = false;
        CompositionTarget.Rendering -= OnCompositionRendering;

        if (_window != null)
        {
            _window.Closed -= OnWindowClosed;
            _window = null;
        }

        Canvas.DisconnectedHandler();
        Canvas.Dispose();

        ReleaseSurface();
        _gpuView?.Dispose();
    }

    /// <inheritdoc/>
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        ReleaseSurface();
        Canvas.Update();
    }

    /// <inheritdoc/>
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Super.Screen.Density = (float)newDpi.DpiScaleX;
        ReleaseSurface();
        Canvas.Update();
    }

    private void ReleaseSurface()
    {
        _surface?.Dispose();
        _surface = null;
        _bitmap = null;
        _pixelWidth = 0;
        _pixelHeight = 0;
    }

    private bool EnsureSurface()
    {
        var scale = DpiScale;
        var width = (int)Math.Ceiling(ActualWidth * scale);
        var height = (int)Math.Ceiling(ActualHeight * scale);

        if (width <= 0 || height <= 0)
            return false;

        if (_surface != null && width == _pixelWidth && height == _pixelHeight)
            return true;

        _surface?.Dispose();

        _pixelWidth = width;
        _pixelHeight = height;
        _bitmap = new WriteableBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32, null);

        // Pbgra32 is premultiplied BGRA, which is exactly Bgra8888/Premul on the Skia side, so the
        // surface can be created straight over the bitmap's back buffer with no intermediate copy.
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _surface = SKSurface.Create(info, _bitmap.BackBuffer, _bitmap.BackBufferStride);

        _drawable.Surface = _surface;
        _drawable.CanvasSize = new SKSize(width, height);

        InvalidateVisual();
        return _surface != null;
    }

    /// <summary>
    /// WPF's composition tick is the pacer for both modes, but a frame is only produced when the
    /// canvas asked for one (<c>Update()</c> → <c>IsDirty</c>) — the same gate the Net and OpenTK
    /// heads use. An idle tree costs nothing per tick.
    /// </summary>
    private void OnCompositionRendering(object sender, EventArgs e)
    {
        if (!_running || !Canvas.CheckCanDraw() || !Canvas.CanDraw)
            return;

        if (_gpuView != null)
        {
            _gpuView.Update();
            return;
        }

        if (!EnsureSurface())
            return;

        var frameTime = Super.GetCurrentTimeNanos();
        _drawable.SignalFrame(frameTime);

        // Points, not pixels: DrawnView derives RenderingScale from PhisicalWidth / WidthRequest,
        // so the DPI scale falls out of the difference between these two on its own.
        Canvas.WidthRequest = ActualWidth;
        Canvas.HeightRequest = ActualHeight;

        _bitmap.Lock();
        try
        {
            Canvas.RenderExternalSurface(_surface, new SKRect(0, 0, _pixelWidth, _pixelHeight), frameTime);
            _surface.Canvas.Flush();
            _bitmap.AddDirtyRect(new Int32Rect(0, 0, _pixelWidth, _pixelHeight));
        }
        finally
        {
            _bitmap.Unlock();
        }
    }

    /// <inheritdoc/>
    protected override void OnRender(WpfDrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (_gpuView != null)
            // The GL child is not hit-testable, so the host paints an invisible hit area of its own.
            drawingContext.DrawRectangle(Brushes.Transparent, null, new WpfRect(0, 0, ActualWidth, ActualHeight));
        else if (_bitmap != null)
            drawingContext.DrawImage(_bitmap, new WpfRect(0, 0, ActualWidth, ActualHeight));
    }

    #region Input

    private float ClientPixelWidth => (float)Math.Ceiling(ActualWidth * DpiScale);

    private float ClientPixelHeight => (float)Math.Ceiling(ActualHeight * DpiScale);

    private SKPoint ToCanvasPixels(WpfPoint position)
    {
        var scale = DpiScale;
        return new SKPoint((float)(position.X * scale), (float)(position.Y * scale));
    }

    /// <inheritdoc/>
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.ChangedButton != WpfMouseButton.Left)
            return;

        Focus();
        CaptureMouse();
        _pointerDown = true;

        var point = ToCanvasPixels(e.GetPosition(this));
        Canvas.HandleDesktopPointerDown(point.X, point.Y, ClientPixelWidth, ClientPixelHeight);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        _lastPointer = e.GetPosition(this);
        var point = ToCanvasPixels(_lastPointer);
        Canvas.HandleDesktopPointerMove(point.X, point.Y, _pointerDown, ClientPixelWidth, ClientPixelHeight);
    }

    /// <inheritdoc/>
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.ChangedButton != WpfMouseButton.Left || !_pointerDown)
            return;

        _pointerDown = false;
        ReleaseMouseCapture();

        var point = ToCanvasPixels(e.GetPosition(this));
        Canvas.HandleDesktopPointerUp(point.X, point.Y, ClientPixelWidth, ClientPixelHeight);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        var point = ToCanvasPixels(e.GetPosition(this));
        Canvas.HandleDesktopWheel(point.X, point.Y, e.Delta, ClientPixelWidth, ClientPixelHeight);
        e.Handled = true;
    }

    /// <summary>
    /// Right click, Shift+F10 and the Menu key all arrive here. The request is routed to
    /// <c>SkiaControl.ContextMenu</c> like a tap; when a control takes it the event is marked handled
    /// so no WPF ContextMenu set on this element (or an ancestor) opens on top of it.
    /// </summary>
    protected override void OnContextMenuOpening(WpfContextMenuEventArgs e)
    {
        base.OnContextMenuOpening(e);

        if (e.Handled)
            return;

        // Keyboard-triggered requests carry no position (-1): use where the pointer last was.
        var fromKeyboard = e.CursorLeft < 0 || e.CursorTop < 0;
        var position = fromKeyboard ? _lastPointer : Mouse.GetPosition(this);
        var point = ToCanvasPixels(position);
        PointerDeviceType? device = fromKeyboard
            ? null
            : Stylus.CurrentStylusDevice?.TabletDevice?.Type == TabletDeviceType.Touch ? PointerDeviceType.Touch
            : Stylus.CurrentStylusDevice != null ? PointerDeviceType.Pen
            : PointerDeviceType.Mouse;

        if (Canvas.HandleDesktopContextMenu(point.X, point.Y, ClientPixelWidth, ClientPixelHeight, device))
            e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnTextInput(TextCompositionEventArgs e)
    {
        base.OnTextInput(e);

        var text = e.Text;
        if (string.IsNullOrEmpty(text) || text == "\r" || text == "\n" || text == "\t")
            return;

        Canvas.HandleDesktopTextInput(text);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        // Editing keys belong to a focused drawn editor only. Without one they stay with WPF, so
        // Tab keeps cycling focus through the window and arrows keep scrolling a host ScrollViewer.
        if (Canvas.FocusedChild is not SkiaEditor)
            return;

        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);

        switch (e.Key)
        {
            case Key.Back: Canvas.DesktopEditorBackspace(); break;
            case Key.Delete: Canvas.DesktopEditorDelete(); break;
            case Key.Enter: Canvas.DesktopEditorEnter(alt, shift); break;
            case Key.Left: Canvas.DesktopEditorMoveCursor(-1, shift); break;
            case Key.Right: Canvas.DesktopEditorMoveCursor(1, shift); break;
            case Key.Home: Canvas.DesktopEditorMoveToStart(shift); break;
            case Key.End: Canvas.DesktopEditorMoveToEnd(shift); break;
            case Key.A when ctrl: Canvas.DesktopEditorSelectAll(); break;
            case Key.Tab: Canvas.HandleDesktopTextInput("    "); break;
            default: return;
        }

        e.Handled = true;
    }

    #endregion
}
