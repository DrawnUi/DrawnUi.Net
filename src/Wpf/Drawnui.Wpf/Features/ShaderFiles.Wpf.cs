using System.IO;
using DrawnUi.Infrastructure;

namespace DrawnUi.Wpf;

/// <summary>
/// Makes <c>ShaderSource="shaders/blit.sksl"</c> work on WPF the way it does with MAUI raw
/// resources: the shared <see cref="SkSl"/> loader has no file access on this head (it serves
/// browser fetches), so every <c>.sksl</c> file copied next to the executable is read once into
/// <see cref="SkSl.LoadedCache"/> under its relative path, with both slash styles.
/// </summary>
public static class ShaderFiles
{
    private static bool _loaded;

    /// <summary>Loads every .sksl under the application base directory. Idempotent.</summary>
    public static void PreloadAll() => Preload(AppContext.BaseDirectory);

    /// <summary>Loads every .sksl under <paramref name="root"/>, keyed relative to it.</summary>
    public static void Preload(string root)
    {
        if (_loaded || !Directory.Exists(root))
            return;

        _loaded = true;
        foreach (var file in Directory.EnumerateFiles(root, "*.sksl", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file);
            var code = File.ReadAllText(file);
            SkSl.LoadedCache[relative.Replace('\\', '/')] = code;
            SkSl.LoadedCache[relative.Replace('/', '\\')] = code;
        }
    }
}
