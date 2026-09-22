// Constants and API shape forked from SkiaSharp (MIT, https://github.com/mono/SkiaSharp) SkiaSharp.Views.Windows
// 4.148 through the DrawnUi MAUI head's copy (src/Maui/DrawnUi/Platforms/Windows/Views/Gles/Egl.cs).
//
// Bound at runtime rather than through DllImport because the two ANGLE distributions name things
// differently: stock ANGLE ships libEGL.dll exporting egl*, Avalonia.Angle.Windows.Natives ships one merged
// av_libglesv2.dll exporting EGL_* next to gl*. Both are accepted, stock first.
using System.Runtime.InteropServices;

namespace DrawnUi.Views.Angle;

internal static unsafe class Egl
{
	public static readonly nint EGL_DEFAULT_DISPLAY = IntPtr.Zero;

	public static readonly nint EGL_NO_CONFIG = IntPtr.Zero;

	public static readonly nint EGL_NO_DISPLAY = IntPtr.Zero;

	public static readonly nint EGL_NO_CONTEXT = IntPtr.Zero;

	public static readonly nint EGL_NO_SURFACE = IntPtr.Zero;

	public const int EGL_FALSE = 0;

	public const int EGL_TRUE = 1;

	public const int EGL_SUCCESS = 12288;

	public const int EGL_BUFFER_SIZE = 12320;

	public const int EGL_ALPHA_SIZE = 12321;

	public const int EGL_BLUE_SIZE = 12322;

	public const int EGL_GREEN_SIZE = 12323;

	public const int EGL_RED_SIZE = 12324;

	public const int EGL_DEPTH_SIZE = 12325;

	public const int EGL_STENCIL_SIZE = 12326;

	public const int EGL_HEIGHT = 12374;

	public const int EGL_WIDTH = 12375;

	public const int EGL_NONE = 12344;

	public const int EGL_CONTEXT_CLIENT_VERSION = 12440;

	public const int EGL_SWAP_BEHAVIOR = 12435;

	public const int EGL_BUFFER_PRESERVED = 12436;

	public const int EGL_BUFFER_DESTROYED = 12437;

	public const int EGL_OPENGL_ES_API = 12448;

	public const int EGL_RENDERABLE_TYPE = 12352;

	public const int EGL_OPENGL_ES2_BIT = 4;

	public const int EGL_SURFACE_TYPE = 12339;

	public const int EGL_PBUFFER_BIT = 1;

	public const int EGL_WINDOW_BIT = 4;

	public const int EGL_SWAP_BEHAVIOR_PRESERVED_BIT = 1024;

	public const int EGL_TEXTURE_FORMAT = 0x3080;

	public const int EGL_TEXTURE_TARGET = 0x3081;

	public const int EGL_TEXTURE_RGBA = 0x305E;

	public const int EGL_TEXTURE_2D = 0x305F;

	public const int EGL_EXPERIMENTAL_PRESENT_PATH_ANGLE = 13220;

	public const int EGL_EXPERIMENTAL_PRESENT_PATH_FAST_ANGLE = 13226;

	public const int EGL_PLATFORM_ANGLE_TYPE_ANGLE = 12803;

	public const int EGL_PLATFORM_ANGLE_MAX_VERSION_MAJOR_ANGLE = 12804;

	public const int EGL_PLATFORM_ANGLE_MAX_VERSION_MINOR_ANGLE = 12805;

	public const int EGL_PLATFORM_ANGLE_TYPE_DEFAULT_ANGLE = 12806;

	public const int EGL_PLATFORM_ANGLE_ANGLE = 12802;

	public const int EGL_PLATFORM_ANGLE_TYPE_D3D9_ANGLE = 12807;

	public const int EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE = 12808;

	public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_ANGLE = 12809;

	public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_HARDWARE_ANGLE = 12810;

	public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_D3D_WARP_ANGLE = 12811;

	public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_D3D_REFERENCE_ANGLE = 12812;

	public const int EGL_PLATFORM_ANGLE_ENABLE_AUTOMATIC_TRIM_ANGLE = 12815;

	public const int EGL_FIXED_SIZE_ANGLE = 12801;

	/// <summary>
	/// EGL_ANGLE_d3d_share_handle_client_buffer: a pbuffer created over a shared Direct3D texture,
	/// which is how the surface reaches WPF's D3DImage.
	/// </summary>
	public const int EGL_D3D_TEXTURE_2D_SHARE_HANDLE_ANGLE = 0x3200;

	/// <summary>The loaded ANGLE module. Also serves the gl* imports in <see cref="Gles"/>.</summary>
	public static readonly nint Module;

	private static readonly delegate* unmanaged[Stdcall]<byte*, nint> _getProcAddress;
	private static readonly delegate* unmanaged[Stdcall]<uint, nint, int*, nint> _getPlatformDisplayEXT;
	private static readonly delegate* unmanaged[Stdcall]<nint, int*, int*, int> _initialize;
	private static readonly delegate* unmanaged[Stdcall]<nint, int*, nint*, int, int*, int> _chooseConfig;
	private static readonly delegate* unmanaged[Stdcall]<nint, nint, nint, int*, nint> _createContext;
	private static readonly delegate* unmanaged[Stdcall]<nint, int, nint, nint, int*, nint> _createPbufferFromClientBuffer;
	private static readonly delegate* unmanaged[Stdcall]<nint, nint, int, int*, int> _querySurface;
	private static readonly delegate* unmanaged[Stdcall]<nint, nint, int> _destroySurface;
	private static readonly delegate* unmanaged[Stdcall]<nint, nint, nint, nint, int> _makeCurrent;
	private static readonly delegate* unmanaged[Stdcall]<nint, nint, int> _destroyContext;
	private static readonly delegate* unmanaged[Stdcall]<int> _getError;

