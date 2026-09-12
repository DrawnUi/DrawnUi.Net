using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DrawnUi.Views.Angle;
using SkiaSharp;
using WpfDrawingContext = System.Windows.Media.DrawingContext;
using WpfRect = System.Windows.Rect;

namespace DrawnUi.Draw;

/// <summary>
/// The GPU surface of the WPF head: ANGLE (OpenGL ES over Direct3D 11) drawing into a Direct3D
/// texture that WPF composes through <see cref="D3DImage"/>. Same GPU stack as the MAUI Windows head,
/// so shaders, caches and their quirks are one and the same; no vendor OpenGL driver involved, WARP
/// and remote sessions included.
/// <para>
/// Shared code recognises acceleration by testing <c>CanvasView is SkiaViewAccelerated</c> and reading
/// <see cref="GRContext"/> (see <c>DrawnView.CreateSurface</c>): GPU cache types get real GPU surfaces.
/// </para>
/// <para>
/// The texture persists between frames (it is a pbuffer, not a swap chain), so retained rendering is
/// inherent — nothing is copied to keep the last frame. Frames are produced only on
/// <see cref="Update"/>, which the host calls on its composition tick when the canvas is dirty.
/// </para>
/// </summary>
public partial class SkiaViewAccelerated : FrameworkElement, ISkiaDrawable
{
	private const SKColorType ColorType = SKColorType.Rgba8888;
	private const GRSurfaceOrigin SurfaceOrigin = GRSurfaceOrigin.TopLeft;

	private readonly Guid _uid = Guid.NewGuid();
	private readonly D3DImage _image;
	private AngleContext _gl;
	private nint _d3d;
	private nint _device;
	private nint _texture;
	private nint _d3dSurface;
	private GRBackendRenderTarget _renderTarget;
	private SKSurface _surface;
	private int _width;
	private int _height;
	private long _lastFrameNanos;
	private bool _failed;

	/// <summary>Creates the view. GPU objects are created on the first frame that has a size.</summary>
	public SkiaViewAccelerated()
	{
		var dpi = VisualTreeHelper.GetDpi(this);
		_image = new D3DImage(96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY);
		_image.IsFrontBufferAvailableChanged += OnFrontBufferAvailableChanged;
		Unloaded += (_, _) => ReleaseSurface();
	}

	/// <summary>The GPU context DrawnUI creates its cache surfaces on. Null until the first frame.</summary>
	public GRContext GRContext { get; private set; }

	/// <summary>Called by DrawnUI to paint one frame into the supplied surface.</summary>
	public Func<SKSurface, SKRect, bool> OnDraw { get; set; }

	/// <summary>Surface over the shared Direct3D texture.</summary>
	public SKSurface Surface => _surface;

	/// <summary>Always true — this view renders on the GPU.</summary>
	public bool IsHardwareAccelerated => true;

	/// <summary>Smoothed frames per second.</summary>
	public double FPS { get; private set; }

	/// <summary>True while a frame is being painted.</summary>
	public bool IsDrawing { get; private set; }

	/// <summary>True once at least one frame has been painted.</summary>
	public bool HasDrawn { get; private set; }

	/// <summary>Timestamp of the frame being drawn, in nanoseconds.</summary>
	public long FrameTime { get; private set; }

	/// <summary>Surface size in pixels.</summary>
	public SKSize CanvasSize => new(_width, _height);

	Guid ISkiaDrawable.Uid => _uid;

	/// <summary>
	/// Paints one frame now and hands it to WPF. Returns false when there is no surface to paint into.
	/// </summary>
	public bool Update(long nanos = 0)
	{
		if (_failed || OnDraw == null || !Super.EnableRendering || !_image.IsFrontBufferAvailable)
			return false;

		if (!EnsureSurface())
			return false;

		IsDrawing = true;
		try
		{
			_gl.MakeCurrent();
			GRContext.ResetContext();

			SignalFrame(nanos > 0 ? nanos : Super.GetCurrentTimeNanos());
			OnDraw.Invoke(_surface, new SKRect(0, 0, _width, _height));
			_surface.Canvas.Flush();
			GRContext.Flush();
			// The texture is read by WPF's D3D9 device on another queue: the GL work has to be
			// submitted before the dirty rect is announced.
			Gles.glFlush();

			_image.Lock();
			try
			{
				_image.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
			}
			finally
			{
				_image.Unlock();
			}

			HasDrawn = true;
			return true;
		}
		catch (Exception e)
		{
			Super.Log(e);
			_failed = true;
			return false;
		}
		finally
		{
			IsDrawing = false;
		}
	}

