using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DrawnUi.Draw;
using DrawnUi.Views;

namespace DrawnUi.OpenTk;

// COM interfaces, UiaControlType/UiaPropertyId/UiaRect/UiaProviderOptions/UiaNavigateDirection
// are in DrawnUi.Draw (shared project: Shared/Platforms/Windows/WindowsUiaInterfaces.cs).

// ── Root provider ─────────────────────────────────────────────────────────────

[SupportedOSPlatform("windows")]
[ComVisible(true)]
internal sealed class WindowRootProvider
    : IRawElementProviderSimple, IRawElementProviderFragment, IRawElementProviderFragmentRoot
{
    private readonly nint _hwnd;
    internal readonly SkiaAccessibilityManager Manager;
    private readonly Func<float> _getScale;

    internal WindowRootProvider(nint hwnd, SkiaAccessibilityManager manager, Func<float> getScale)
    {
        _hwnd     = hwnd;
        Manager   = manager;
        _getScale = getScale;
    }

    internal float Scale => Math.Max(_getScale(), 1f);

    // IRawElementProviderSimple ───────────────────────────────────────────────

    public UiaProviderOptions ProviderOptions =>
        UiaProviderOptions.ServerSideProvider | UiaProviderOptions.UseComThreading;

    public object? GetPatternProvider(int patternId) => null;

    public object? GetPropertyValue(int propertyId) => propertyId switch
    {
        UiaPropertyId.ControlType      => UiaControlType.Pane,
        UiaPropertyId.IsControlElement => false,
        UiaPropertyId.IsContentElement => false,
        _ => null
    };

    public IRawElementProviderSimple? HostRawElementProvider
    {
        get
        {
            UiaHostProviderFromHwnd(_hwnd, out var p);
            return p;
        }
    }

    // IRawElementProviderFragment ─────────────────────────────────────────────

    public IRawElementProviderFragment? Navigate(UiaNavigateDirection direction)
    {
        var snap = Manager.Snapshot;
        Trace.WriteLine($"[UIA] Root.Navigate({direction}) snapshot={snap.Length}");
        return direction switch
        {
            UiaNavigateDirection.FirstChild => snap.Length > 0 ? MakeChild(snap[0], 0) : null,
            UiaNavigateDirection.LastChild  => snap.Length > 0 ? MakeChild(snap[^1], snap.Length - 1) : null,
            _ => null
        };
    }

    public int[]? GetRuntimeId() => null;

    public UiaRect BoundingRectangle => default;

    public object[]? GetEmbeddedFragmentRoots() => null;

    public void SetFocus() { }

    public IRawElementProviderFragmentRoot? FragmentRoot => this;

    // IRawElementProviderFragmentRoot ─────────────────────────────────────────

    public IRawElementProviderFragment? ElementProviderFromPoint(double x, double y)
    {
        var origin = GetClientScreenOrigin(_hwnd);
        var scale  = Scale;
        var snap   = Manager.Snapshot;

        // Reverse order: topmost (last) wins.
        for (int i = snap.Length - 1; i >= 0; i--)
        {
            var n = snap[i];
            var sl = origin.x + n.Rect.Left   * scale;
            var st = origin.y + n.Rect.Top    * scale;
            var sr = sl       + n.Rect.Width  * scale;
            var sb = st       + n.Rect.Height * scale;
            if (x >= sl && x <= sr && y >= st && y <= sb)
                return MakeChild(n, i);
        }
        return this;
    }

    public IRawElementProviderFragment? GetFocus() => null;

    // ── Helpers ───────────────────────────────────────────────────────────────

    internal VirtualElementProvider MakeChild(AccessibilityNode node, int index)
        => new(node, index, this, _hwnd, _getScale);

    internal static (double x, double y) GetClientScreenOrigin(nint hwnd)
    {
        var pt = new POINT();
        ClientToScreen(hwnd, ref pt);
        return (pt.X, pt.Y);
    }

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(nint hwnd, ref POINT pt);

    [DllImport("UIAutomationCore.dll")]
    private static extern int UiaHostProviderFromHwnd(
        nint hwnd,
        [MarshalAs(UnmanagedType.Interface)] out IRawElementProviderSimple ppProvider);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }
}

// ── Virtual element provider ──────────────────────────────────────────────────