	static Egl()
	{
		var assembly = typeof(Egl).Assembly;

		foreach (var name in new[] { "libEGL.dll", "av_libglesv2.dll" })
		{
			if (NativeLibrary.TryLoad(name, assembly, null, out Module))
				break;
		}

		if (Module == IntPtr.Zero)
			throw new DllNotFoundException("ANGLE not found: neither libEGL.dll nor av_libglesv2.dll could be loaded.");

		_getProcAddress = (delegate* unmanaged[Stdcall]<byte*, nint>)Bind("GetProcAddress");
		_getPlatformDisplayEXT = (delegate* unmanaged[Stdcall]<uint, nint, int*, nint>)Bind("GetPlatformDisplayEXT");
		_initialize = (delegate* unmanaged[Stdcall]<nint, int*, int*, int>)Bind("Initialize");
		_chooseConfig = (delegate* unmanaged[Stdcall]<nint, int*, nint*, int, int*, int>)Bind("ChooseConfig");
		_createContext = (delegate* unmanaged[Stdcall]<nint, nint, nint, int*, nint>)Bind("CreateContext");
		_createPbufferFromClientBuffer = (delegate* unmanaged[Stdcall]<nint, int, nint, nint, int*, nint>)Bind("CreatePbufferFromClientBuffer");
		_querySurface = (delegate* unmanaged[Stdcall]<nint, nint, int, int*, int>)Bind("QuerySurface");
		_destroySurface = (delegate* unmanaged[Stdcall]<nint, nint, int>)Bind("DestroySurface");
		_makeCurrent = (delegate* unmanaged[Stdcall]<nint, nint, nint, nint, int>)Bind("MakeCurrent");
		_destroyContext = (delegate* unmanaged[Stdcall]<nint, nint, int>)Bind("DestroyContext");
		_getError = (delegate* unmanaged[Stdcall]<int>)Bind("GetError");

		NativeLibrary.SetDllImportResolver(assembly, ResolveGles);
	}

	/// <summary>Stock export name first (egl*), then the merged-binary name (EGL_*).</summary>
	private static nint Bind(string name)
	{
		if (NativeLibrary.TryGetExport(Module, "egl" + name, out var export))
			return export;

		if (NativeLibrary.TryGetExport(Module, "EGL_" + name, out export))
			return export;

		throw new EntryPointNotFoundException($"ANGLE export egl{name} / EGL_{name} not found.");
	}

	/// <summary>
	/// libGLESv2.dll when an app ships stock ANGLE, otherwise the module already loaded for EGL —
	/// the merged binary exports gl* as well.
	/// </summary>
	private static nint ResolveGles(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
	{
		if (libraryName != "libGLESv2.dll")
			return IntPtr.Zero;

		return NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var stock) ? stock : Module;
	}

	/// <summary>
	/// Procedure lookup for Skia's GL interface: eglGetProcAddress first, module exports second —
	/// what SkiaSharp's own ANGLE loader does, which is not used because it insists on the stock file names.
	/// </summary>
	public static nint GetProc(string name)
	{
		var proc = eglGetProcAddress(name);
		if (proc != IntPtr.Zero)
			return proc;

		return NativeLibrary.TryGetExport(Module, name, out var export) ? export : IntPtr.Zero;
	}

	public static nint eglGetProcAddress(string procname)
	{
		var bytes = System.Text.Encoding.ASCII.GetBytes(procname + "\0");
		fixed (byte* p = bytes)
			return _getProcAddress(p);
	}

	public static nint eglGetPlatformDisplayEXT(uint platform, nint native_display, int[] attrib_list)
	{
		fixed (int* a = attrib_list)
			return _getPlatformDisplayEXT(platform, native_display, a);
	}

	public static int eglInitialize(nint dpy, out int major, out int minor)
	{
		int maj, min;
		var result = _initialize(dpy, &maj, &min);
		major = maj;
		minor = min;
		return result;
	}

	public static int eglChooseConfig(nint dpy, int[] attrib_list, nint[] configs, int config_size, out int num_config)
	{
		int count;
		int result;
		fixed (int* a = attrib_list)
		fixed (nint* c = configs)
			result = _chooseConfig(dpy, a, c, config_size, &count);
		num_config = count;
		return result;
	}

	public static nint eglCreateContext(nint dpy, nint config, nint share_context, int[] attrib_list)
	{
		fixed (int* a = attrib_list)
			return _createContext(dpy, config, share_context, a);
	}

	public static nint eglCreatePbufferFromClientBuffer(nint dpy, int buftype, nint buffer, nint config, int[] attrib_list)
	{
		fixed (int* a = attrib_list)
			return _createPbufferFromClientBuffer(dpy, buftype, buffer, config, a);
	}

	public static int eglQuerySurface(nint dpy, nint surface, int attribute, out int value)
	{
		int v;
		var result = _querySurface(dpy, surface, attribute, &v);
		value = v;
		return result;
	}

	public static int eglDestroySurface(nint dpy, nint surface) => _destroySurface(dpy, surface);

	public static int eglMakeCurrent(nint dpy, nint draw, nint read, nint ctx) => _makeCurrent(dpy, draw, read, ctx);

	public static int eglDestroyContext(nint dpy, nint ctx) => _destroyContext(dpy, ctx);

	public static int eglGetError() => _getError();
}
