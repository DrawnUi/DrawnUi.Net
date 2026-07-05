using DrawnChatList;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Repro for "delete message keeps empty space": real ChatPage, delete a resident visible message
/// via the exact app path (DeleteMessage -> service + windowed source removal), then verify the
/// remaining cells TILE the viewport with no persistent hole bigger than the layout spacing.
/// </summary>
public static class DeleteGapRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("============== DELETE MESSAGE GAP ==============");

        var page = new ChatPage();
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: ChatTheme.Bg);
        host.Canvas.Content = page.CreateCanvasContent();
        page.InitializeList();

        for (int i = 0; i < 400 && page.ChatStack.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(4); }
        host.AdvanceFrames(30, 16);

        // Mirror the reported flow: scroll a couple of screens INTO history first, then delete a
        // first-of-day message there (mid-window), then keep scrolling and come back through the
        // deletion site — the corruption showed exactly at the deleted slot.
        var robotPre = new GestureRobot(host);
        for (int k = 0; k < 3; k++)
        {
            robotPre.Pan(215, 250, 215, 820, durationMs: 100, steps: 8);
            for (int f = 0; f < 40; f++) { host.RenderFrame(16); Thread.Sleep(3); }
        }
        for (int f = 0; f < 60; f++) { host.RenderFrame(16); Thread.Sleep(3); }

        // pick a message that's currently on screen — PREFER a first-of-day one (has the date chip;
        // reported: deleting it leaves overlapping cells), else any mid-viewport cell. Retry while
        // the page populates (cross-repro dispatcher timing in the harness).
        ChatMessage victim = null;
        for (int attempt = 0; attempt < 200 && victim == null; attempt++)
        {
            var tree = page.ChatStack.RenderTree;
            if (tree != null)
            {
                foreach (var t in tree)
                {
                    if (t.FreezeBindingContext is ChatMessage m && t.HitRect.Top > 100 && t.HitRect.Bottom < 800)
                    {
                        if (m.IsFirstDay)
                        {
                            victim = m;
                            break;
                        }

                        victim ??= m;
                    }
                }
            }

            if (victim == null)
            {
                host.RenderFrame(16);
                Thread.Sleep(5);
            }
        }

        if (victim == null)
        {
            Console.WriteLine("=> FAIL (no visible message found to delete)");
            return;
        }

        int residentBefore = page.ProbeResident;
        Console.WriteLine($"deleting msg Index={victim.Index} firstOfDay={victim.IsFirstDay} '{victim.Text?[..Math.Min(20, victim.Text.Length)]}' resident={residentBefore}");

        page.DeleteMessage(victim);

        // settle: let the remove apply + any restack land
        for (int f = 0; f < 120; f++) { host.RenderFrame(16); Thread.Sleep(3); }

        int residentAfter = page.ProbeResident;

        // scan the render tree for holes between consecutive painted cells inside the viewport
        float maxGap = 0;
        float prevBottom = float.NaN;
        var cells = new List<(float top, float bottom, int idx)>();
        var treeAfter = page.ChatStack.RenderTree;
        if (treeAfter != null)
            foreach (var t in treeAfter)
                if (t.FreezeBindingContext is ChatMessage m)
                    cells.Add((t.HitRect.Top, t.HitRect.Bottom, m.Index));
        float maxOverlap = 0;
        cells.Sort((a, b) => a.top.CompareTo(b.top));
        foreach (var c in cells)
        {
            if (!float.IsNaN(prevBottom))
            {
                if (c.top > prevBottom)
                    maxGap = Math.Max(maxGap, c.top - prevBottom);
                else if (prevBottom - c.top > 1f)
                    maxOverlap = Math.Max(maxOverlap, prevBottom - c.top);
            }
            prevBottom = Math.Max(prevBottom is float.NaN ? c.bottom : prevBottom, c.bottom);
        }

        // coverage: how much of the viewport [0..920] is actually painted by cells
        float covered = 0;
        float cursor = 0;
        foreach (var c in cells)
        {
            float top = Math.Max(cursor, Math.Max(0, c.top));
            float bottom = Math.Min(920, c.bottom);
            if (bottom > top) covered += bottom - top;
            cursor = Math.Max(cursor, bottom);
        }

        bool deleted = residentAfter == residentBefore - 1;
        bool noHole = maxGap <= 30f; // spacing 4pt + tolerance; a dead message slot is ~60-150px
        bool noOverlap = maxOverlap <= 1f;
        bool viewportFull = covered >= 920 * 0.85f; // chat at rest must fill the screen
        Console.WriteLine($"resident {residentBefore}->{residentAfter} deleted={deleted} maxGap={maxGap:0}px maxOverlap={maxOverlap:0}px cells={cells.Count} covered={covered:0}/920px");
        Console.WriteLine(deleted && noHole && noOverlap && viewportFull
            ? "=> PASS (item removed, no hole, no overlap, viewport full)"
            : "=> FAIL (gap/overlap/empty space — bug reproduced)");

        // SCROLL-UP INTEGRITY after the delete: window bookkeeping desync used to make every later
        // LoadOlder page a shifted range — cells bound wrong messages ("..270, 288, GAP, 273..").
        // Pan into history through several LoadOlder cycles, then verify the visible sequence is
        // consecutive ascending msg.Index (one missing value = the deleted message is fine).
        var robot = new GestureRobot(host);
        for (int k = 0; k < 5; k++)
        {
            robot.Pan(215, 250, 215, 820, durationMs: 100, steps: 8); // inverted: drag down = into history
            for (int f = 0; f < 45; f++) { host.RenderFrame(16); Thread.Sleep(3); }
        }
        // ...and come BACK through the deletion site (drag up = toward newest)
        for (int k = 0; k < 5; k++)
        {
            robot.Pan(215, 820, 215, 250, durationMs: 100, steps: 8);
            for (int f = 0; f < 45; f++) { host.RenderFrame(16); Thread.Sleep(3); }
        }
        for (int f = 0; f < 90; f++) { host.RenderFrame(16); Thread.Sleep(3); } // settle loads

        var seq = new List<(float top, int idx)>();
        var treeUp = page.ChatStack.RenderTree;
        if (treeUp != null)
            foreach (var t in treeUp)
                if (t.FreezeBindingContext is ChatMessage m)
                    seq.Add((t.HitRect.Top, m.Index));
        seq.Sort((a, b) => a.top.CompareTo(b.top));

        // HitRect lives in pre-rotation space: the healthy inverted-chat sequence reads DESCENDING
        // by one in this ordering (verified against a clean run).
        bool ordered = true;
        int jumps = 0;
        for (int i = 1; i < seq.Count; i++)
        {
            var diff = seq[i].idx - seq[i - 1].idx;
            if (diff == -1) continue;
            if (diff == -2) { jumps++; continue; } // the deleted message's index missing once is OK
            ordered = false;
            Console.WriteLine($"   BAD SEQUENCE: ...{seq[i - 1].idx} -> {seq[i].idx} (diff {diff}) at y={seq[i].top:0}");
        }

        bool seqOk = ordered && jumps <= 1 && seq.Count >= 5;
        Console.WriteLine($"scroll-up integrity: cells={seq.Count} range=[{(seq.Count > 0 ? seq[0].idx : -1)}..{(seq.Count > 0 ? seq[^1].idx : -1)}] deletedSkips={jumps}");
        Console.WriteLine(seqOk
            ? "=> PASS (post-delete scroll-up sequence intact)"
            : "=> FAIL (sequence corrupted after delete + LoadOlder — bug reproduced)");
        Console.WriteLine("================================================");
    }
}
