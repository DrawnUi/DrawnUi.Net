using System.Runtime.Versioning;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;

namespace DrawnUi.Draw;

// ── Host interface ────────────────────────────────────────────────────────────
// Implemented by the platform view so the automation peer can read a11y state
// without a hard dependency on the private handler inner class.

[SupportedOSPlatform("windows")]
internal interface IDrawnUiA11yHost
{
    SkiaAccessibilityManager? A11yManager { get; set; }
    Func<(double x, double y)>? A11yGetOrigin { get; set; }
    Func<float>? A11yGetScale { get; set; }
    Microsoft.UI.Xaml.Automation.Peers.AutomationPeer? A11yPeer { get; set; }
}

// ── Container peer (backed by the SwapChainPanel platform view) ───────────────

[SupportedOSPlatform("windows")]
internal sealed class DrawnUiAutomationPeer : FrameworkElementAutomationPeer
{
    private readonly IDrawnUiA11yHost _host;
    private List<DrawnUiVirtualAutomationPeer> _cachedChildren = [];
    internal IReadOnlyList<DrawnUiVirtualAutomationPeer> CachedChildren => _cachedChildren;
    internal void EnsureChildrenCached() { if (_cachedChildren.Count == 0) GetChildrenCore(); }

    internal DrawnUiAutomationPeer(Microsoft.UI.Xaml.FrameworkElement owner, IDrawnUiA11yHost host)
        : base(owner)
    {
        _host = host;
    }

    // Stable peer cache: same ISkiaAccessibilityNode source → same peer instance.
    // WinUI3 finds siblings by reference equality inside GetChildrenCore results;
    // recreating new instances on every call breaks GetNextSibling navigation.
    private readonly Dictionary<ISkiaAccessibilityNode, DrawnUiVirtualAutomationPeer> _peerCache = new();

    // Which virtual peer currently holds keyboard focus (null = none).
    internal DrawnUiVirtualAutomationPeer? FocusedPeer { get; private set; }

    internal void NotifyStructureChanged()
        => RaiseStructureChangedEvent(AutomationStructureChangeType.ChildrenInvalidated, null);

    internal void ClearVirtualFocus()
    {
        FocusedPeer = null;
    }

    // Returns true if focus moved to a child; false if we're past the boundary (let XAML Tab continue).
    internal bool MoveFocusToNext(bool forward)
    {
        if (_cachedChildren.Count == 0)
            GetChildren();
        if (_cachedChildren.Count == 0)
            return false;

        var focusable = _cachedChildren.Where(p => p.Source?.AccessibilityCanInteract == true).ToList();
        if (focusable.Count == 0)
            focusable = _cachedChildren;

        int current = FocusedPeer == null ? (forward ? -1 : focusable.Count) : focusable.IndexOf(FocusedPeer);
        int next    = forward ? current + 1 : current - 1;

        if (next < 0 || next >= focusable.Count)
            return false;

        var prev = FocusedPeer;
        FocusedPeer = focusable[next];

        // Notify input controls (e.g. SkiaEditor) so they can activate/deactivate their
        // native input sink — gives Tab-in/Tab-out cursor behaviour matching standard fields.
        prev?.Source?.OnAccessibilityFocused(false);
        FocusedPeer.Source?.OnAccessibilityFocused(true);

        // Proper prev→next focus transition so Narrator reliably tracks virtual focus
        // even without an HWND-level change (same pattern as ListViewItemAutomationPeer).
        prev?.RaisePropertyChangedEvent(
            AutomationElementIdentifiers.HasKeyboardFocusProperty, true, false);
        FocusedPeer.RaisePropertyChangedEvent(
            AutomationElementIdentifiers.HasKeyboardFocusProperty, false, true);
        FocusedPeer.RaiseAutomationEvent(AutomationEvents.AutomationFocusChanged);

        return true;
    }

    internal void ActivateFocused()
    {
        // Called from OnCanvasKeyDown (already on main thread) — call directly.
        System.Diagnostics.Debug.WriteLine($"[A11y-ACT] ActivateFocused FocusedPeer={(FocusedPeer == null ? "NULL" : FocusedPeer.Role)} source={(FocusedPeer?.Source == null ? "NULL" : FocusedPeer.Source.GetType().Name)}");
        if (FocusedPeer?.Source != null)
        {
            FocusedPeer.RaiseAutomationEvent(AutomationEvents.InvokePatternOnInvoked);
            FocusedPeer.Source.OnAccessibilityActivated();
            // Re-announce focused element so Narrator says the button name/role after invocation.
            FocusedPeer.RaiseAutomationEvent(AutomationEvents.AutomationFocusChanged);
        }
    }

