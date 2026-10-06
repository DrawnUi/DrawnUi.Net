using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Frame timing of SkiaSprite, SkiaGif and SkiaLottie on the synthetic clock: SpeedRatio scales the play
/// time (0.5 = twice as long), a FrameSequence shows exactly its frames, DefaultFrame / CurrentFrame are
/// frame indexes, and seeking to the exact start time of a frame shows that frame.
/// Every sheet frame is filled with its own color, the test reads back which frame is on screen.
/// </summary>
[Collection("SharedFrameInterpolator")]
public class AnimatedFramesTimingTests
{
    private const int FramePx = 4;

    private static SKColor FrameColor(int i) => new((byte)(10 + i * 3), (byte)(250 - i * 3), 0x80);

    private static SKBitmap CreateSheet(int columns, int rows)
    {
        var bitmap = new SKBitmap(columns * FramePx, rows * FramePx);
        using var canvas = new SKCanvas(bitmap);
        for (var i = 0; i < columns * rows; i++)
        {
            using var paint = new SKPaint { Color = FrameColor(i) };
            canvas.DrawRect(SKRect.Create(i % columns * FramePx, i / columns * FramePx, FramePx, FramePx), paint);
        }
        return bitmap;
    }

    private static HeadlessCanvasHost Host(SkiaControl content)
    {
        var host = new HeadlessCanvasHost(40, 40, 1f, Colors.Black);
        host.Canvas.Content = content;
        host.RenderFrame(0);
        return host;
    }

    private static SkiaSprite Sprite(int columns, int rows, double fps) => new()
    {
        Columns = columns, Rows = rows, FramesPerSecond = fps, AutoPlay = false, WidthRequest = 40, HeightRequest = 40,
    };

    /// <summary>Sheet frame index drawn at the center of the canvas, -1 when no frame color is there.</summary>
    private static int ShownFrame(HeadlessCanvasHost host)
    {
        host.RenderFrame(0);
        using var image = host.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        var c = bitmap.GetPixel(20, 20);
        var i = (c.Red - 10) / 3;
        return c.Blue == 0x80 && c.Red >= 10 && (c.Red - 10) % 3 == 0 && c.Green == 250 - i * 3 ? i : -1;
    }

    /// <summary>Plays once and returns the synthetic time from the first animator tick to Finished.</summary>
    private static double PlayOnceMs(HeadlessCanvasHost host, AnimatedFramesRenderer control, double stepMs = 10)
    {
        var finished = false;
        control.Finished += (_, _) => finished = true;
        control.Start();
        Assert.True(control.IsPlaying);
        host.RenderFrame(stepMs); // first tick, the animator clock starts here
        var start = host.FrameTimeNanos;
        for (var i = 0; i < 2000 && !finished; i++)
            host.RenderFrame(stepMs);
        Assert.True(finished);
        return (host.FrameTimeNanos - start) / 1_000_000.0;
    }

    private sealed class TestGif : GifAnimation
    {
        public TestGif(params int[] durations)
        {
            Frames = durations.Select((_, i) =>
            {
                var b = new SKBitmap(FramePx, FramePx);
                b.Erase(FrameColor(i));
                return b;
            }).ToArray();
            TotalFrames = durations.Length;
            DurationMs = durations.Sum();
            var end = 0;
            FramesPositionsMs = durations.Select(d => end += d).ToArray();
            SeekFrame(0);
        }

        public SKBitmap FrameAt(int i) => Frames[i];

