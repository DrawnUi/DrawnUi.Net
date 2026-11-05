using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;

public partial class KeyboardManager
{
    // Key handlers owned by controls, held weakly by their owner: a page removed from the canvas without Dispose is
    // collected, and its handlers are called only while their control is on a canvas.
    private static readonly ConditionalWeakTable<SkiaControl, List<Action<InputKey>>> ControlKeyDown = new();
    private static readonly ConditionalWeakTable<SkiaControl, List<Action<InputKey>>> ControlKeyUp = new();
    private static int _controlListenersHooked;

    /// <summary>
    /// Calls <paramref name="keyDown"/> / <paramref name="keyUp"/> for every key while <paramref name="owner"/> is on a
    /// canvas. The owner is held weakly: a control removed without Dispose is not kept alive by its handlers, and it
    /// gets no keys while it is off the canvas (they come back when it is added again). <see cref="Unsubscribe"/>
    /// removes them; Dispose of the owner should call it.
    /// </summary>
    public static void Subscribe(SkiaControl owner, Action<InputKey> keyDown, Action<InputKey> keyUp = null)
    {
        if (owner == null)
            return;

        if (Interlocked.Exchange(ref _controlListenersHooked, 1) == 0)
        {
            KeyDown += (_, key) => RaiseForControls(ControlKeyDown, key);
            KeyUp += (_, key) => RaiseForControls(ControlKeyUp, key);
        }

        if (keyDown != null)
            lock (ControlKeyDown)
                ControlKeyDown.GetValue(owner, _ => new()).Add(keyDown);
        if (keyUp != null)
            lock (ControlKeyUp)
                ControlKeyUp.GetValue(owner, _ => new()).Add(keyUp);
    }

    /// <summary>Removes every handler <paramref name="owner"/> registered with <see cref="Subscribe"/>.</summary>
    public static void Unsubscribe(SkiaControl owner)
    {
        if (owner == null)
            return;

        lock (ControlKeyDown)
            ControlKeyDown.Remove(owner);
        lock (ControlKeyUp)
            ControlKeyUp.Remove(owner);
    }

    private static void RaiseForControls(ConditionalWeakTable<SkiaControl, List<Action<InputKey>>> table, InputKey key)
    {
        List<(SkiaControl Owner, Action<InputKey>[] Handlers)> listeners;
        lock (table)
            listeners = table.Select(x => (x.Key, x.Value.ToArray())).ToList();

        foreach (var (owner, handlers) in listeners)
        {
            if (owner.IsDisposed || !IsOnCanvas(owner))
                continue;
            foreach (var handler in handlers)
                handler(key);
        }
    }

    // the parent chain reaches a canvas: a control inside a removed page does not
    private static bool IsOnCanvas(SkiaControl control)
    {
        IDrawnBase current = control;
        while (current is SkiaControl skia)
        {
            if (skia.Parent == null)
                return false;
            current = skia.Parent;
        }
        return current is DrawnUi.Views.DrawnView { IsDisposed: false };
    }
}
