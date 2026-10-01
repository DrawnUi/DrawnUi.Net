using DrawnUi.Controls;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaLottie and SkiaSprite raise Success once the source is loaded and applied, Error when it cannot be loaded.
/// </summary>
public class PlayFramesLoadEventsTests
{
    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Path.Exists(Path.Combine(dir.FullName, ".git"))) // a worktree has a .git file
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative);
    }

    private static async Task<string> WaitFor<T>(T control, Action<T, Action<string>> subscribe)
    {
        var done = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        subscribe(control, s => done.TrySetResult(s));
        var finished = await Task.WhenAny(done.Task, Task.Delay(5000));
        return finished == done.Task ? done.Task.Result : "timeout";
    }

    [Fact]
    public async Task Lottie_Success_WithFrames()
    {
        var path = RepoFile(@"Tests\PreviewTests\Resources\Raw\Lottie\ok.json");
        var lottie = new SkiaLottie { AutoPlay = false };
        var result = WaitFor(lottie, (c, set) =>
        {
            c.Success += (s, source) => set($"ok {source} {((SkiaLottie)s).Animation != null}");
            c.Error += (_, e) => set("error " + e.Message);
        });

        lottie.Source = path;

        Assert.Equal($"ok {path} True", await result);
    }

    [Fact]
    public async Task Lottie_MissingFile_Error()
    {
        var lottie = new SkiaLottie { AutoPlay = false };
        var result = WaitFor(lottie, (c, set) =>
        {
            c.Success += (_, _) => set("ok");
            c.Error += (_, _) => set("error");
        });

        lottie.Source = "no/such/animation.json";

        Assert.Equal("error", await result);
    }

    [Fact]
    public async Task Sprite_Success_WithFrames()
    {
        var path = RepoFile(@"src\Maui\Samples\HelloMaui\Resources\Raw\anims\BlueWarrior\Warrior_Idle.png");
        var sprite = new SkiaSprite { Columns = 8, Rows = 1, AutoPlay = false };
        var result = WaitFor(sprite, (c, set) =>
        {
            c.Success += (s, _) => set($"ok {((SkiaSprite)s).TotalFrames}");
            c.Error += (_, e) => set("error " + e.Message);
        });

        sprite.Source = path;

        Assert.Equal("ok 8", await result);
    }

    [Fact]
    public async Task Sprite_MissingFile_Error()
    {
        var sprite = new SkiaSprite { Columns = 8, Rows = 1, AutoPlay = false };
        var result = WaitFor(sprite, (c, set) =>
        {
            c.Success += (_, _) => set("ok");
            c.Error += (_, _) => set("error");
        });

        sprite.Source = "no/such/sheet.png";

        Assert.Equal("error", await result);
    }
}
