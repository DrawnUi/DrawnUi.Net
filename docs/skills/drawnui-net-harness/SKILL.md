---
name: drawnui-net-harness
description: Use for headless, pure-.NET (no device/GPU) testing and runtime debugging of DrawnUi controls — render frames, simulate gestures, settle background measurement, detect blank/empty renders, reproduce SkiaScroll/SkiaLayout virtualization + windowed-list bugs. Trigger on "test in .net harness", "headless", "repro without device", DrawnUi.Testing, HeadlessCanvasHost, GestureRobot, VirtualizationProbe.
---

# DrawnUi .NET Headless Harness

Pure managed DrawnUi runs WITHOUT a window, device, or GPU. Render to an offscreen SkiaSharp
surface on a deterministic synthetic clock, drive real gestures, inspect structure + pixels. Ideal
for unit tests AND for reproducing/fixing layout/scroll/virtualization bugs the way the device does —
fast, deterministic, scriptable. **Prefer this over OpenTk for measurement/scroll/windowing bugs.**

## Where it lives (repo `DrawnUi`)

- Infra: `src/Net/DrawnUi/Testing/` — `HeadlessCanvasHost.cs`, `GestureRobot.cs`, `VirtualizationProbe.cs`, `VirtualizationScene.cs`.
- Runnable harness sample: `src/Net/Samples/VirtualizationHarnessDemo/` — `Program.cs` (top-level entry), `ChatLikeScene.cs` (inverted+windowed chat repro), `BlankJumpRepro.cs`.
- Namespaces: `DrawnUi.Testing`, `DrawnUi.Draw`. Project targets `net9.0`, refs `DrawnUi.Net` by project → editing engine `src/Shared/**` recompiles into the harness directly.

## Core API

```csharp
var host = new HeadlessCanvasHost(width:430, height:720, scale:1f, background:Colors.Black);
host.Canvas.Content = root;            // any SkiaControl tree (SkiaScroll, SkiaLayout, ...)
host.RenderFrame(16);                  // advance synthetic clock 16ms + render one frame
host.AdvanceFrames(12, 16);            // N frames
host.SavePng(path);                    // visual diff
double fill = host.NonBackgroundFraction(Colors.Black); // 0..1 ; ~0 on a populated scene == BLANK
```

- Clock is synthetic + monotonic: gestures (flushed in `ExecuteBeforeDraw`) and fling/animation (ticked off `FrameTimeNanos`) advance only when you call `RenderFrame`. Nothing happens "between" frames.
- `Super.Init()` runs once inside the host ctor.

### Gestures — really wired
```csharp
var robot = new GestureRobot(host);
robot.Pan(fromX, fromY, toX, toY, durationMs:90, steps:8); // finger up (520->180) scrolls content DOWN
robot.Tap(x, y); robot.Fling(...);                          // see GestureRobot.cs for full set
```

### Virtualization probe (any templated SkiaScroll+SkiaLayout)
```csharp
var probe = new VirtualizationProbe(host, scroll, list);
probe.Frontier;     // list.LastMeasuredIndex (measurement frontier)
probe.FirstVisible / probe.LastVisible / probe.OffsetY / probe.ContentHeight / probe.ItemsCount;
probe.SettleBackground(maxFrames:120);          // render until the frontier stops advancing
probe.Drive(robot, stop:()=>..., down:true);    // gesture-drive until a condition
```

## Building a faithful scene (CRITICAL)

A harness only proves something if it mimics the target's DEFINING conditions. For ChatPage-class bugs
that means: **inverted** (`SkiaScroll.Rotation=180, ReverseGestures=true, TrackIndexPosition=Start`),
**windowed ItemsSource** (`ObservableRangeCollection`, slice newest-first `Items[i]==All[End-1-i]`,
cap resident count), **bidirectional LoadMore** (`LoadMoreCommand`=older/append-tail, `LoadMoreTopCommand`=newer/head-insert),
**variable heights**, **MeasureVisible** strategy, **jump buttons** (ReplaceRange rebase + ordered
`scroll.ScrollToIndex(local, animate, align, ordered:true)`). `ChatLikeScene.cs` is the reference; copy it.

