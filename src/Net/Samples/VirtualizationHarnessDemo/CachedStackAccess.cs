using System.Reflection;
using DrawnUi.Draw;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Shared helpers for the repros. They opt a stack into the double-buffer plane path: <see cref="SkiaCachedStack"/> keeps that switch
/// protected (off unless a subclass opts in), so the harness sets it by reflection, as <c>PlaneSpy</c> reads
/// the plane internals.
/// </summary>
static class CachedStackAccess
{
    static readonly FieldInfo AutoDoubleBufferingField =
        typeof(SkiaCachedStack).GetField("AutoDoubleBuffering", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(SkiaCachedStack), "AutoDoubleBuffering");

    public static bool GetAutoDoubleBuffering(SkiaLayout stack) => (bool)AutoDoubleBufferingField.GetValue(stack)!;

    public static void SetAutoDoubleBuffering(SkiaLayout stack, bool value) => AutoDoubleBufferingField.SetValue(stack, value);

    /// <summary>Where repros save their PNGs: a temp folder that exists on any machine.</summary>
    public static string OutputDir
    {
        get
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "drawnui-harness");
            System.IO.Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
