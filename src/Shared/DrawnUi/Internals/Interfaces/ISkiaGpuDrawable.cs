namespace DrawnUi.Draw;

/// <summary>
/// A canvas view that draws with a GPU context of its own (not a <c>SkiaViewAccelerated</c>), such as the WebGL view
/// of the WebAssembly head or the OpenGL drawable of OpenTK. <c>DrawnView.GetGRContext</c> reads it, so GPU caches are
/// made on that context.
/// </summary>
public interface ISkiaGpuDrawable
{
    /// <summary>
    /// The GPU context the view draws with, null while it draws on the CPU.
    /// </summary>
    GRContext GRContext { get; }
}