[SupportedOSPlatform("windows")]
[ComVisible(true)]
internal sealed class VirtualElementProvider
    : IRawElementProviderSimple, IRawElementProviderFragment, IInvokeProvider
{
    private readonly AccessibilityNode _node;
    private readonly int _index;
    private readonly WindowRootProvider _root;
    private readonly nint _hwnd;
    private readonly Func<float> _getScale;

    private const int UiaAppendRuntimeId = 3;

    internal VirtualElementProvider(
        AccessibilityNode node, int index,
        WindowRootProvider root, nint hwnd, Func<float> getScale)
    {
        _node     = node;
        _index    = index;
        _root     = root;
        _hwnd     = hwnd;
        _getScale = getScale;
    }

    // IRawElementProviderSimple ───────────────────────────────────────────────

    public UiaProviderOptions ProviderOptions =>
        UiaProviderOptions.ServerSideProvider | UiaProviderOptions.UseComThreading;

    public object? GetPatternProvider(int patternId) => patternId switch
    {
        UiaPatternId.Invoke when _node.CanInteract => this,
        _ => null
    };

    public void Invoke()
    {
        var source = _node.Source;
        if (source != null)
            MainThread.BeginInvokeOnMainThread(() => source.OnAccessibilityActivated());
    }

    public object? GetPropertyValue(int propertyId) => propertyId switch
    {
        UiaPropertyId.Name                 => _node.Label,
        UiaPropertyId.HelpText             => _node.Hint,
        UiaPropertyId.ControlType          => AriaToControlType(_node.Role),
        UiaPropertyId.LocalizedControlType => _node.Role ?? "custom",
        UiaPropertyId.IsKeyboardFocusable  => _node.CanInteract,
        UiaPropertyId.IsEnabled            => true,
        UiaPropertyId.IsControlElement     => true,
        UiaPropertyId.IsContentElement     => true,
        _ => null
    };

    public IRawElementProviderSimple? HostRawElementProvider => null;

    // IRawElementProviderFragment ─────────────────────────────────────────────

    public IRawElementProviderFragment? Navigate(UiaNavigateDirection direction)
    {
        var snap = _root.Manager.Snapshot;
        return direction switch
        {
            UiaNavigateDirection.Parent          => _root,
            UiaNavigateDirection.NextSibling     => _index + 1 < snap.Length
                                                        ? _root.MakeChild(snap[_index + 1], _index + 1)
                                                        : null,
            UiaNavigateDirection.PreviousSibling => _index > 0
                                                        ? _root.MakeChild(snap[_index - 1], _index - 1)
                                                        : null,
            _ => null
        };
    }

    public int[]? GetRuntimeId() => [UiaAppendRuntimeId, _index];

    public UiaRect BoundingRectangle
    {
        get
        {
            var origin = WindowRootProvider.GetClientScreenOrigin(_hwnd);
            var scale  = Math.Max(_getScale(), 1f);
            return new UiaRect
            {
                left   = origin.x + _node.Rect.Left   * scale,
                top    = origin.y + _node.Rect.Top    * scale,
                width  = _node.Rect.Width  * scale,
                height = _node.Rect.Height * scale,
            };
        }
    }

    public object[]? GetEmbeddedFragmentRoots() => null;

    public void SetFocus()
    {
        var source = _node.Source;
        if (source != null)
            MainThread.BeginInvokeOnMainThread(() => source.OnAccessibilityActivated());
    }

    public IRawElementProviderFragmentRoot? FragmentRoot => _root;

    // ── Aria → UIA control type ───────────────────────────────────────────────

    private static int AriaToControlType(string? role) => role switch
    {
        "button"           => UiaControlType.Button,
        "link"             => UiaControlType.Hyperlink,
        "checkbox"         => UiaControlType.CheckBox,
        "radio"            => UiaControlType.RadioButton,
        "switch"           => UiaControlType.CheckBox,
        "slider"           => UiaControlType.Slider,
        "spinbutton"       => UiaControlType.Spinner,
        "textbox"          => UiaControlType.Edit,
        "searchbox"        => UiaControlType.Edit,
        "combobox"         => UiaControlType.ComboBox,
        "listbox"          => UiaControlType.List,
        "option"           => UiaControlType.ListItem,
        "tab"              => UiaControlType.TabItem,
        "tablist"          => UiaControlType.Tab,
        "tabpanel"         => UiaControlType.Pane,
        "menu"             => UiaControlType.Menu,
        "menubar"          => UiaControlType.MenuBar,
        "menuitem"         => UiaControlType.MenuItem,
        "menuitemcheckbox" => UiaControlType.MenuItem,
        "menuitemradio"    => UiaControlType.MenuItem,
        "scrollbar"        => UiaControlType.ScrollBar,
        "text"             => UiaControlType.Text,
        "heading"          => UiaControlType.Text,
        "img"              => UiaControlType.Image,
        "list"             => UiaControlType.List,
        "listitem"         => UiaControlType.ListItem,
        "progressbar"      => UiaControlType.ProgressBar,
        "tooltip"          => UiaControlType.ToolTip,
        "group"            => UiaControlType.Group,
        "dialog"           => UiaControlType.Pane,
        "alertdialog"      => UiaControlType.Pane,
        "status"           => UiaControlType.StatusBar,
        "alert"            => UiaControlType.Text,
        "region"           => UiaControlType.Pane,
        "navigation"       => UiaControlType.Pane,
        "main"             => UiaControlType.Pane,
        "separator"        => UiaControlType.Separator,
        "presentation"     => UiaControlType.Custom,
        _                  => UiaControlType.Custom,
    };
}

