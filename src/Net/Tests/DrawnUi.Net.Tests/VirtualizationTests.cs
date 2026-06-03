using DrawnUi.Testing;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

// Headless reproduction + invariants for the LoadMoreRepro planes-scroll virtualization.
public class VirtualizationTests
{
    private readonly ITestOutputHelper _out;
    public VirtualizationTests(ITestOutputHelper output) => _out = output;

    private static (VirtualizationScene scene, VirtualizationProbe probe, GestureRobot robot) NewScene(int count = 1000)
    {
        var scene = new VirtualizationScene(itemsCount: count);
        scene.Warmup();
        var probe = new VirtualizationProbe(scene.Host, scene.Scroll, scene.List);
        var robot = new GestureRobot(scene.Host);
        return (scene, probe, robot);
    }

    // -------------------------------------------------------------------- initial state

    [Fact]
    public void Initial_Layout_Only_Measures_A_Bounded_Window()
    {
        using var scene = new VirtualizationScene(itemsCount: 1000);
        scene.Warmup();

        // measured a small window near the top, never the whole 1000
        Assert.InRange(scene.List.LastMeasuredIndex, 10, 99);
        // rendered only the top region
        Assert.InRange(scene.MaxBoundItemIndex, 5, 99);
        // no binding desync
        Assert.Empty(scene.DrainMismatches());
    }

    [Fact]
    public void First_Plane_Is_Filled_For_Its_Full_Height_Not_Just_The_Viewport()
    {
        using var scene = new VirtualizationScene(itemsCount: 1000, width: 430, height: 640);
        scene.Warmup();

        // The current plane spans TWO viewports (_planeHeight = viewportHeight * 2). With the measured
        // item height, that's how many items must be measured/rendered to fill it. The visible viewport
        // alone is only half the plane.
        double measuredEndPts = scene.List.GetMeasuredContentEnd();   // top of last measured item, in points
        int measured = scene.List.LastMeasuredIndex + 1;
        double itemHeightPts = measuredEndPts / Math.Max(1, scene.List.LastMeasuredIndex);
        int expectedPerViewport = (int)(640 / Math.Max(1, itemHeightPts));
        int expectedPerPlane = expectedPerViewport * 2;

        _out.WriteLine($"measured={measured} itemH={itemHeightPts:0.0}pt perViewport={expectedPerViewport} " +
                       $"expectedPerPlane={expectedPerPlane} maxBound={scene.MaxBoundItemIndex}");

        // BUG (LoadMoreRepro): first red plane only fills to the visible window (~1 viewport),
        // bottom half empty. This asserts the plane is filled for its full 2-viewport height.
        Assert.True(scene.List.LastMeasuredIndex + 1 >= expectedPerPlane * 0.8,
            $"first plane under-filled: measured {measured} items but the 2-viewport plane needs ~{expectedPerPlane}");
    }

    [Fact]
    public void Idle_Rendering_Does_Not_Keep_Measuring()
    {
        var (scene, probe, robot) = NewScene(1000);
        using var _ = scene;

        // small scroll, then sit idle rendering many frames — measurement must NOT creep toward the
        // whole list while nothing is happening (guards against measure->commit->swap feedback loops).
        robot.Pan(215, 520, 215, 380, durationMs: 140, steps: 10);
        probe.SettleBackground();
        int afterScroll = probe.Frontier;

        for (int i = 0; i < 60; i++) { scene.Host.RenderFrame(16); System.Threading.Thread.Sleep(6); }
        int afterIdle = probe.Frontier;

        _out.WriteLine($"afterScroll={afterScroll} afterIdle={afterIdle}");
        Assert.True(afterIdle - afterScroll <= 16,
            $"idle rendering kept measuring: {afterScroll} -> {afterIdle}");
    }