Windowing is now a LIBRARY primitive — no subclass needed: `DrawnUi.Draw.WindowedSource<T>` (sliding window
over a big backing list) + `SkiaScrollWindowHost` (built-in `IWindowHost` adapter) over a plain `SkiaLayout`.
Base `SkiaLayout` provides `SuppressLoadMore`, ordered-scroll LoadMore gating (inside `ShouldTriggerLoadMore`),
and the `MeasurementApplied` event (trim-after-measure hook). `ChatLikeScene` drives these directly.
Do NOT validate on a simplified static scene (uniform heights, non-inverted, non-windowed) — it proves nothing.

## Detecting a blank/empty render headlessly

`NonBackgroundFraction(bg)` ~ 0 on a scene that should be full = blank. Pair with structure dump from a
`SkiaLayout` subclass (`StackStructure` is `protected`): print each cell `ControlIndex / Destination.Top / Bottom / Measured.Height`.
A non-monotonic Top sequence or `h0` cells = corrupt structure (overlap). Content height is computed O(1)
as `last.Destination.Bottom - first.Destination.Top` (`UpdateProgressiveContentSize`), so a wrong LAST-cell
position collapses content → offset exceeds it → blank.

## Run it

```
dotnet run --project src/Net/Samples/VirtualizationHarnessDemo/VirtualizationHarnessDemo.csproj -c Debug
```
For engine `Debug.WriteLine` on stdout, add `Trace.Listeners.Add(new ConsoleTraceListener())` in the entry
(remove after). Gate temp engine probes behind a `public static bool DebugX` flag and REMOVE when done —
keep instrumentation out of committed engine code.

## Gotchas learned

- Pixel-asserting an `ImageDoubleBuffered` control: the offscreen bake runs on a REAL worker thread while the harness clock is synthetic — N `AdvanceFrames` alone can snapshot before the bake lands → canvas all-background, flaky. Settle first: `for(...40){ if (host.NonBackgroundFraction(bg) > 0.001) break; Thread.Sleep(20); host.AdvanceFrames(3); }`. Property/structure asserts don't need this. (Verified 2026-07, SkiaSlider style probes.)
- Background-measured cells must set BOTH `cell.Area` and `cell.Destination`. `ComputeBottomOfRow` (which positions the NEXT batch) reads `cell.Area.Top` — leaving Area default (Top=0) makes every cell report bottom=height, so the next batch stacks at the top → overlap → collapsed content → blank. (Root cause of the 2026 item-measurement-memo blank-jump bug.)
- `_measuredItems` is keyed by LOCAL index → a full-collection `ReplaceRange` (window jump) clears it (`ResetMeasurementForReplace`). An item-keyed measurement memo (`MeasurementCacheCapacity`, default 1000) survives swaps; it must seed `Area` too.
- MEMO/fast paths complete background measurement in fewer frames → can expose latent multi-batch ordering races that the slow path hid. Always A/B the harness with the optimization ON vs OFF (e.g. `MeasurementCacheCapacity: 0`) to attribute a regression.
- Transient mid-settle collapse (incomplete frontier) is benign and self-heals; a STICKY collapse is the bug. Settle fully before asserting.
- `VirtualizationProbe.SettleBackground` returns on frontier-STABLE, which a *stalled-but-incomplete* frontier also satisfies → false-blank snapshots / flaky runs. For jump assertions settle until `Frontier >= ItemsCount-1 && LastVisible >= 0` (background measurement re-triggers each rendered frame). Killed the blank-jump flakiness.
- Background measurement runs on real threadpool threads (`Task.Run`) with `Thread.Sleep` pacing in settle → naive single-run assertions are flaky. Run the scenario N times and assert 0 failures.
- This harness is undocumented in other DrawnUi skills; reach for it first for measurement/scroll/windowing work.
