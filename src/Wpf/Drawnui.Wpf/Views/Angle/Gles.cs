// Subset of the MAUI head's Gles.cs (src/Maui/DrawnUi/Platforms/Windows/Views/Gles/Gles.cs, forked from
// SkiaSharp, MIT): only what the WPF surface needs to describe its framebuffer to Skia and to flush.
using System.Runtime.InteropServices;

namespace DrawnUi.Views.Angle;

internal static class Gles
{
	private const string libGLESv2 = "libGLESv2.dll";

	public const uint GL_FRAMEBUFFER_BINDING = 36006;

	public const uint GL_STENCIL_BITS = 3415;

	public const uint GL_SAMPLES = 32937;

	[DllImport(libGLESv2)]
	public static extern void glGetIntegerv(uint pname, out int data);

	[DllImport(libGLESv2)]
	public static extern void glViewport(int x, int y, int width, int height);

	[DllImport(libGLESv2)]
	public static extern void glFlush();

	[DllImport(libGLESv2)]
	public static extern void glFinish();
}
