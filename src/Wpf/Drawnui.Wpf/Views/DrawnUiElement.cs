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
    private WpfMouseButton _pressedButton;
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

    /// <summary>How the canvas shares pointer input with WPF. See <see cref="Gestures"/>.</summary>
    public static readonly DependencyProperty GesturesProperty = DependencyProperty.Register(
        nameof(Gestures), typeof(GesturesMode), typeof(DrawnUiElement),
        new FrameworkPropertyMetadata(GesturesMode.Enabled, (d, e) => ((DrawnUiElement)d).Canvas.Gestures = (GesturesMode)e.NewValue));

    /// <summary>
    /// Same modes as <c>Canvas.Gestures</c> on the other heads, in WPF terms:
    /// <list type="bullet">
    /// <item><c>Disabled</c>: the canvas takes no mouse, touch or wheel input; everything stays with WPF.</item>
    /// <item><c>Enabled</c> (default here, a desktop canvas is interactive): input goes to the drawn tree, and
    /// what no drawn control used is left unhandled so WPF parents still get it. A wheel over drawn content
    /// that does not scroll keeps scrolling a hosting <c>ScrollViewer</c>; an unused click still bubbles.</item>
    /// <item><c>Lock</c>: the canvas keeps every pointer event, used or not. For a canvas inside a
    /// <c>ScrollViewer</c> whose own panning / wheel must never move the page.</item>
    /// </list>
    /// Touch is always kept while enabled: WPF decides between touch and pan at TouchDown, before the
    /// drawn tree can know whether it will use the gesture.
    /// </summary>
    public GesturesMode Gestures
    {
        get => (GesturesMode)GetValue(GesturesProperty);
        set => SetValue(GesturesProperty, value);
    }

    private Func<SkiaControl> _contentBuilder;

    /// <summary>
    /// Builds the drawn root. Set it instead of <see cref="Content"/> for code-behind UI: it is called
    /// once now and again after every C# Hot Reload, so an edited <c>Build()</c> shows up without
    /// restarting. The previous tree is disposed. Hot reload never fires in a published app.
    /// </summary>
    public Func<SkiaControl> ContentBuilder
    {
        get => _contentBuilder;
        set
        {
            _contentBuilder = value;
            Rebuild();
        }
    }

    /// <summary>Calls <see cref="ContentBuilder"/> and swaps the drawn root.</summary>
    public void Rebuild()
    {
        if (_contentBuilder == null || _disposed)
            return;

        var previous = Content;
        Content = _contentBuilder();
        if (previous != null && !ReferenceEquals(previous, Content))
            (previous as IDisposable)?.Dispose();
        Canvas.Update();
    }

    private void OnHotReload(Type[] types) => Dispatcher.BeginInvoke(Rebuild);

    private bool InputDisabled => Gestures == GesturesMode.Disabled;

    /// <summary>Lock keeps everything; Enabled keeps only what a drawn control used.</summary>
    private bool KeepInput => Gestures == GesturesMode.Lock || Canvas.LastInputUsed;

    /// <summary>Creates the host and its canvas.</summary>
    public DrawnUiElement()
    {
        EnsureSuperInitialized();

        Canvas = new Canvas { Gestures = GesturesMode.Enabled };

        // The canvas joins the WPF logical tree so DataContext flows into it the ordinary WPF way;
        // DrawnUI then propagates that context down its own tree (SkiaControl.ApplyBindingContext).
        AddLogicalChild(Canvas);

        Focusable = true;
        FocusVisualStyle = null;

        // keyboard focus ring of the accessibility node in focus, above the GPU child
        AddVisualChild(_focusRing);
        Canvas.AccessibilityManager.Changed += OnAccessibilityChanged;
        Canvas.AccessibilityManager.FocusChanged += OnAccessibilityFocusChanged;
        Canvas.AccessibilityManager.LiveRegionUpdated += OnAccessibilityLiveRegion;

        Super.HotReload += OnHotReload;

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

        AppPackageServices.EnsureInstalled(); // relative "package" paths resolve to files next to the exe
        Super.Init();
        ShaderFiles.PreloadAll(); // .sksl files next to the exe become ShaderSource resources
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
    protected override int VisualChildrenCount => _gpuView != null ? 2 : 1;

    /// <inheritdoc/>
    protected override Visual GetVisualChild(int index)
    {
        if (_gpuView != null && index == 0)
            return _gpuView;

        if (index == (_gpuView != null ? 1 : 0))
            return _focusRing;

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    #region Accessibility

    private readonly DrawingVisual _focusRing = new();
    private DrawnUiElementAutomationPeer _peer;
    private static readonly Pen FocusPen = CreateFocusPen();

    private static Pen CreateFocusPen()
    {
        var pen = new Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6E, 0xA8, 0xFE)), 2);
        pen.Freeze();
        return pen;
    }

    /// <inheritdoc/>
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        _peer = new DrawnUiElementAutomationPeer(this);

    private DrawnUiElementAutomationPeer EnsurePeer() =>
        _peer ?? System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(this) as DrawnUiElementAutomationPeer;

    private void OnAccessibilityChanged() => Dispatcher.BeginInvoke(() =>
    {
        if (_peer == null)
            return;

        _peer.NotifyStructureChanged();
        InvalidateFocusRing();
    });

    private void OnAccessibilityFocusChanged(ISkiaAccessibilityNode node) => Dispatcher.BeginInvoke(() =>
    {
        _peer?.NotifyFocusChanged(node);
        InvalidateFocusRing();
    });

    private void OnAccessibilityLiveRegion(ISkiaAccessibilityNode node) => Dispatcher.BeginInvoke(() => _peer?.NotifyLiveRegion(node));

    /// <summary>Redraws the keyboard focus ring around the accessibility node in focus.</summary>
    internal void InvalidateFocusRing()
    {
        using var dc = _focusRing.RenderOpen();
        var focused = _peer?.FocusedPeer;
        if (focused == null || !IsKeyboardFocused)
            return;

        var rect = focused.LocalRect;
        rect.Inflate(2, 2);
        dc.DrawRoundedRectangle(null, FocusPen, rect, 6, 6);
    }

    /// <inheritdoc/>
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        _peer?.ClearVirtualFocus();
        InvalidateFocusRing();
    }

    /// <summary>Tab / Shift+Tab walk the interactive nodes, Enter / Space activate, Escape leaves.</summary>
    private bool HandleAccessibilityKey(KeyEventArgs e)
    {
        var peer = EnsurePeer();
        if (peer == null)
            return false;

        switch (e.Key)
        {
            case Key.Tab:
                var moved = peer.MoveFocus(!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
                InvalidateFocusRing();
                return moved; // false past either end: WPF moves focus out of the canvas

            case Key.Enter or Key.Space when peer.FocusedPeer != null:
                peer.ActivateFocused();
                return true;

            case Key.Escape when peer.FocusedPeer != null:
                peer.ClearVirtualFocus();
                InvalidateFocusRing();
                return true;
        }

        return false;
    }

    #endregion

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
        Super.HotReload -= OnHotReload;
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

        // WPF can raise Rendering more than once for the same frame; only the first one is drawn.
        if (e is RenderingEventArgs args)
        {
            if (args.RenderingTime == _lastRenderingTime)
                return;
            _lastRenderingTime = args.RenderingTime;
        }

        var frameTime = FrameClock(e);

        if (_gpuView != null)
        {
            // CPU pre-rendering, as on the Android and Apple retained views: ANGLE start-up plus the
            // first GPU frame block this thread for a few hundred ms, so one software frame is shown
            // first (OnRender paints it under the still empty GL child) and the GPU starts on the
            // next tick. Costs one software raster at start-up, nothing per frame afterwards.
            if (!_gpuView.HasDrawn && !_prerenderAttempted && Super.IsPrerenderingEnabled)
            {
                _prerenderAttempted = true;
                if (DrawSoftwareFrame())
                {
                    InvalidateVisual();
                    Canvas.Update(); // the canvas is clean after that frame: ask for the GPU one
                    return;
                }
            }

            if (_gpuView.Update(frameTime) && _bitmap != null)
            {
                ReleaseSurface(); // the pre-rendered frame did its job
                InvalidateVisual();
            }

            return;
        }

        DrawSoftwareFrame(frameTime);
    }

    private bool _prerenderAttempted;
    private TimeSpan _lastRenderingTime = TimeSpan.MinValue;
    private long _clockWallNanos;
    private TimeSpan _clockRenderingTime;

    /// <summary>
    /// The frame timestamp animations advance by. WPF's composition tick lands a couple of ms early or
    /// late around the vsync while the frame is shown at the vsync itself, so sampling the wall clock at
    /// the tick puts that jitter into every animated position. <c>RenderingTime</c> is the frame's
    /// presentation time, uniform per frame; it is anchored to the wall clock once so the values stay
    /// comparable with <see cref="Super.GetCurrentTimeNanos"/>.
    /// </summary>
    private long FrameClock(EventArgs e)
    {
        var now = Super.GetCurrentTimeNanos();
        if (e is not RenderingEventArgs args)
            return now;

        if (_clockWallNanos == 0)
        {
            _clockWallNanos = now;
            _clockRenderingTime = args.RenderingTime;
            return now;
        }

        var frame = _clockWallNanos + (args.RenderingTime - _clockRenderingTime).Ticks * 100;

        // A pause (window hidden, breakpoint) is not animated through: re-anchor when the two drift apart.
        if (Math.Abs(frame - now) > 250_000_000)
        {
            _clockWallNanos = now;
            _clockRenderingTime = args.RenderingTime;
            return now;
        }

        return frame;
    }

    /// <summary>Paints one frame into the WriteableBitmap surface. False when there is no surface.</summary>
    private bool DrawSoftwareFrame(long frameTime = 0)
    {
        if (!EnsureSurface())
            return false;

        if (frameTime <= 0)
            frameTime = Super.GetCurrentTimeNanos();
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

        return true;
    }

    /// <inheritdoc/>
    protected override void OnRender(WpfDrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (_gpuView != null)
        {
            // The GL child is not hit-testable, so the host paints an invisible hit area of its own.
            drawingContext.DrawRectangle(Brushes.Transparent, null, new WpfRect(0, 0, ActualWidth, ActualHeight));

            // the pre-rendered software frame, until the first GPU frame covers it
            if (_bitmap != null)
                drawingContext.DrawImage(_bitmap, new WpfRect(0, 0, ActualWidth, ActualHeight));
        }
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

    /// <summary>
    /// Every mouse button presses, drags and taps — a control that only wants the primary button
    /// filters on <c>args.Event.Pointer.Button</c>. One button owns the pointer at a time: a second
    /// button pressed while the first is held is ignored until the first is released.
    /// </summary>
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (_pointerDown || InputDisabled)
            return;

        _peer?.ClearVirtualFocus(); // the pointer takes over from keyboard navigation
        InvalidateFocusRing();
        Focus();
        CaptureMouse();
        _pointerDown = true;
        _pressedButton = e.ChangedButton;

        var point = ToCanvasPixels(e.GetPosition(this));
        Canvas.HandleDesktopPointerDown(point.X, point.Y, ClientPixelWidth, ClientPixelHeight,
            DescribePointer(e.ChangedButton, AppoMobi.Gestures.MouseButtonState.Pressed, e));
        e.Handled = KeepInput; // Enabled: a press no drawn control used still bubbles to WPF
    }

    /// <inheritdoc/>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (InputDisabled)
            return;


        _lastPointer = e.GetPosition(this);
        var point = ToCanvasPixels(_lastPointer);
        var pointer = _pointerDown ? DescribePointer(_pressedButton, AppoMobi.Gestures.MouseButtonState.Pressed, e) : null;
        Canvas.HandleDesktopPointerMove(point.X, point.Y, _pointerDown, ClientPixelWidth, ClientPixelHeight, pointer);
    }

    /// <inheritdoc/>
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (!_pointerDown || e.ChangedButton != _pressedButton)
            return;

        _pointerDown = false;
        ReleaseMouseCapture();

        var point = ToCanvasPixels(e.GetPosition(this));
        Canvas.HandleDesktopPointerUp(point.X, point.Y, ClientPixelWidth, ClientPixelHeight,
            DescribePointer(e.ChangedButton, AppoMobi.Gestures.MouseButtonState.Released, e));

        // A handled right-button release never becomes ContextMenuOpening; leave that one to WPF so
        // the right click still reaches SkiaControl.ContextMenu (see OnContextMenuOpening).
        e.Handled = KeepInput && e.ChangedButton != WpfMouseButton.Right;
    }

    /// <summary>
    /// WPF button → gesture pointer. Button numbers follow the DOM convention the browser heads use
    /// (0 left, 1 middle, 2 right, 3/4 the side buttons), so app code filters the same way everywhere.
    /// </summary>
    private static PointerData DescribePointer(WpfMouseButton button, AppoMobi.Gestures.MouseButtonState state, MouseEventArgs e)
    {
        var (mapped, number) = button switch
        {
            WpfMouseButton.Left => (AppoMobi.Gestures.MouseButton.Left, 0),
            WpfMouseButton.Middle => (AppoMobi.Gestures.MouseButton.Middle, 1),
            WpfMouseButton.Right => (AppoMobi.Gestures.MouseButton.Right, 2),
            WpfMouseButton.XButton1 => (AppoMobi.Gestures.MouseButton.XButton1, 3),
            _ => (AppoMobi.Gestures.MouseButton.XButton2, 4),
        };

        var pressed = MouseButtons.None;
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) pressed |= MouseButtons.Left;
        if (e.RightButton == System.Windows.Input.MouseButtonState.Pressed) pressed |= MouseButtons.Right;
        if (e.MiddleButton == System.Windows.Input.MouseButtonState.Pressed) pressed |= MouseButtons.Middle;
        if (e.XButton1 == System.Windows.Input.MouseButtonState.Pressed) pressed |= MouseButtons.XButton1;
        if (e.XButton2 == System.Windows.Input.MouseButtonState.Pressed) pressed |= MouseButtons.XButton2;

        return new PointerData
        {
            Button = mapped,
            ButtonNumber = number,
            State = state,
            PressedButtons = pressed,
            DeviceType = e.StylusDevice == null ? PointerDeviceType.Mouse
                : e.StylusDevice.TabletDevice?.Type == TabletDeviceType.Touch ? PointerDeviceType.Touch
                : PointerDeviceType.Pen,
        };
    }

    #region Touch

    private static readonly PointerData TouchPointer = new() { DeviceType = PointerDeviceType.Touch, Button = AppoMobi.Gestures.MouseButton.Left };

    /// <summary>
    /// Touch is handled natively, one pointer per finger, so pinch and rotate reach the drawn tree.
    /// Handling TouchDown also stops WPF promoting the primary finger to mouse events.
    /// </summary>
    protected override void OnTouchDown(TouchEventArgs e)
    {
        base.OnTouchDown(e);
        if (InputDisabled)
            return;

        _peer?.ClearVirtualFocus();
        InvalidateFocusRing();
        Focus();
        CaptureTouch(e.TouchDevice);
        var point = ToCanvasPixels(e.GetTouchPoint(this).Position);
        Canvas.HandleTouchDown(e.TouchDevice.Id, point.X, point.Y, ClientPixelWidth, ClientPixelHeight, TouchPointer);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnTouchMove(TouchEventArgs e)
    {
        base.OnTouchMove(e);
        var point = ToCanvasPixels(e.GetTouchPoint(this).Position);
        Canvas.HandleTouchMove(e.TouchDevice.Id, point.X, point.Y, ClientPixelWidth, ClientPixelHeight, TouchPointer);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnTouchUp(TouchEventArgs e)
    {
        base.OnTouchUp(e);
        var point = ToCanvasPixels(e.GetTouchPoint(this).Position);
        Canvas.HandleTouchUp(e.TouchDevice.Id, point.X, point.Y, ClientPixelWidth, ClientPixelHeight, false, TouchPointer);
        ReleaseTouchCapture(e.TouchDevice);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnLostTouchCapture(TouchEventArgs e)
    {
        base.OnLostTouchCapture(e);
        var point = ToCanvasPixels(e.GetTouchPoint(this).Position);
        Canvas.HandleTouchUp(e.TouchDevice.Id, point.X, point.Y, ClientPixelWidth, ClientPixelHeight, true, TouchPointer);
    }

    #endregion

    /// <inheritdoc/>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (InputDisabled)
            return;

        var point = ToCanvasPixels(e.GetPosition(this));
        Canvas.HandleDesktopWheel(point.X, point.Y, e.Delta, ClientPixelWidth, ClientPixelHeight);
        e.Handled = KeepInput; // an unused wheel goes on to a hosting ScrollViewer unless locked
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

        // Ctrl+letter arrives as a control character: not a printable KeyChar, not editor text.
        if (text.Length == 1 && char.IsControl(text[0]))
            return;

        KeyboardManager.KeyboardChar(text);

        if (Canvas.FocusedChild is not SkiaEditor)
            return;

        Canvas.HandleDesktopTextInput(text);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        base.OnPreviewKeyUp(e);
        KeyboardManager.KeyboardReleased(KeyboardManager.MapKey(e.Key == Key.System ? e.SystemKey : e.Key));
    }

    /// <inheritdoc/>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        // Every key reaches the shared KeyboardManager (KeyDown / modifier state) first.
        if (!e.IsRepeat)
            KeyboardManager.KeyboardPressed(KeyboardManager.MapKey(e.Key == Key.System ? e.SystemKey : e.Key));

        // Editing keys belong to a focused drawn editor only. Without one they stay with WPF, so
        // Tab keeps cycling focus through the window and arrows keep scrolling a host ScrollViewer.
        if (Canvas.FocusedChild is not SkiaEditor editor)
        {
            if (HandleAccessibilityKey(e))
                e.Handled = true;
            return;
        }

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
            case Key.Up: if (!editor.StubMoveLine(-1, shift)) return; Canvas.Update(); break;
            case Key.Down: if (!editor.StubMoveLine(1, shift)) return; Canvas.Update(); break;
            case Key.Home: Canvas.DesktopEditorMoveToStart(shift); break;
            case Key.End: Canvas.DesktopEditorMoveToEnd(shift); break;
            case Key.A when ctrl: Canvas.DesktopEditorSelectAll(); break;
            case Key.C when ctrl: CopySelection(editor); break;
            case Key.X when ctrl: if (CopySelection(editor)) Canvas.DesktopEditorBackspace(); break;
            case Key.V when ctrl:
                if (Clipboard.ContainsText())
                    Canvas.HandleDesktopTextInput(Clipboard.GetText().Replace("\r\n", "\n"));
                break;
            case Key.Tab: Canvas.HandleDesktopTextInput("    "); break;
            default: return;
        }

        e.Handled = true;
    }

    private static bool CopySelection(SkiaEditor editor)
    {
        var selected = editor.GetSelectedText();
        if (string.IsNullOrEmpty(selected))
            return false;

        Clipboard.SetText(selected);
        return true;
    }

    #endregion
}
