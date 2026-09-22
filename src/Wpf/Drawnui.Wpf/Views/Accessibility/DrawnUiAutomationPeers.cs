using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using DrawnUi.Draw;
using DrawnUi.Views;
using IInvokeProvider = System.Windows.Automation.Provider.IInvokeProvider;
using Point = System.Windows.Point;


namespace DrawnUi.Wpf;

/// <summary>
/// UI Automation view of a <see cref="DrawnUiElement"/>: the element is a Pane whose children are
/// virtual peers, one per node of the engine's <see cref="SkiaAccessibilityManager.Snapshot"/>.
/// WPF port of the MAUI Windows peers (<c>Platforms/Windows/Accessibility/MauiWindowsAutomationPeer.cs</c>).
/// </summary>
internal sealed class DrawnUiElementAutomationPeer : FrameworkElementAutomationPeer
{
    private readonly DrawnUiElement _element;
    private List<DrawnUiVirtualAutomationPeer> _children = new();

    // Stable peers: the same source node keeps the same peer, or sibling navigation breaks.
    private readonly Dictionary<ISkiaAccessibilityNode, DrawnUiVirtualAutomationPeer> _cache = new();

    internal DrawnUiElementAutomationPeer(DrawnUiElement owner) : base(owner)
    {
        _element = owner;
    }

    /// <summary>The virtual peer holding keyboard focus, null when none.</summary>
    internal DrawnUiVirtualAutomationPeer FocusedPeer { get; private set; }

    internal DrawnUiElement Element => _element;

    internal void NotifyStructureChanged()
    {
        ResetChildrenCache();
        RaiseAutomationEvent(AutomationEvents.StructureChanged);
    }

    internal void ClearVirtualFocus()
    {
        var previous = FocusedPeer;
        FocusedPeer = null;
        previous?.Source?.OnAccessibilityFocused(false);
        previous?.RaisePropertyChangedEvent(AutomationElementIdentifiers.HasKeyboardFocusProperty, true, false);
    }

    /// <summary>Moves virtual focus; false when past either end, so WPF Tab navigation continues.</summary>
    internal bool MoveFocus(bool forward)
    {
        EnsureChildren();
        var focusable = _children.Where(p => p.Source?.AccessibilityCanInteract == true).ToList();
        if (focusable.Count == 0)
            return false;

        var current = FocusedPeer == null ? (forward ? -1 : focusable.Count) : focusable.IndexOf(FocusedPeer);
        var next = forward ? current + 1 : current - 1;
        if (next < 0 || next >= focusable.Count)
        {
            ClearVirtualFocus();
            return false;
        }

        SetFocused(focusable[next]);
        return true;
    }

    internal void ActivateFocused()
    {
        if (FocusedPeer?.Source == null)
            return;

        FocusedPeer.RaiseAutomationEvent(AutomationEvents.InvokePatternOnInvoked);
        FocusedPeer.Source.OnAccessibilityActivated();
    }

    /// <summary>The engine reported a focus change (a control took focus by pointer).</summary>
    internal void NotifyFocusChanged(ISkiaAccessibilityNode focused)
    {
        EnsureChildren();
        var peer = focused == null ? null : _children.FirstOrDefault(p => ReferenceEquals(p.Source, focused));
        if (ReferenceEquals(peer, FocusedPeer))
            return;

        var previous = FocusedPeer;
        FocusedPeer = peer;
        previous?.RaisePropertyChangedEvent(AutomationElementIdentifiers.HasKeyboardFocusProperty, true, false);
        peer?.RaisePropertyChangedEvent(AutomationElementIdentifiers.HasKeyboardFocusProperty, false, true);
        peer?.RaiseAutomationEvent(AutomationEvents.AutomationFocusChanged);
    }