    // KNOWN BUG (LoadMoreRepro green plane): forward plane is prepared before its 2-viewport region is
    // measured, so its far half renders empty. Every attempted fix that measures within the plane
    // lifecycle hits a feedback loop: measure -> SetMeasured grows size -> NeedMeasure -> re-measure ->
    // InvalidatePlanes -> re-prepare -> measure (idle runaway / pool thrash). Needs a planes<->measurement
    // lifecycle redesign. Repro kept (GetPlanesContentInfo); unskip to work on it.
    [Fact]
    public void Forward_Plane_Captures_Its_Full_Region()
    {
        var (scene, probe, robot) = NewScene(1000);
        using var _ = scene;

        // scroll ~one viewport so the planes move and the forward plane gets prepared
        robot.Pan(215, 520, 215, 180, durationMs: 120, steps: 8);
        // let the background forward-plane prep task run (modest frames, NOT a long measure settle)
        for (int i = 0; i < 24; i++) { scene.Host.RenderFrame(16); System.Threading.Thread.Sleep(10); }

        var planes = scene.Scroll.GetPlanesContentInfo();
        foreach (var p in planes)
            _out.WriteLine($"plane {p.Id} ready={p.IsReady} count={p.Count} range=[{p.MinIndex}..{p.MaxIndex}] offY={p.OffsetY:0}");

        // a plane spans two viewports (~32 items here); each ready plane must capture roughly that,
        // otherwise its far half renders empty (the LoadMoreRepro green-plane bug)
        foreach (var p in planes)
        {
            if (!p.IsReady || p.MaxIndex < 0) continue;
            Assert.True(p.MaxIndex - p.MinIndex >= 24,
                $"plane {p.Id} under-filled: range [{p.MinIndex}..{p.MaxIndex}] (count={p.Count})");
        }
    }

    // -------------------------------------------------------------------- reach index 300

    [Fact]
    public void Scrolls_To_Index_300_And_Everything_Is_Consistent()
    {
        var (scene, probe, robot) = NewScene(1000);
        using var _ = scene;

        scene.DrainMeasures(); // ignore warmup paint measures

        int rounds = probe.DriveUntilBound(robot, targetBoundIndex: 300, () => scene.MaxBoundItemIndex, maxRounds: 200);
        var (fg, bg) = scene.DrainMeasures();
        var mismatches = scene.DrainMismatches();

        _out.WriteLine($"rounds={rounds} frontier={probe.Frontier} maxBound={scene.MaxBoundItemIndex} " +
                       $"offY={probe.OffsetY:0} cells={scene.CellInstanceCount} fg={fg} bg={bg} mismatches={mismatches.Count}");

        // actually rendered a cell at/after index 300
        Assert.True(scene.MaxBoundItemIndex >= 300, $"only rendered up to index {scene.MaxBoundItemIndex}");
        // the measured frontier keeps up with what was rendered (it may lag slightly behind)
        Assert.True(probe.Frontier >= scene.MaxBoundItemIndex - 60,
            $"measured frontier {probe.Frontier} lagging rendered index {scene.MaxBoundItemIndex}");
        // sanity: never instantiated more cells than items
        Assert.True(scene.CellInstanceCount <= probe.ItemsCount, $"created {scene.CellInstanceCount} cells");
        // ALL measurement to get here happened in the background
        Assert.Equal(0, fg);
        Assert.True(bg > 0, "expected background measurement while scrolling to 300");
        // no binding/index desync at any point
        Assert.Empty(mismatches);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(300)]
    [InlineData(600)]
    public void Scrolls_To_Various_Depths_With_No_Foreground_Measurement(int target)
    {
        var (scene, probe, robot) = NewScene(1000);
        using var _ = scene;
        scene.DrainMeasures();

        probe.DriveUntilBound(robot, target, () => scene.MaxBoundItemIndex, maxRounds: 300);
        var (fg, _) = scene.DrainMeasures();

        _out.WriteLine($"target={target} maxBound={scene.MaxBoundItemIndex} frontier={probe.Frontier} fg={fg}");

        Assert.True(scene.MaxBoundItemIndex >= target, $"target {target}, rendered up to {scene.MaxBoundItemIndex}");
        Assert.Equal(0, fg);
        Assert.Empty(scene.DrainMismatches());
    }

    // -------------------------------------------------------------------- round trip

