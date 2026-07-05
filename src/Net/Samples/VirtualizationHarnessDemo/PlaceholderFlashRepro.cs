using System.Reflection;
using DrawnChatList;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// INVESTIGATION (user-reported): send a message with an image -> frames show the message, then the
/// loaded image, then a CACHE PLACEHOLDER flash, then the message again. Suspicion: a DIFFERENT cell
/// instance briefly renders that context. This repro sends an image message and traces, per frame, the
/// VIEW IDENTITY (Uid) serving the new message's index, its NeedMeasure flag and cache (RenderObject)
/// state — logging every transition so the exact mechanism is visible.
/// </summary>
public static class PlaceholderFlashRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= PLACEHOLDER FLASH ON SEND-IMAGE (investigation) =========");

        var page = new ChatPage();
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: ChatTheme.Bg);
        host.Canvas.Content = page.CreateCanvasContent();
        page.InitializeList();

        for (int i = 0; i < 400 && page.ChatStack.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(4); }
        for (int f = 0; f < 60; f++) { host.RenderFrame(16); Thread.Sleep(3); }

        // send an image message via the page's own mock (private -> reflection, test-only)
        var send = typeof(ChatPage).GetMethod("SendImage", BindingFlags.NonPublic | BindingFlags.Instance);
        if (send == null)
        {
            Console.WriteLine("=> FAIL (SendImage not found)");
            return;
        }

        send.Invoke(page, null);

        string last = null;
        Guid lastUid = Guid.Empty;
        int skeletonFrames = 0, uidChanges = 0, cacheDrops = 0;

        for (int f = 0; f < 240; f++)
        {
            host.RenderFrame(16);
            Thread.Sleep(3);

            // the sent message = newest = ItemsSource[0] in the inverted window
            var src = page.ChatStack.ItemsSource;
            if (src == null || src.Count == 0) continue;
            var msg = src[0] as ChatMessage;
            if (msg == null || msg.Type != ChatMessageType.Image) continue;

            var view = page.ChatStack.ChildrenFactory.PeekRealizedViewForIndex(0);
            string state;
            if (view == null)
            {
                state = "view=NULL";
            }
            else
            {
                bool ctxMatch = ReferenceEquals(view.BindingContext, msg);
                bool hasCache = view.RenderObject != null;
                state = $"uid={view.Uid.ToString()[..8]} ctxMatch={ctxMatch} needMeasure={view.NeedMeasure} cache={(hasCache ? "OK" : "NULL")}";

                if (view.NeedMeasure)
                {
                    skeletonFrames++;
                    // capture the exact invalidation frame: with the stale-serve fix this must show the
                    // message CONTENT (cache blit), not the skeleton
                    host.SavePng(System.IO.Path.Combine(
                        @"C:\Users\taubl\AppData\Local\Temp\claude\C--Users-taubl\2c597eec-477f-4fa9-a277-76a6f3f92aa3\scratchpad",
                        "flash-frame.png"));
                }
                if (lastUid != Guid.Empty && view.Uid != lastUid) uidChanges++;
                if (lastUid == view.Uid && last != null && last.Contains("cache=OK") && !hasCache) cacheDrops++;
                lastUid = view.Uid;
            }

            if (state != last)
            {
                Console.WriteLine($"  f{f,3}: {state}");
                last = state;
            }
        }

        Console.WriteLine($"  SUMMARY: uidChanges={uidChanges} needMeasureFrames={skeletonFrames} cacheDrops(sameView)={cacheDrops}");
        Console.WriteLine("====================================================================");
    }
}