    internal void NotifyLiveRegion(ISkiaAccessibilityNode node)
    {
        EnsureChildren();
        _children.FirstOrDefault(p => ReferenceEquals(p.Source, node))?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void SetFocused(DrawnUiVirtualAutomationPeer peer)
    {
        var previous = FocusedPeer;
        FocusedPeer = peer;

        // lets input controls (SkiaEditor) take / release their input sink on Tab-in / Tab-out
        previous?.Source?.OnAccessibilityFocused(false);
        peer.Source?.OnAccessibilityFocused(true);

        previous?.RaisePropertyChangedEvent(AutomationElementIdentifiers.HasKeyboardFocusProperty, true, false);
        peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.HasKeyboardFocusProperty, false, true);
        peer.RaiseAutomationEvent(AutomationEvents.AutomationFocusChanged);
    }

    private void EnsureChildren()
    {
        if (_children.Count == 0)
            GetChildrenCore();
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

    protected override string GetClassNameCore() => "DrawnUiCanvas";

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override List<AutomationPeer> GetChildrenCore()
    {
        var manager = _element.Canvas?.AccessibilityManager;
        if (manager == null)
            return null;

        var snapshot = manager.Snapshot;
        var list = new List<DrawnUiVirtualAutomationPeer>(snapshot.Length);
        for (var i = 0; i < snapshot.Length; i++)
        {
            var node = snapshot[i];
            if (node.Source != null && _cache.TryGetValue(node.Source, out var existing))
            {
                existing.Update(node, i);
                list.Add(existing);
            }
            else
            {
                var peer = new DrawnUiVirtualAutomationPeer(node, i, this);
                if (node.Source != null)
                    _cache[node.Source] = peer;
                list.Add(peer);
            }
        }

        // forget peers whose node left the snapshot
        if (_cache.Count > list.Count * 2 + 16)
        {
            var alive = new HashSet<ISkiaAccessibilityNode>(snapshot.Where(n => n.Source != null).Select(n => n.Source));
            foreach (var dead in _cache.Keys.Where(k => !alive.Contains(k)).ToList())
                _cache.Remove(dead);
        }

        _children = list;
        return list.Cast<AutomationPeer>().ToList();
    }

    protected override AutomationPeer GetPeerFromPointCore(Point point)
    {
        EnsureChildren();
        for (var i = _children.Count - 1; i >= 0; i--)
        {
            if (_children[i].GetBoundingRectangle().Contains(point))
                return _children[i];
        }

        return this;
    }
}

/// <summary>One accessibility node of the drawn tree; has no backing UIElement.</summary>
internal sealed class DrawnUiVirtualAutomationPeer : AutomationPeer, IInvokeProvider, IToggleProvider
{
    private readonly DrawnUiElementAutomationPeer _parent;
    private AccessibilityNode _node;
    private int _index;

    internal DrawnUiVirtualAutomationPeer(AccessibilityNode node, int index, DrawnUiElementAutomationPeer parent)
    {
        _node = node;
        _index = index;
        _parent = parent;
    }

    internal ISkiaAccessibilityNode Source => _node.Source;

    /// <summary>Node rectangle in the element's device-independent units (DrawnUI points).</summary>
    internal System.Windows.Rect LocalRect => new(_node.Rect.Left, _node.Rect.Top, Math.Max(0, _node.Rect.Width), Math.Max(0, _node.Rect.Height));

    internal void Update(AccessibilityNode node, int index)
    {
        _node = node;
        _index = index;
    }

    // live label: the snapshot is rate-limited, the source is not
    protected override string GetNameCore() => Source?.AccessibilityLabel ?? _node.Label ?? string.Empty;
    protected override string GetHelpTextCore() => _node.Hint ?? string.Empty;
    protected override string GetClassNameCore() => "DrawnUiNode";
    protected override string GetLocalizedControlTypeCore() => _node.Role ?? "custom";
    protected override string GetAutomationIdCore() => $"drawnui_{_index}";
    protected override string GetAcceleratorKeyCore() => string.Empty;
    protected override string GetAccessKeyCore() => string.Empty;
    protected override string GetItemStatusCore() => string.Empty;
    protected override string GetItemTypeCore() => string.Empty;
    protected override AutomationControlType GetAutomationControlTypeCore() => AriaToControlType(_node.Role);
    protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.None;
    protected override bool IsKeyboardFocusableCore() => _node.CanInteract;
    protected override bool IsEnabledCore() => true;
    protected override bool IsOffscreenCore() => false;
    protected override bool IsContentElementCore() => true;
    protected override bool IsControlElementCore() => true;
    protected override bool HasKeyboardFocusCore() => ReferenceEquals(this, _parent.FocusedPeer);
    protected override bool IsPasswordCore() => false;
    protected override bool IsRequiredForFormCore() => false;
    protected override AutomationPeer GetLabeledByCore() => null;
    protected override List<AutomationPeer> GetChildrenCore() => null;