    internal void NotifyFocusChanged(ISkiaAccessibilityNode? focused)
    {
        // Populate children cache if AT has not traversed yet.
        if (_cachedChildren.Count == 0)
            GetChildren();

        var prev = FocusedPeer;
        FocusedPeer = focused == null
            ? null
            : _cachedChildren.FirstOrDefault(p => ReferenceEquals(p.Source, focused));

        // Notify previous peer it lost focus — without this, Narrator keeps the old outline.
        if (prev != null && !ReferenceEquals(prev, FocusedPeer))
            prev.RaisePropertyChangedEvent(
                AutomationElementIdentifiers.HasKeyboardFocusProperty, true, false);

        if (FocusedPeer != null)
        {
            // RaisePropertyChangedEvent signals Narrator even when canvas already holds
            // XAML focus and no HWND-level transition occurs — same pattern used by
            // ListViewItemAutomationPeer for in-list focus changes.
            FocusedPeer.RaisePropertyChangedEvent(
                AutomationElementIdentifiers.HasKeyboardFocusProperty, false, true);
            FocusedPeer.RaiseAutomationEvent(AutomationEvents.AutomationFocusChanged);
        }
    }

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AutomationControlType.Pane;

    protected override string GetClassNameCore() => "DrawnUiCanvas";

    protected override AutomationPeer? GetPeerFromPointCore(Windows.Foundation.Point point)
    {
        if (_cachedChildren.Count == 0)
            GetChildren();

        for (int i = _cachedChildren.Count - 1; i >= 0; i--)
        {
            var child = _cachedChildren[i];
            var rect  = child.GetBoundingRectangle();
            if (point.X >= rect.Left && point.X <= rect.Right &&
                point.Y >= rect.Top  && point.Y <= rect.Bottom)
                return child;
        }
        return this;
    }

    protected override IList<AutomationPeer> GetChildrenCore()
    {
        var manager  = _host.A11yManager;
        var getScale = _host.A11yGetScale;
        if (manager == null || getScale == null)
            return base.GetChildrenCore() ?? [];

        var snap = manager.Snapshot;
        var list = new List<DrawnUiVirtualAutomationPeer>(snap.Length);
        for (int i = 0; i < snap.Length; i++)
        {
            var node = snap[i];
            if (node.Source != null && _peerCache.TryGetValue(node.Source, out var existing))
            {
                existing.UpdateSnapshot(node, i);
                list.Add(existing);
            }
            else
            {
                var peer = new DrawnUiVirtualAutomationPeer(node, i, this, getScale);
                if (node.Source != null) _peerCache[node.Source] = peer;
                list.Add(peer);
            }
        }
        _cachedChildren = list;
        return [.. list];
    }
}

// ── Virtual peer (no backing UIElement) ──────────────────────────────────────

[SupportedOSPlatform("windows")]
internal sealed class DrawnUiVirtualAutomationPeer : AutomationPeer, IInvokeProvider
{
    private AccessibilityNode _node;
    private int _index;
    private readonly DrawnUiAutomationPeer _parent;
    private readonly Func<float> _getScale;

    internal ISkiaAccessibilityNode? Source => _node.Source;
    internal string? Role => _node.Role;

    internal void UpdateSnapshot(AccessibilityNode node, int index)
    {
        _node  = node;
        _index = index;
    }

    internal DrawnUiVirtualAutomationPeer(
        AccessibilityNode node, int index,
        DrawnUiAutomationPeer parent,
        Func<float> getScale)
    {
        _node     = node;
        _index    = index;
        _parent   = parent;
        _getScale = getScale;
    }

    // ── Core overrides ────────────────────────────────────────────────────────

    // Read live label directly from Source so AT gets the latest value even before the
    // rate-limited snapshot rebuild fires.
    protected override string GetNameCore()               => Source?.AccessibilityLabel ?? _node.Label ?? string.Empty;
    protected override string GetHelpTextCore()           => _node.Hint  ?? string.Empty;
    protected override string GetClassNameCore()          => "DrawnUiElement";
    protected override string GetLocalizedControlTypeCore() => _node.Role ?? "custom";
    protected override string GetAutomationIdCore()       => $"drawnui_{_index}";
    protected override string GetAcceleratorKeyCore()     => string.Empty;
    protected override string GetAccessKeyCore()          => string.Empty;
    protected override string GetItemStatusCore()         => string.Empty;
    protected override string GetItemTypeCore()           => string.Empty;

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AriaToControlType(_node.Role);

    protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.None;

    protected override bool IsKeyboardFocusableCore() => _node.CanInteract;
    protected override bool IsEnabledCore()           => true;
    protected override bool IsOffscreenCore()         => false;
    protected override bool IsContentElementCore()    => true;
    protected override bool IsControlElementCore()    => true;
    protected override bool HasKeyboardFocusCore()    => ReferenceEquals(this, _parent.FocusedPeer);
    protected override bool IsPasswordCore()          => false;
    protected override bool IsRequiredForFormCore()   => false;

