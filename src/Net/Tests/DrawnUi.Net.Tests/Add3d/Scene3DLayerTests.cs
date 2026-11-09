using SkiaSharp;
using DrawnUi.Add3d;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests.Add3d;

/// <summary>
/// SkiaScene3D (port of DrawnUI for Rust drawnui-add3d tests/layer.rs): many objects on one game field, placed in the
/// layer's points, each with its own animations, every model read once; taps tell which object was hit.
/// </summary>
public class Scene3DLayerTests
{
    sealed class Field : IDisposable
    {
        public readonly HeadlessCanvasHost Host = new(600, 400, 1f, Colors.Black);
        public readonly SkiaScene3D Layer;
        public readonly List<SceneAnimationEnded> Ended = new();
        public readonly List<SceneHit> Hits = new();

        public Field()
        {
            Host.Canvas.Content = new SkiaLayer
            {
                VerticalOptions = LayoutOptions.Fill,
                Children =
                {
                    new SkiaScene3D { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill }
                        .Model("dragon", Add3dAssets.Dragon)
                        .Assign(out Layer)
                        .OnAnimationEnded((_, e) => Ended.Add(e))
                        .OnTapped3D((_, hit) => Hits.Add(hit)),
                }
            };
            Host.RenderFrame(16.7);
        }

        public void Frames(float seconds)
        {
            for (var i = 0; i < (int)(seconds * 60); i++)
                Host.RenderFrame(16.7);
        }

        public void Tap(float x, float y)
        {
            new GestureRobot(Host).Tap(x, y, 16.7);
            Frames(0.1f);
        }

        public void Dispose() => Host.Dispose();
    }

    [Fact]
    public void Many_objects_each_with_its_own_animation_and_taps_by_object()
    {
        using var f = new Field();
        // A crowd: 30 dragons of one model, 60 points tall, standing in rows.
        for (var i = 0; i < 30; i++)
            Assert.True(f.Layer.Add($"mob{i}", "dragon", 40 + i % 10 * 55, 120 + i / 10 * 110, 60));
        Assert.False(f.Layer.Add("x", "no such model", 0, 0, 10));
        Assert.True(f.Layer.PlayOnce("mob3", Add3dAssets.Punch));
        Assert.Equal(30, f.Layer.ObjectNames.Count);
        f.Frames(3);
        Assert.Equal(new[] { new SceneAnimationEnded("mob3", Add3dAssets.Punch) }, f.Ended); // only the one that punched
        Assert.True(f.Layer.IsAnimating, "the crowd keeps idling");

        // mob0 stands at (40, 120), 60 tall: its body is around (40, 95).
        f.Tap(40, 95);
        Assert.Equal("mob0", f.Hits.LastOrDefault()?.Object);
        // Empty field between the rows hits nothing.
        f.Tap(580, 390);
        Assert.Single(f.Hits);

        // Moved, it is hit where it now stands; removed, not at all.
        Assert.True(f.Layer.SetPosition("mob0", 300, 380));
        Assert.True(f.Layer.SetFacing("mob0", 90));
        Assert.True(f.Layer.Remove("mob1"));
        f.Tap(300, 355);
        Assert.Equal("mob0", f.Hits.LastOrDefault()?.Object);
        Assert.Equal(29, f.Layer.ObjectNames.Count);
    }

    [Fact]
    public void A_still_layer_asks_for_no_frames()
    {
        using var f = new Field();
        f.Layer.Add("a", "dragon", 100, 200, 80);
        f.Layer.SetPlaying(false);
        f.Frames(0.2f);
        Assert.False(f.Layer.IsAnimating);
        Assert.False(f.Host.NeedsFrame);
    }

    [Fact]
    public void Every_object_is_its_own_player()
    {
        using var f = new Field();
        f.Layer.Add("a", "dragon", 100, 200, 80);
        f.Layer.Add("b", "dragon", 300, 200, 80);
        Assert.True(f.Layer.Play("b", Add3dAssets.Idle));
        // A new object loops its model's first animation (the dragon's first is its death).
        Assert.Equal(Add3dAssets.Death, f.Layer.CurrentAnimation("a"));
        Assert.Equal(Add3dAssets.Idle, f.Layer.CurrentAnimation("b"));
        Assert.True(f.Layer.SetMaterialColor("a", "Dragon_Main", Color.FromRgb(40, 120, 220)));
        Assert.False(f.Layer.SetMaterialColor("a", "Nothing", null));
        using var surface = SKSurface.Create(new SKImageInfo(4, 4));
        using var snapshot = surface.Snapshot();
        Assert.True(f.Layer.SetMaterialImage("a", "Dragon_Main", Texture3D.FromImage(snapshot)));
        Assert.False(f.Layer.SetMaterialImage("a", "Nothing", null));
        Assert.False(f.Layer.SetMaterialImage("nobody", "Dragon_Main", null));
    }
}