    protected override AutomationLiveSetting GetLiveSettingCore() => (_node.Live ?? Source?.AccessibilityLive) switch
    {
        "assertive" => AutomationLiveSetting.Assertive,
        "polite" => AutomationLiveSetting.Polite,
        _ => AutomationLiveSetting.Off,
    };

    protected override System.Windows.Rect GetBoundingRectangleCore()
    {
        var element = _parent.Element;
        if (PresentationSource.FromVisual(element) == null)
            return System.Windows.Rect.Empty;

        var local = LocalRect;
        var topLeft = element.PointToScreen(local.TopLeft);
        var bottomRight = element.PointToScreen(local.BottomRight);
        return new System.Windows.Rect(topLeft, bottomRight);
    }

    protected override Point GetClickablePointCore()
    {
        var rect = GetBoundingRectangleCore();
        return rect.IsEmpty ? new Point(double.NaN, double.NaN) : new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
    }

    public override object GetPattern(PatternInterface patternInterface)
    {
        if (!_node.CanInteract)
            return null;

        return patternInterface switch
        {
            PatternInterface.Invoke => this,
            PatternInterface.Toggle when (Source?.AccessibilityIsPressed ?? _node.IsPressed).HasValue => this,
            _ => null,
        };
    }

    protected override void SetFocusCore()
    {
        _parent.Element.Dispatcher.BeginInvoke(() =>
        {
            _parent.Element.Focus();
            _parent.NotifyFocusChanged(Source);
            _parent.Element.InvalidateFocusRing();
        });
    }

    /// <inheritdoc/>
    public void Invoke()
    {
        var source = Source;
        if (source == null)
            return;

        RaiseAutomationEvent(AutomationEvents.InvokePatternOnInvoked);
        _parent.Element.Dispatcher.BeginInvoke(source.OnAccessibilityActivated);
    }

    /// <inheritdoc/>
    public void Toggle() => Invoke();

    /// <inheritdoc/>
    public ToggleState ToggleState => (Source?.AccessibilityIsPressed ?? _node.IsPressed) switch
    {
        true => ToggleState.On,
        false => ToggleState.Off,
        _ => ToggleState.Indeterminate,
    };

    private static AutomationControlType AriaToControlType(string role) => role switch
    {
        "button" => AutomationControlType.Button,
        "link" => AutomationControlType.Hyperlink,
        "checkbox" or "switch" => AutomationControlType.CheckBox,
        "radio" => AutomationControlType.RadioButton,
        "slider" => AutomationControlType.Slider,
        "spinbutton" => AutomationControlType.Spinner,
        "textbox" or "searchbox" => AutomationControlType.Edit,
        "combobox" => AutomationControlType.ComboBox,
        "listbox" or "list" => AutomationControlType.List,
        "option" or "listitem" => AutomationControlType.ListItem,
        "tab" => AutomationControlType.TabItem,
        "tablist" => AutomationControlType.Tab,
        "menu" => AutomationControlType.Menu,
        "menubar" => AutomationControlType.MenuBar,
        "menuitem" or "menuitemcheckbox" or "menuitemradio" => AutomationControlType.MenuItem,
        "scrollbar" => AutomationControlType.ScrollBar,
        "text" or "heading" or "alert" => AutomationControlType.Text,
        "img" => AutomationControlType.Image,
        "progressbar" => AutomationControlType.ProgressBar,
        "tooltip" => AutomationControlType.ToolTip,
        "group" => AutomationControlType.Group,
        "status" => AutomationControlType.StatusBar,
        "separator" => AutomationControlType.Separator,
        "tabpanel" or "dialog" or "alertdialog" or "region" or "navigation" or "main" => AutomationControlType.Pane,
        _ => AutomationControlType.Custom,
    };
}
