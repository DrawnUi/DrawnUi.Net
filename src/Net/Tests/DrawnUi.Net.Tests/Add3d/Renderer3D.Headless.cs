using System.Numerics;
using DrawnUi.Views;
using SkiaSharp;

namespace DrawnUi.Add3d;

/// <summary>
/// The headless build of the 3D add-on: no GL, so the renderer draws nothing (the controls' CPU side, reading,
/// posing, animations and hit tests, is what the tests drive). The real renderer is SharedAdd3d/Renderer3D.cs.
/// </summary>
internal static class Renderer3D
{
    public static SKImage? Draw(DrawnView canvas, GRContext gr, RenderTarget target, RenderItem[] items, int count, in RenderStyle style, in Matrix4x4 viewProj, int w, int h) => null;

    public static void Release(RenderTarget target)
    {
    }
}