    [Fact]
    public void Scroll_To_300_Then_Back_To_Top_Stays_Consistent()
    {
        var (scene, probe, robot) = NewScene(1000);
        using var _ = scene;

        probe.DriveUntilBound(robot, 300, () => scene.MaxBoundItemIndex, maxRounds: 200);
        Assert.True(scene.MaxBoundItemIndex >= 300);

        probe.DriveToTop(robot, maxRounds: 200);

        _out.WriteLine($"backToTop offY={probe.OffsetY:0} cells={scene.CellInstanceCount}");

        Assert.True(probe.OffsetY >= -2f, $"did not return to top, offY={probe.OffsetY}");
        Assert.Empty(scene.DrainMismatches());
    }

    // -------------------------------------------------------------------- full traversal / fluidity

    [Fact]
    public void Scrolling_Measures_Through_The_Whole_List()
    {
        var (scene, probe, robot) = NewScene(1000);
        using var _ = scene;

        int reached = probe.DriveDownByGesture(robot, maxRounds: 200);
        _out.WriteLine($"reached frontier={reached}/{probe.ItemsCount} offY={probe.OffsetY:0}");

        Assert.True(reached >= probe.ItemsCount - 50,
            $"measurement stalled at index {reached} of {probe.ItemsCount}");
        Assert.Empty(scene.DrainMismatches());
    }

    [Fact]
    public void Scroll_Measurement_Happens_Only_In_Background()
    {
        var (scene, probe, robot) = NewScene(1000);
        using var _ = scene;
        scene.DrainMeasures(); // discard warmup paint measurements

        int fgTotal = 0, bgTotal = 0;
        for (int round = 0; round < 60 && probe.Frontier < probe.ItemsCount - 1; round++)
        {
            robot.Pan(215, 520, 215, 180, durationMs: 90, steps: 8);
            probe.SettleBackground();
            var (fg, bg) = scene.DrainMeasures();
            fgTotal += fg;
            bgTotal += bg;
        }

        _out.WriteLine($"scroll-phase MEASURES fg={fgTotal} bg={bgTotal} frontier={probe.Frontier}");

        Assert.True(bgTotal > 0, "expected background measurement while scrolling");
        Assert.Equal(0, fgTotal);
        Assert.Empty(scene.DrainMismatches());
    }

    [Fact]
    public void Frontier_Advances_Monotonically_As_It_Scrolls()
    {
        var (scene, probe, robot) = NewScene(1000);
        using var _ = scene;

        int prev = probe.Frontier;
        for (int round = 0; round < 60 && probe.Frontier < probe.ItemsCount - 1; round++)
        {
            robot.Pan(215, 520, 215, 180, durationMs: 90, steps: 8);
            probe.SettleBackground();
            Assert.True(probe.Frontier >= prev, $"frontier went backwards: {prev} -> {probe.Frontier}");
            prev = probe.Frontier;
        }

        Assert.True(probe.Frontier > 100, $"frontier barely moved: {probe.Frontier}");
    }

    [Fact]
    public void Rendered_Index_Climbs_As_It_Scrolls_Down()
    {
        var (scene, probe, robot) = NewScene(1000);
        using var _ = scene;

        int initial = scene.MaxBoundItemIndex;
        probe.DriveUntilBound(robot, 500, () => scene.MaxBoundItemIndex, maxRounds: 300);

        _out.WriteLine($"initialBound={initial} finalBound={scene.MaxBoundItemIndex}");

        Assert.True(scene.MaxBoundItemIndex >= 500, $"rendering only reached index {scene.MaxBoundItemIndex}");
        Assert.True(scene.MaxBoundItemIndex > initial + 100, "rendered index did not climb with scrolling");
    }

    // -------------------------------------------------------------------- small list

    [Fact]
    public void Short_List_Measures_All_Items()
    {
        using var scene = new VirtualizationScene(itemsCount: 8);
        scene.Warmup();

        Assert.Equal(8, scene.List.LastMeasuredIndex + 1);
        Assert.Empty(scene.DrainMismatches());
    }
}