    protected override AutomationLiveSetting GetLiveSettingCore() => (_node.Live ?? Source?.AccessibilityLive) switch
    {
        "assertive" => AutomationLiveSetting.Assertive,
        "polite"    => AutomationLiveSetting.Polite,
        _           => AutomationLiveSetting.Off,
    };

    internal void RaiseLiveRegionChanged()
    {
        // Refresh name from source so AT reads the latest value.
        RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    protected override Windows.Foundation.Rect GetBoundingRectangleCore()
    {
        // Use the parent peer's bounding rect (correct screen coords from FrameworkElementAutomationPeer)
        // as the canvas origin. _node.Rect is in DrawnUI logical units; multiply by renderingScale.
        var canvasRect = _parent.GetBoundingRectangle();
        var scale      = Math.Max(_getScale(), 1f);
        return new Windows.Foundation.Rect(
            canvasRect.X + _node.Rect.Left * scale,
            canvasRect.Y + _node.Rect.Top  * scale,
            _node.Rect.Width  * scale,
            _node.Rect.Height * scale);
    }

    protected override Windows.Foundation.Point GetClickablePointCore()
    {
        var r = GetBoundingRectangleCore();
        return new Windows.Foundation.Point(r.X + r.Width / 2, r.Y + r.Height / 2);
    }

    protected override AutomationPeer? GetLabeledByCore() => null;
    protected override IList<AutomationPeer>? GetChildrenCore() => null;

    protected override object? GetPatternCore(PatternInterface patternInterface)
        => patternInterface == PatternInterface.Invoke && _node.CanInteract ? this : null;

    protected override void SetFocusCore()
    {
        var source = _node.Source;
        if (source != null)
            MainThread.BeginInvokeOnMainThread(() => source.OnAccessibilityActivated());
    }

    // IInvokeProvider ─────────────────────────────────────────────────────────

    public void Invoke()
    {
        var source = _node.Source;
        System.Diagnostics.Debug.WriteLine($"[A11y-UIA] Invoke() called on '{_node.Label}' role='{_node.Role}' source={(source == null ? "NULL" : source.GetType().Name)}");
        if (source != null)
        {
            RaiseAutomationEvent(AutomationEvents.InvokePatternOnInvoked);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                source.OnAccessibilityActivated();
                RaiseAutomationEvent(AutomationEvents.AutomationFocusChanged);
            });
        }
    }

    // ── Aria → AutomationControlType ─────────────────────────────────────────

    private static AutomationControlType AriaToControlType(string? role) => role switch
    {
        "button"           => AutomationControlType.Button,
        "link"             => AutomationControlType.Hyperlink,
        "checkbox"         => AutomationControlType.CheckBox,
        "radio"            => AutomationControlType.RadioButton,
        "switch"           => AutomationControlType.CheckBox,
        "slider"           => AutomationControlType.Slider,
        "spinbutton"       => AutomationControlType.Spinner,
        "textbox"          => AutomationControlType.Edit,
        "searchbox"        => AutomationControlType.Edit,
        "combobox"         => AutomationControlType.ComboBox,
        "listbox"          => AutomationControlType.List,
        "option"           => AutomationControlType.ListItem,
        "tab"              => AutomationControlType.TabItem,
        "tablist"          => AutomationControlType.Tab,
        "tabpanel"         => AutomationControlType.Pane,
        "menu"             => AutomationControlType.Menu,
        "menubar"          => AutomationControlType.MenuBar,
        "menuitem"         => AutomationControlType.MenuItem,
        "menuitemcheckbox" => AutomationControlType.MenuItem,
        "menuitemradio"    => AutomationControlType.MenuItem,
        "scrollbar"        => AutomationControlType.ScrollBar,
        "text"             => AutomationControlType.Text,
        "heading"          => AutomationControlType.Text,
        "img"              => AutomationControlType.Image,
        "list"             => AutomationControlType.List,
        "listitem"         => AutomationControlType.ListItem,
        "progressbar"      => AutomationControlType.ProgressBar,
        "tooltip"          => AutomationControlType.ToolTip,
        "group"            => AutomationControlType.Group,
        "dialog"           => AutomationControlType.Pane,
        "alertdialog"      => AutomationControlType.Pane,
        "status"           => AutomationControlType.StatusBar,
        "alert"            => AutomationControlType.Text,
        "region"           => AutomationControlType.Pane,
        "navigation"       => AutomationControlType.Pane,
        "main"             => AutomationControlType.Pane,
        "separator"        => AutomationControlType.Separator,
        "presentation"     => AutomationControlType.Custom,
        _                  => AutomationControlType.Custom,
    };
}
