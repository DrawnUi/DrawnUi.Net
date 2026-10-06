using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace DrawnUi.Draw;

/// <summary>
/// Screen readers and keyboard navigation for pure WebAssembly, the Blazor Canvas overlay contract: every node of the
/// accessibility snapshot (<see cref="SkiaAccessibilityManager"/>) is an invisible ARIA element over the canvas, in
/// reading order (drawnui-web.js). The element in focus is the canvas keyboard focus and the screen reader's node;
/// Enter / Space activate it, the arrow keys go to it (a slider steps) and then to its group. Pointer input stays with
/// the canvas.
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class WebAccessibility
{
    private static Canvas? _canvas;
    private static System.Threading.Timer? _refresh;

    [JSImport("a11yAttach", "drawnui-web")]
    private static partial void JsAttach(string elementId,
        [JSMarshalAs<JSType.Function<JSType.Number>>] Action<int> onActivate,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.String, JSType.Boolean>>] Func<int, string, bool> onKey,
        [JSMarshalAs<JSType.Function<JSType.Number>>] Action<int> onFocus,
        [JSMarshalAs<JSType.Function<JSType.Number>>] Action<int> onBlur);

    [JSImport("a11yDetach", "drawnui-web")]
    private static partial void JsDetach();

    [JSImport("a11yUpdate", "drawnui-web")]
    private static partial void JsUpdate(
        [JSMarshalAs<JSType.Array<JSType.Number>>] int[] ints,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] nums,
        [JSMarshalAs<JSType.Array<JSType.String>>] string[] strs);

    [JSImport("a11yFocus", "drawnui-web")]
    private static partial void JsFocus(int id);

    [JSImport("a11yLive", "drawnui-web")]
    private static partial void JsLive(int id, string text);

    /// <summary>The overlay follows this canvas (a Hot Reload rebuild attaches the new one).</summary>
    public static void Attach(Canvas canvas, string elementId)
    {
        Detach();

        _canvas = canvas;
        var manager = canvas.AccessibilityManager;
        manager.Changed += Push;
        manager.KeyboardFocusRequested += OnKeyboardFocusRequested;
        manager.ReaderRefocusRequested += OnReaderRefocus;
        manager.LiveRegionUpdated += OnLiveRegionUpdated;
        manager.RebuildSkipped += OnRebuildSkipped;

        JsAttach(elementId, OnActivate, OnKey, OnFocus, OnBlur);
        Push();
    }

    private static void Detach()
    {
        var canvas = _canvas;
        if (canvas == null)
            return;

        _canvas = null;
        var manager = canvas.AccessibilityManager;
        manager.Changed -= Push;
        manager.KeyboardFocusRequested -= OnKeyboardFocusRequested;
        manager.ReaderRefocusRequested -= OnReaderRefocus;
        manager.LiveRegionUpdated -= OnLiveRegionUpdated;
        manager.RebuildSkipped -= OnRebuildSkipped;
        _refresh?.Dispose();
        _refresh = null;
        JsDetach();
    }

    // the snapshot to the overlay: (id, flags), (rect, value), (role, label, hint, live, value text, pressed attribute) per node
    private static void Push()
    {
        var canvas = _canvas;
        if (canvas == null)
            return;

        var manager = canvas.AccessibilityManager;
        var snapshot = manager.Snapshot;
        var ints = new int[snapshot.Length * 2];
        var nums = new double[snapshot.Length * 7];
        var strs = new string[snapshot.Length * 6];
        for (var i = 0; i < snapshot.Length; i++)
        {
            var node = snapshot[i];
            var flags = 0;
            if (node.CanInteract)
            {
                flags |= 1;
                if (manager.IsTabStop(node.Source))
                    flags |= 2;
            }
            else if (DrawnUi.Models.Aria.IsInteractiveRole(node.Role))
            {
                flags |= 64; // a control role that takes no input reads as unavailable (drawnui-cross 6c rule 2)
            }

            if (node.IsPressed is { } pressed)
                flags |= pressed ? 12 : 4;

            var o = i * 7;
            nums[o] = node.Rect.Left;
            nums[o + 1] = node.Rect.Top;
            nums[o + 2] = node.Rect.Width;
            nums[o + 3] = node.Rect.Height;
            if (node.Value is { } value)
            {
                flags |= value.Vertical ? 48 : 16;
                nums[o + 4] = value.Now;
                nums[o + 5] = value.Min;
                nums[o + 6] = value.Max;
            }

            ints[i * 2] = node.Id;
            ints[i * 2 + 1] = flags;
            var s = i * 6;
            strs[s] = node.Role ?? string.Empty;
            strs[s + 1] = node.Label ?? string.Empty;
            strs[s + 2] = node.Hint ?? string.Empty;
            strs[s + 3] = node.Live ?? string.Empty;
            strs[s + 4] = node.Value?.Text ?? string.Empty;
            strs[s + 5] = DrawnUi.Models.Aria.PressedStateAttribute(node.Role);
        }

        JsUpdate(ints, nums, strs);
    }

    private static ISkiaAccessibilityNode? Find(int id)
    {
        var canvas = _canvas;
        if (canvas == null)
            return null;

        foreach (var node in canvas.AccessibilityManager.Snapshot)
        {
            if (node.Id == id)
                return node.Source;
        }

        return null;
    }

    private static void OnActivate(int id) => SkiaAccessibilityManager.Activate(Find(id));

    // arrows, Home / End, PageUp / PageDown: the node first (a slider steps), else its group
    private static bool OnKey(int id, string key) =>
        Enum.TryParse(key, out InputKey inputKey) && SkiaAccessibilityManager.Key(Find(id), inputKey);

    // the focused element is the canvas keyboard focus (ring, scroll bars) and the screen reader's node; a node off
    // screen is scrolled into view
    private static void OnFocus(int id)
    {
        var canvas = _canvas;
        var source = Find(id);
        if (canvas == null || source == null)
            return;

        canvas.AccessibilityManager.NotifyReaderFocused(source);
        canvas.KeyboardFocusNode = source;
        source.OnAccessibilityFocused(true); // a text field takes the caret, as on the other heads
        SkiaAccessibilityManager.ScrollIntoView(source);
        Push(); // the Tab stop of its group moves with it (tabindex)
    }

    private static void OnBlur(int id)
    {
        var canvas = _canvas;
        var source = Find(id);
        if (canvas == null || source == null)
            return;

        if (ReferenceEquals(canvas.AccessibilityManager.ReaderNode, source))
            canvas.AccessibilityManager.NotifyReaderFocused(null); // focus left the canvas: never pull it back later
        source.OnAccessibilityFocused(false);
        if (ReferenceEquals(canvas.KeyboardFocusNode, source))
            canvas.KeyboardFocusNode = null!;
    }

    // arrow keys in a group moved keyboard focus to another node
    private static void OnKeyboardFocusRequested(ISkiaAccessibilityNode node)
    {
        Push();
        JsFocus(node.AccessibilityId);
    }

    // the screen reader's node left with its page: focus the first node that says something (raised after Changed,
    // so its element exists)
    private static void OnReaderRefocus(AccessibilityNode next) => JsFocus(next.Id);

    private static void OnLiveRegionUpdated(ISkiaAccessibilityNode node)
    {
        var text = node.AccessibilityLabel ?? string.Empty;
        var value = node.GetAccessibilityValue()?.Text;
        if (!string.IsNullOrEmpty(value))
            text = string.IsNullOrEmpty(text) ? value : $"{text}, {value}";
        JsLive(node.AccessibilityId, text);
    }

    // a page that just opened may draw no more frames: rebuild the skipped snapshot once the rate limit allows
    private static void OnRebuildSkipped(long remainingMs)
    {
        var due = Math.Max(1, remainingMs) + 16;
        if (_refresh == null)
            _refresh = new System.Threading.Timer(_ =>
            {
                if (_canvas is { } canvas)
                    canvas.AccessibilityManager.RefreshIfStale(canvas.RenderingScale);
            }, null, due, System.Threading.Timeout.Infinite);
        else
            _refresh.Change(due, System.Threading.Timeout.Infinite);
    }
}