internal enum StructureChangeType
{
    ChildrenAdded       = 0,
    ChildrenRemoved     = 1,
    ChildrenReordered   = 2,
    ChildrenBulkAdded   = 3,
    ChildrenBulkRemoved = 4,
    ChildrenInvalidated = 5,
}

// ── Orchestrator ──────────────────────────────────────────────────────────────

[SupportedOSPlatform("windows")]
internal sealed class WindowsUiaProvider : IDisposable
{
    private readonly nint _hwnd;
    private readonly WindowRootProvider _root;
    private readonly SkiaAccessibilityManager _manager;

    private const uint WM_GETOBJECT   = 0x003D;
    private static readonly nint UiaRootObjectId = (nint)(-25);

    internal WindowsUiaProvider(nint hwnd, SkiaAccessibilityManager manager, Func<float> getScale)
    {
        _hwnd    = hwnd;
        _manager = manager;
        _root    = new WindowRootProvider(hwnd, manager, getScale);
        _manager.Changed += OnSnapshotChanged;
    }

    internal nint HandleMessage(uint msg, nint wParam, nint lParam)
    {
        if (msg != WM_GETOBJECT) return 0;

        Trace.WriteLine($"[UIA] WM_GETOBJECT lParam=0x{lParam:X} UiaRootId=0x{UiaRootObjectId:X} snapshot={_manager.Snapshot.Length}");

        if (lParam == UiaRootObjectId)
        {
            var result = UiaReturnRawElementProvider(_hwnd, wParam, lParam, _root);
            Trace.WriteLine($"[UIA] UiaReturnRawElementProvider → 0x{result:X}");
            return result;
        }
        return 0;
    }

    private void OnSnapshotChanged()
    {
        Trace.WriteLine($"[UIA] Snapshot changed → {_manager.Snapshot.Length} nodes, raising StructureChanged");
        UiaRaiseStructureChangedEvent(_root, StructureChangeType.ChildrenInvalidated, null, 0);
    }

    internal void NotifyFocusChanged(ISkiaAccessibilityNode? focused)
    {
        if (focused == null) return;
        var snap = _manager.Snapshot;
        var idx  = Array.FindIndex(snap, n => ReferenceEquals(n.Source, focused));
        if (idx < 0) return;
        UiaRaiseAutomationEvent(_root.MakeChild(snap[idx], idx), UIA_AutomationFocusChangedEventId);
    }

    public void Dispose()
    {
        _manager.Changed -= OnSnapshotChanged;
    }

    [DllImport("UIAutomationCore.dll", SetLastError = false)]
    private static extern nint UiaReturnRawElementProvider(
        nint hwnd, nint wParam, nint lParam,
        [MarshalAs(UnmanagedType.Interface)] IRawElementProviderSimple el);

    [DllImport("UIAutomationCore.dll", SetLastError = false)]
    private static extern int UiaRaiseStructureChangedEvent(
        [MarshalAs(UnmanagedType.Interface)] IRawElementProviderSimple provider,
        StructureChangeType structureChangeType,
        int[]? runtimeId,
        int runtimeIdLen);

    private const int UIA_AutomationFocusChangedEventId = 20005;

    [DllImport("UIAutomationCore.dll", SetLastError = false)]
    private static extern int UiaRaiseAutomationEvent(
        [MarshalAs(UnmanagedType.Interface)] IRawElementProviderSimple provider,
        int id);
}
