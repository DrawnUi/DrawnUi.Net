using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DrawnUi.Add3d;

/// <summary>
/// The GL ES entry points (Gl, libGLESv2) come from the ANGLE build DrawnUi.Wpf draws with: libGLESv2.dll when the app
/// ships stock ANGLE, else Avalonia's merged av_libglesv2.dll, which DrawnUi.Wpf references.
/// </summary>
static class GlesLibrary
{
    [ModuleInitializer]
    internal static void Register() => NativeLibrary.SetDllImportResolver(typeof(GlesLibrary).Assembly, Resolve);

    static nint Resolve(string name, Assembly assembly, DllImportSearchPath? path)
    {
        if (name != "libGLESv2")
            return 0;
        foreach (var file in new[] { "libGLESv2.dll", "av_libglesv2.dll" })
            if (NativeLibrary.TryLoad(file, assembly, path, out var module))
                return module;
        return 0;
    }
}