	/// <summary>Records the frame timestamp and updates the FPS average.</summary>
	public void SignalFrame(long nanoseconds)
	{
		if (_lastFrameNanos > 0 && nanoseconds > _lastFrameNanos)
		{
			var delta = nanoseconds - _lastFrameNanos;
			var instant = 1_000_000_000.0 / delta;
			FPS = FPS * 0.9 + instant * 0.1; // exponential moving average
		}

		_lastFrameNanos = nanoseconds;
		FrameTime = nanoseconds;
	}

	private bool EnsureSurface()
	{
		var dpi = VisualTreeHelper.GetDpi(this);
		var width = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
		var height = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);

		if (width <= 0 || height <= 0)
			return false;

		if (_surface != null && width == _width && height == _height)
			return true;

		ReleaseSurface();

		if (_device == IntPtr.Zero)
			D3D9Ex.CreateDevice(out _d3d, out _device);

		_gl ??= new AngleContext();

		D3D9Ex.CreateSharedRenderTarget(_device, width, height, out _texture, out _d3dSurface, out var shareHandle);
		_gl.CreateSurface(shareHandle, width, height);
		_gl.MakeCurrent();

		GRContext ??= GRContext.CreateGl(GRGlInterface.CreateAngle(Egl.GetProc))
			?? throw new InvalidOperationException("Skia could not bind the ANGLE GL interface");

		Gles.glGetIntegerv(Gles.GL_FRAMEBUFFER_BINDING, out var framebuffer);
		Gles.glGetIntegerv(Gles.GL_STENCIL_BITS, out var stencil);
		Gles.glGetIntegerv(Gles.GL_SAMPLES, out var samples);
		samples = Math.Min(samples, GRContext.GetMaxSurfaceSampleCount(ColorType));

		var info = new GRGlFramebufferInfo((uint)framebuffer, ColorType.ToGlSizedFormat());
		_renderTarget = new GRBackendRenderTarget(width, height, samples, stencil, info);
		_surface = SKSurface.Create(GRContext, _renderTarget, SurfaceOrigin, ColorType)
			?? throw new InvalidOperationException("Skia could not wrap the ANGLE framebuffer");

		_width = width;
		_height = height;

		_image.Lock();
		try
		{
			// Software fallback keeps the image alive when the front buffer is unavailable (remote
			// desktop, device removal); a D3D9Ex surface is what makes that flag legal.
			_image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, _d3dSurface, true);
		}
		finally
		{
			_image.Unlock();
		}

		InvalidateVisual();
		return true;
	}

	private void ReleaseSurface()
	{
		if (_surface == null && _texture == IntPtr.Zero)
			return;

		_surface?.Dispose();
		_surface = null;
		_renderTarget?.Dispose();
		_renderTarget = null;

		_gl?.DestroySurface();

		if (_image.IsFrontBufferAvailable)
		{
			_image.Lock();
			try
			{
				_image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
			}
			finally
			{
				_image.Unlock();
			}
		}

		D3D9Ex.Release(ref _d3dSurface);
		D3D9Ex.Release(ref _texture);
		_width = 0;
		_height = 0;
	}

	private void OnFrontBufferAvailableChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if ((bool)e.NewValue)
		{
			// Device came back: everything GPU-side is stale, including Skia's caches.
			ReleaseSurface();
			Super.NeedGlobalUpdate();
		}
	}

	/// <inheritdoc/>
	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
	{
		base.OnRenderSizeChanged(sizeInfo);
		ReleaseSurface();
	}

	/// <inheritdoc/>
	protected override void OnRender(WpfDrawingContext drawingContext)
	{
		base.OnRender(drawingContext);

		if (_surface != null)
			drawingContext.DrawImage(_image, new WpfRect(0, 0, ActualWidth, ActualHeight));
	}

	/// <summary>Releases surface, Skia context, EGL context and Direct3D objects.</summary>
	public void Dispose()
	{
		_image.IsFrontBufferAvailableChanged -= OnFrontBufferAvailableChanged;
		ReleaseSurface();

		if (GRContext != null)
		{
			_gl?.MakeCurrent();
			GRContext.AbandonContext(false);
			GRContext.Dispose();
			GRContext = null;
		}

		_gl?.Dispose();
		_gl = null;

		D3D9Ex.Release(ref _device);
		D3D9Ex.Release(ref _d3d);
	}
}