        public int StartMs(int i) => i == 0 ? 0 : FramesPositionsMs[i - 1];
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Path.Exists(Path.Combine(dir.FullName, ".git"))) // a worktree has a .git file
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void SpeedRatio_Sprite_DividesDuration(double ratio)
    {
        var sprite = Sprite(4, 2, 10); // 8 frames at 10 fps = 800 ms
        sprite.SpeedRatio = ratio;
        using var host = Host(sprite);
        sprite.SetSpriteSheet(CreateSheet(4, 2), false);

        Assert.InRange(PlayOnceMs(host, sprite), 800 / ratio, 800 / ratio + 10);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void SpeedRatio_Gif_DividesDuration(double ratio)
    {
        var gif = new SkiaGif { AutoPlay = false, WidthRequest = 40, HeightRequest = 40, SpeedRatio = ratio };
        using var host = Host(gif);
        gif.SetAnimation(new TestGif(100, 100, 100, 100), false);

        Assert.InRange(PlayOnceMs(host, gif), 400 / ratio, 400 / ratio + 10);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void SpeedRatio_Lottie_DividesDuration(double ratio)
    {
        var lottie = new SkiaLottie { AutoPlay = false, WidthRequest = 40, HeightRequest = 40, SpeedRatio = ratio };
        using var host = Host(lottie);
        var json = File.ReadAllText(RepoFile(@"Tests\PreviewTests\Resources\Raw\Lottie\ok.json"));
        lottie.SetAnimation(lottie.LoadAnimationFromJson(json), false);
        var ms = lottie.Animation.Duration.TotalMilliseconds; // 164 frames at 80 fps

        Assert.InRange(PlayOnceMs(host, lottie), ms / ratio, ms / ratio + 10);
    }

    [Fact]
    public void SpeedRatio_ChangedAfterLoad_Applies()
    {
        var sprite = Sprite(4, 2, 10);
        using var host = Host(sprite);
        sprite.SetSpriteSheet(CreateSheet(4, 2), false);
        sprite.SpeedRatio = 0.25;

        Assert.InRange(PlayOnceMs(host, sprite), 3200, 3210);
    }

    [Fact]
    public void FrameSequence_Seek_ShowsListedFrames()
    {
        int[] sequence = { 3, 4, 5, 4, 3, 7, 0 };
        var sprite = Sprite(4, 2, 10);
        sprite.FrameSequence = sequence;
        using var host = Host(sprite);
        sprite.SetSpriteSheet(CreateSheet(4, 2), false);

        Assert.Equal(sequence.Length, sprite.TotalFrames);
        for (var i = 0; i < sequence.Length; i++)
        {
            sprite.Seek(i * sprite.FrameDurationMs + sprite.FrameDurationMs / 2);
            Assert.Equal(i, sprite.CurrentFrame);
            Assert.Equal(sequence[i], ShownFrame(host));
        }
    }

    /// <summary>Plays once, returns the frames shown while playing (repeats collapsed).</summary>
    private static List<int> PlayAndCollect(HeadlessCanvasHost host, SkiaSprite sprite)
    {
        var shown = new List<int>();
        sprite.Start();
        for (var i = 0; i < 400; i++)
        {
            host.RenderFrame(25);
            if (!sprite.IsPlaying)
                break;
            var frame = ShownFrame(host);
            if (shown.Count == 0 || shown[^1] != frame)
                shown.Add(frame);
        }
        return shown;
    }

    [Fact]
    public void FrameSequence_Playback_ShowsListedFramesInOrder()
    {
        int[] sequence = { 3, 4, 5, 4, 3, 7, 0 };
        var sprite = Sprite(4, 2, 10);
        sprite.FrameSequence = sequence;
        using var host = Host(sprite);
        sprite.SetSpriteSheet(CreateSheet(4, 2), false);

        Assert.Equal(sequence, PlayAndCollect(host, sprite));
    }

    [Fact]
    public void FrameSequence_SetAfterLoad_PlaysListedFramesInOrder()
    {
        int[] sequence = { 3, 4, 5, 4, 3, 7, 0 };
        var sprite = Sprite(4, 2, 10);
        using var host = Host(sprite);
        sprite.SetSpriteSheet(CreateSheet(4, 2), false);
        sprite.FrameSequence = sequence;

        Assert.Equal(sequence, PlayAndCollect(host, sprite));
        Assert.InRange(PlayOnceMs(host, sprite), 700, 710);
    }

    [Fact]
    public void FramesPerSecond_SetAfterLoad_Applies()
    {
        var sprite = Sprite(4, 2, 10);
        using var host = Host(sprite);
        sprite.SetSpriteSheet(CreateSheet(4, 2), false);
        sprite.FramesPerSecond = 20; // 8 frames = 400 ms

        Assert.InRange(PlayOnceMs(host, sprite), 400, 410);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    [InlineData(7, 7)]
    [InlineData(-1, 7)]
    public void DefaultFrame_Sprite_ShowsThatFrame(int defaultFrame, int expected)
    {
        // set after the sheet is loaded
        var sprite = Sprite(4, 2, 10);
        using var host = Host(sprite);
        sprite.SetSpriteSheet(CreateSheet(4, 2), false);
        sprite.DefaultFrame = defaultFrame;
        Assert.Equal(expected, ShownFrame(host));

        // set before the sheet is loaded
        var early = Sprite(4, 2, 10);
        early.DefaultFrame = defaultFrame;
        using var host2 = Host(early);
        early.SetSpriteSheet(CreateSheet(4, 2), false);
        Assert.Equal(expected, ShownFrame(host2));
    }

    [Fact]
    public void DefaultFrame_Sprite_WithSequence_IsSequencePosition()
    {
        var sprite = Sprite(4, 2, 10);
        sprite.FrameSequence = new[] { 6, 1, 2 };
        using var host = Host(sprite);
        sprite.SetSpriteSheet(CreateSheet(4, 2), false);

        sprite.DefaultFrame = 1;
        Assert.Equal(1, ShownFrame(host));
        sprite.DefaultFrame = -1;
        Assert.Equal(2, ShownFrame(host));
        sprite.DefaultFrame = 0;
        Assert.Equal(6, ShownFrame(host));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(-1, 3)]
    public void DefaultFrame_Gif_ShowsThatFrame(int defaultFrame, int expected)
    {
        var gif = new SkiaGif { AutoPlay = false, WidthRequest = 40, HeightRequest = 40, DefaultFrame = defaultFrame };
        using var host = Host(gif);
        var animation = new TestGif(100, 50, 70, 100);
        gif.SetAnimation(animation, false);
        Assert.Same(animation.FrameAt(expected), animation.Frame);

        gif.DefaultFrame = 1;
        Assert.Same(animation.FrameAt(1), animation.Frame);
        gif.DefaultFrame = defaultFrame;
        Assert.Same(animation.FrameAt(expected), animation.Frame);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(12)]
    [InlineData(15)]
    [InlineData(23.976)]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(29.97)]
    [InlineData(30)]
    [InlineData(60)]
    public void Seek_ToFrameStart_Sprite_ShowsThatFrame(double fps)
    {
        var sprite = Sprite(8, 8, fps);
        using var host = Host(sprite);
        sprite.SetSpriteSheet(CreateSheet(8, 8), false);

        for (var k = 63; k >= 0; k--) // backwards: every seek changes the frame
        {
            sprite.Seek(k * sprite.FrameDurationMs);
            Assert.Equal(k, sprite.CurrentFrame);
            Assert.Equal(k, ShownFrame(host));
        }
    }

    [Fact]
    public void Seek_ToFrameStart_Gif_ShowsThatFrame()
    {
        var gif = new SkiaGif { AutoPlay = false, WidthRequest = 40, HeightRequest = 40 };
        using var host = Host(gif);
        var animation = new TestGif(100, 50, 70, 30, 100);
        gif.SetAnimation(animation, false);

        for (var k = 0; k < 5; k++)
        {
            gif.Seek(animation.StartMs(k));
            Assert.Same(animation.FrameAt(k), animation.Frame);
        }
    }

    [Fact]
    public void CurrentFrame_Set_ShowsThatFrame()
    {
        var sprite = Sprite(4, 2, 10);
        using var host = Host(sprite);
        sprite.SetSpriteSheet(CreateSheet(4, 2), false);

        foreach (var frame in new[] { 6, 2, 7, 0 })
        {
            sprite.CurrentFrame = frame;
            Assert.Equal(frame, ShownFrame(host));
        }

        sprite.FrameSequence = new[] { 5, 3, 1 };
        sprite.CurrentFrame = 1;
        Assert.Equal(3, ShownFrame(host));
        sprite.CurrentFrame = 2;
        Assert.Equal(1, ShownFrame(host));
    }
}

/// <summary>
/// Animators with UseInterpolator (frame players, SkiaScroll fling) all read the static FrameTimeInterpolator.Instance.
/// Frame players ticking on another host's synthetic clock at the same time skew the frame delta a fling integrates
/// (GestureRobotTests.Pan_SameFling_WhenTheMachineStalls failed about every second run), so these run alone.
/// </summary>
[CollectionDefinition("SharedFrameInterpolator", DisableParallelization = true)]
public class SharedFrameInterpolatorCollection
{
}
