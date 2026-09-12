using System.Runtime.InteropServices;

namespace DrawnUi.Views.Angle;

/// <summary>
/// The Direct3D 9Ex side of the ANGLE ⇄ WPF bridge. WPF composes through D3D9Ex, and
/// <see cref="System.Windows.Interop.D3DImage"/> accepts nothing but an <c>IDirect3DSurface9</c>;
/// a render-target texture created here with a share handle is the one object both ANGLE (D3D11,
/// through <c>EGL_ANGLE_d3d_share_handle_client_buffer</c>) and WPF can address.
/// <para>
/// Raw vtable calls rather than a COM interop package: five methods are needed, none of them worth a
/// dependency.
/// </para>
/// </summary>
internal static unsafe class D3D9Ex
{
	private const uint D3D_SDK_VERSION = 32;
	private const uint D3DDEVTYPE_HAL = 1;
	private const uint D3DFMT_A8R8G8B8 = 21;
	private const uint D3DPOOL_DEFAULT = 0;
	private const uint D3DUSAGE_RENDERTARGET = 1;
	private const uint D3DSWAPEFFECT_DISCARD = 1;
	private const uint D3DCREATE_FPU_PRESERVE = 0x2;
	private const uint D3DCREATE_MULTITHREADED = 0x4;
	private const uint D3DCREATE_HARDWARE_VERTEXPROCESSING = 0x40;

	// vtable slots
	private const int IDirect3D9Ex_CreateDeviceEx = 20;
	private const int IDirect3DDevice9_CreateTexture = 23;
	private const int IDirect3DTexture9_GetSurfaceLevel = 18;

	[StructLayout(LayoutKind.Sequential)]
	private struct D3DPRESENT_PARAMETERS
	{
		public uint BackBufferWidth;
		public uint BackBufferHeight;
		public uint BackBufferFormat;
		public uint BackBufferCount;
		public uint MultiSampleType;
		public uint MultiSampleQuality;
		public uint SwapEffect;
		public nint hDeviceWindow;
		public int Windowed;
		public int EnableAutoDepthStencil;
		public uint AutoDepthStencilFormat;
		public uint Flags;
		public uint FullScreen_RefreshRateInHz;
		public uint PresentationInterval;
	}

	[DllImport("d3d9.dll")]
	private static extern int Direct3DCreate9Ex(uint sdkVersion, out nint d3d);

	[DllImport("user32.dll")]
	private static extern nint GetDesktopWindow();

	private static void* Slot(nint com, int index) => (*(void***)com)[index];

	/// <summary>Creates the D3D9Ex object and a windowed device on the default adapter.</summary>
	public static void CreateDevice(out nint d3d, out nint device)
	{
		Marshal.ThrowExceptionForHR(Direct3DCreate9Ex(D3D_SDK_VERSION, out d3d));

		var window = GetDesktopWindow();
		var parameters = new D3DPRESENT_PARAMETERS
		{
			BackBufferWidth = 1,
			BackBufferHeight = 1,
			BackBufferCount = 1,
			SwapEffect = D3DSWAPEFFECT_DISCARD,
			hDeviceWindow = window,
			Windowed = 1,
		};

		var createDeviceEx = (delegate* unmanaged[Stdcall]<nint, uint, uint, nint, uint, D3DPRESENT_PARAMETERS*, nint, nint*, int>)
			Slot(d3d, IDirect3D9Ex_CreateDeviceEx);

		nint created;
		var hr = createDeviceEx(d3d, 0, D3DDEVTYPE_HAL, window,
			D3DCREATE_HARDWARE_VERTEXPROCESSING | D3DCREATE_MULTITHREADED | D3DCREATE_FPU_PRESERVE,
			&parameters, IntPtr.Zero, &created);

		if (hr < 0)
		{
			Marshal.Release(d3d);
			d3d = IntPtr.Zero;
			Marshal.ThrowExceptionForHR(hr);
		}

		device = created;
	}

	/// <summary>
	/// A shared A8R8G8B8 render-target texture: <paramref name="surface"/> goes to D3DImage,
	/// <paramref name="shareHandle"/> to ANGLE. Release both texture and surface with <see cref="Release"/>.
	/// </summary>
	public static void CreateSharedRenderTarget(nint device, int width, int height, out nint texture, out nint surface, out nint shareHandle)
	{
		var createTexture = (delegate* unmanaged[Stdcall]<nint, uint, uint, uint, uint, uint, uint, nint*, nint*, int>)
			Slot(device, IDirect3DDevice9_CreateTexture);

		nint tex;
		nint share = IntPtr.Zero;
		Marshal.ThrowExceptionForHR(createTexture(device, (uint)width, (uint)height, 1, D3DUSAGE_RENDERTARGET, D3DFMT_A8R8G8B8, D3DPOOL_DEFAULT, &tex, &share));

		var getSurfaceLevel = (delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)Slot(tex, IDirect3DTexture9_GetSurfaceLevel);

		nint level;
		var hr = getSurfaceLevel(tex, 0, &level);
		if (hr < 0)
		{
			Marshal.Release(tex);
			Marshal.ThrowExceptionForHR(hr);
		}

		texture = tex;
		surface = level;
		shareHandle = share;
	}

	/// <summary>Releases a COM pointer, tolerating zero.</summary>
	public static void Release(ref nint com)
	{
		if (com == IntPtr.Zero)
			return;

		Marshal.Release(com);
		com = IntPtr.Zero;
	}
}
