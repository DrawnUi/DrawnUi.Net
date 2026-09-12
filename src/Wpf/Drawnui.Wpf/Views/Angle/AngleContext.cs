// Forked from SkiaSharp (MIT, https://github.com/mono/SkiaSharp) SkiaSharp.Views.Windows 4.148 through the
// DrawnUi MAUI head's GlesContext (src/Maui/DrawnUi/Platforms/Windows/Views/Gles/GlesContext.cs). Display
// and context setup are kept as they are there — D3D11 first, feature level 9.3 next, WARP last. Only the
// surface differs: WPF has no SwapChainPanel, so the surface is a pbuffer over a shared Direct3D texture.

namespace DrawnUi.Views.Angle;

internal class AngleContext : IDisposable
{
	private static nint eglDisplay = Egl.EGL_NO_DISPLAY;

	private bool isDisposed;

	private nint eglContext;

	private nint eglSurface;

	private nint eglConfig;

	public bool HasSurface => eglSurface != Egl.EGL_NO_SURFACE;

	public AngleContext()
	{
		eglConfig = Egl.EGL_NO_CONFIG;
		eglContext = Egl.EGL_NO_CONTEXT;
		eglSurface = Egl.EGL_NO_SURFACE;
		InitializeDisplay();
		Initialize();
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!isDisposed)
		{
			DestroySurface();
			Cleanup();
			isDisposed = true;
		}
	}

	~AngleContext()
	{
		Dispose(disposing: false);
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Creates the drawing surface over a Direct3D share handle (EGL_ANGLE_d3d_share_handle_client_buffer).
	/// The texture keeps its content between frames — a pbuffer is not a swap chain — so nothing has to
	/// be retained by hand and partial redraws just work.
	/// </summary>
	public void CreateSurface(nint shareHandle, int width, int height)
	{
		if (shareHandle == IntPtr.Zero)
		{
			throw new ArgumentException("Share handle is invalid", nameof(shareHandle));
		}

		int[] attrib_list =
		{
			Egl.EGL_WIDTH, width,
			Egl.EGL_HEIGHT, height,
			Egl.EGL_TEXTURE_FORMAT, Egl.EGL_TEXTURE_RGBA,
			Egl.EGL_TEXTURE_TARGET, Egl.EGL_TEXTURE_2D,
			Egl.EGL_NONE,
		};

		var surface = Egl.eglCreatePbufferFromClientBuffer(eglDisplay, Egl.EGL_D3D_TEXTURE_2D_SHARE_HANDLE_ANGLE, shareHandle, eglConfig, attrib_list);
		if (surface == Egl.EGL_NO_SURFACE)
		{
			throw new Exception($"Failed to create EGL surface from share handle (EGL error 0x{Egl.eglGetError():X})");
		}

		eglSurface = surface;
	}

	public void GetSurfaceDimensions(out int width, out int height)
	{
		Egl.eglQuerySurface(eglDisplay, eglSurface, Egl.EGL_WIDTH, out width);
		Egl.eglQuerySurface(eglDisplay, eglSurface, Egl.EGL_HEIGHT, out height);
	}

	public void SetViewportSize(int width, int height)
	{
		Gles.glViewport(0, 0, width, height);
	}

	public void DestroySurface()
	{
		if (eglDisplay != Egl.EGL_NO_DISPLAY && eglSurface != Egl.EGL_NO_SURFACE)
		{
			Egl.eglMakeCurrent(eglDisplay, Egl.EGL_NO_SURFACE, Egl.EGL_NO_SURFACE, Egl.EGL_NO_CONTEXT);
			Egl.eglDestroySurface(eglDisplay, eglSurface);
			eglSurface = Egl.EGL_NO_SURFACE;
		}
	}

	public void MakeCurrent()
	{
		if (Egl.eglMakeCurrent(eglDisplay, eglSurface, eglSurface, eglContext) == 0)
		{
			throw new Exception("Failed to make EGLSurface current");
		}
	}

	public void Reset()
	{
		Cleanup();
		Initialize();
	}

	private void InitializeDisplay()
	{
		if (eglDisplay != Egl.EGL_NO_DISPLAY)
		{
			return;
		}
		int[] attrib_list = new int[7] { 12803, 12808, 13220, 13226, 12815, 1, 12344 };
		int[] attrib_list2 = new int[11]
		{
			12803, 12808, 12804, 9, 12805, 3, 13220, 13226, 12815, 1,
			12344
		};
		int[] attrib_list3 = new int[9] { 12803, 12808, 12809, 12811, 13220, 13226, 12815, 1, 12344 };
		eglDisplay = Egl.eglGetPlatformDisplayEXT(12802u, Egl.EGL_DEFAULT_DISPLAY, attrib_list);
		if (eglDisplay == Egl.EGL_NO_DISPLAY)
		{
			throw new Exception("Failed to get EGL display");
		}
		if (Egl.eglInitialize(eglDisplay, out var major, out var minor) != 0)
		{
			return;
		}
		eglDisplay = Egl.eglGetPlatformDisplayEXT(12802u, Egl.EGL_DEFAULT_DISPLAY, attrib_list2);
		if (eglDisplay == Egl.EGL_NO_DISPLAY)
		{
			throw new Exception("Failed to get EGL display");
		}
		if (Egl.eglInitialize(eglDisplay, out major, out minor) == 0)
		{
			eglDisplay = Egl.eglGetPlatformDisplayEXT(12802u, Egl.EGL_DEFAULT_DISPLAY, attrib_list3);
			if (eglDisplay == Egl.EGL_NO_DISPLAY)
			{
				throw new Exception("Failed to get EGL display");
			}
			if (Egl.eglInitialize(eglDisplay, out major, out minor) == 0)
			{
				throw new Exception("Failed to initialize EGL");
			}
		}
	}

	public void Initialize()
	{
		// Same 8/8/8/8 + depth 8 + stencil 8 request as the MAUI head, plus the pbuffer surface bit,
		// which is the only surface type this head ever creates.
		int[] attrib_list = new int[15]
		{
			12324, 8, 12323, 8, 12322, 8, 12321, 8, 12325, 8,
			12326, 8, Egl.EGL_SURFACE_TYPE, Egl.EGL_PBUFFER_BIT, 12344
		};
		int[] attrib_list2 = new int[3] { 12440, 2, 12344 };
		nint[] array = new nint[1];
		if (Egl.eglChooseConfig(eglDisplay, attrib_list, array, array.Length, out var num_config) == 0 || num_config == 0)
		{
			throw new Exception("Failed to choose first EGLConfig");
		}
		eglConfig = array[0];
		eglContext = Egl.eglCreateContext(eglDisplay, eglConfig, Egl.EGL_NO_CONTEXT, attrib_list2);
		if (eglContext == Egl.EGL_NO_CONTEXT)
		{
			throw new Exception("Failed to create EGL context");
		}
	}

	private void Cleanup()
	{
		if (eglDisplay != Egl.EGL_NO_DISPLAY && eglContext != Egl.EGL_NO_CONTEXT)
		{
			Egl.eglMakeCurrent(eglDisplay, Egl.EGL_NO_SURFACE, Egl.EGL_NO_SURFACE, Egl.EGL_NO_CONTEXT);
			Egl.eglDestroyContext(eglDisplay, eglContext);
			eglContext = Egl.EGL_NO_CONTEXT;
		}
	}
}
