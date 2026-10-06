using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using DrawnUi.Draw;
using DrawnUi.Views;
using Tmds.DBus.Protocol;

namespace DrawnUi.OpenTk;

/// <summary>
/// Linux screen readers (Orca) through AT-SPI2 over D-Bus: the window publishes the accessibility snapshot
/// (<see cref="SkiaAccessibilityManager"/>, the same nodes UI Automation gets on Windows) on the accessibility bus.
/// The tree is the application root, one frame for the window, and one object per snapshot node under the frame, in
/// reading order. Shaped after AccessKit's Linux adapter (accesskit_unix, accesskit_atspi_common), which DrawnUi.Rust
/// uses: same roles, states, actions, values and events. Costs nothing until assistive technology turns the
/// accessibility bus on (org.a11y.Status IsEnabled / ScreenReaderEnabled).
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxAtSpiProvider : IDisposable
{
    private const string RootPath = "/org/a11y/atspi/accessible/root";
    private const string FramePath = "/org/a11y/atspi/accessible/frame";
    private const string NodePrefix = "/org/a11y/atspi/accessible/n";
    private const string CachePath = "/org/a11y/atspi/cache";
    private const string NullPath = "/org/a11y/atspi/null";

    private const string IAccessible = "org.a11y.atspi.Accessible";
    private const string IApplication = "org.a11y.atspi.Application";
    private const string IComponent = "org.a11y.atspi.Component";
    private const string IAction = "org.a11y.atspi.Action";
    private const string IValue = "org.a11y.atspi.Value";
    private const string ICache = "org.a11y.atspi.Cache";
    private const string IProperties = "org.freedesktop.DBus.Properties";
    private const string IEventObject = "org.a11y.atspi.Event.Object";
    private const string IEventWindow = "org.a11y.atspi.Event.Window";

    // AT-SPI roles (atspi-common Role) and state bits (StateSet: bit n of the 64-bit set)
    private const uint RoleApplication = 75, RoleFrame = 23, RoleToggleButton = 62, RoleButton = 43;
    private const int StActive = 1, StChecked = 4, StEditable = 7, StEnabled = 8, StFocusable = 11, StFocused = 12,
        StHorizontal = 14, StPressed = 20, StSensitive = 24, StShowing = 25, StSingleLine = 26, StVertical = 29,
        StVisible = 30, StCheckable = 41;

    private delegate void Body(ref MessageWriter writer);

    private readonly SkiaAccessibilityManager _manager;
    private readonly Func<float> _getScale;
    private readonly Func<string> _getTitle;
    private readonly object _lock = new();

    // the client area on screen, in pixels, and whether the window has focus: written by the window thread
    private volatile int _clientX, _clientY, _clientWidth, _clientHeight;
    private volatile bool _active;

    private Connection? _bus;
    private string _busName = string.Empty;
    private (string Name, string Path) _desktop = (string.Empty, NullPath);
    private int _applicationId = -1;
    private int _connecting;
    private IDisposable? _statusWatch;
    private System.Threading.Timer? _refresh;
    private bool _disposed;

    // what the screen reader was told: the nodes in reading order, by id
    private AccessibilityNode[] _nodes = [];
    private readonly Dictionary<int, int> _index = new();
    private int? _focusedId;

    internal LinuxAtSpiProvider(SkiaAccessibilityManager manager, Func<float> getScale, Func<string> getTitle)
    {
        _manager = manager;
        _getScale = getScale;
        _getTitle = getTitle;
    }

    /// <summary>The window's client area on screen and its focus, from the window thread.</summary>
    internal void UpdateWindow(int x, int y, int width, int height)
    {
        _clientX = x;
        _clientY = y;
        _clientWidth = width;
        _clientHeight = height;
    }

    /// <summary>
    /// Waits for assistive technology to turn the accessibility bus on, then connects and registers. Fails quietly:
    /// a session without D-Bus or without AT-SPI just has no screen reader.
    /// </summary>
    internal void Start() => _ = RunAsync();

    private async Task RunAsync()
    {
        try
        {
            var address = Environment.GetEnvironmentVariable("AT_SPI_BUS_ADDRESS");
            if (!string.IsNullOrEmpty(address))
            {
                await ConnectAsync(address);
                return;
            }

            var session = Connection.Session;

            // a screen reader that starts later turns the bus on then
            _statusWatch = await session.AddMatchAsync(
                new MatchRule
                {
                    Type = MessageType.Signal, Path = "/org/a11y/bus", Interface = IProperties, Member = "PropertiesChanged"
                },
                (message, _) => ReadStatusChange(message),
                (exception, enabled, _, _) =>
                {
                    if (exception == null && enabled)
                        _ = ConnectFromSessionAsync();
                },
                ObserverFlags.None, null, null, false);

            if (await IsStatusOnAsync(session, "IsEnabled") || await IsStatusOnAsync(session, "ScreenReaderEnabled"))
                await ConnectFromSessionAsync();
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[AT-SPI] not available: {e.Message}");
        }
    }

    private static Task<bool> IsStatusOnAsync(Connection session, string property) =>
        session.CallMethodAsync(Message(session, "org.a11y.Bus", "/org/a11y/bus", IProperties, "Get", "ss",
                (ref MessageWriter w) =>
                {
                    w.WriteString("org.a11y.Status");
                    w.WriteString(property);
                }),
            (message, _) => message.GetBodyReader().ReadVariantValue().GetBool(), null);

    // a method call, built before any await (a message writer cannot live across one)
    private static MessageBuffer Message(Connection connection, string destination, string path, string iface, string member,
        string? signature, Body? body)
    {
        var writer = connection.GetMessageWriter();
        try
        {
            writer.WriteMethodCallHeader(destination, path, iface, member, signature, MessageFlags.None);
            body?.Invoke(ref writer);
            return writer.CreateMessage();
        }
        finally
        {
            writer.Dispose();
        }
    }

    // PropertiesChanged (sa{sv}as) of org.a11y.Status: true when IsEnabled or ScreenReaderEnabled turned on
    private static bool ReadStatusChange(Message message)
    {
        var reader = message.GetBodyReader();
        if (reader.ReadString() != "org.a11y.Status")
            return false;

        var end = reader.ReadDictionaryStart();
        while (reader.HasNext(end))
        {
            reader.AlignStruct();
            var name = reader.ReadString();
            var value = reader.ReadVariantValue();
            if (name is "IsEnabled" or "ScreenReaderEnabled" && value.GetBool())
                return true;
        }

        return false;
    }

    private async Task ConnectFromSessionAsync()
    {
        var session = Connection.Session;
        var address = await session.CallMethodAsync(Message(session, "org.a11y.Bus", "/org/a11y/bus", "org.a11y.Bus", "GetAddress", null, null),
            (message, _) => message.GetBodyReader().ReadString(), null);
        await ConnectAsync(address);
    }

    private async Task ConnectAsync(string address)
    {
        if (Interlocked.Exchange(ref _connecting, 1) == 1 || _disposed)
            return;

        try
        {
            var bus = new Connection(address);
            await bus.ConnectAsync();
            _bus = bus;
            _busName = bus.UniqueName ?? string.Empty;

            // objects first: the registry calls back during Embed (it sets Application.Id)
            bus.AddMethodHandler(new PathHandler(this, RootPath));
            bus.AddMethodHandler(new PathHandler(this, FramePath));
            bus.AddMethodHandler(new PathHandler(this, CachePath));
            lock (_lock)
                Publish(_manager.Snapshot, announce: false);

            var busName = _busName;
            _desktop = await bus.CallMethodAsync(
                Message(bus, "org.a11y.atspi.Registry", RootPath, "org.a11y.atspi.Socket", "Embed", "(so)",
                    (ref MessageWriter w) => WriteRef(ref w, busName, RootPath)),
                (message, _) =>
                {
                    var reader = message.GetBodyReader();
                    reader.AlignStruct();
                    return (reader.ReadString(), reader.ReadObjectPathAsString());
                }, null);

            _manager.Changed += OnSnapshotChanged;
            _manager.FocusChanged += OnFocusChanged;
            _manager.LiveRegionUpdated += OnLiveRegionUpdated;
            _manager.ReaderRefocusRequested += OnReaderRefocus;
            _manager.RebuildSkipped += OnRebuildSkipped;

            // the window joined the application, then it is the active one if it has focus
            EmitObject(RootPath, "ChildrenChanged", "add", 0, 0, (ref MessageWriter w) => WriteRefVariant(ref w, _busName, FramePath));
            if (_active)
                EmitActivate(true);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[AT-SPI] registration failed: {e.Message}");
            Interlocked.Exchange(ref _connecting, 0);
        }
    }

    #region Snapshot to AT-SPI

    // under _lock: the new nodes become the published ones; with announce, the changes are signalled
    private void Publish(AccessibilityNode[] snapshot, bool announce)
    {
        var bus = _bus;
        if (bus == null)
            return;

        var previous = _nodes;
        var previousIndex = new Dictionary<int, int>(_index);

        _nodes = snapshot;
        _index.Clear();
        for (var i = 0; i < snapshot.Length; i++)
            _index[snapshot[i].Id] = i;

        foreach (var node in previous)
        {
            if (_index.ContainsKey(node.Id))
                continue;

            var path = NodePath(node.Id);
            bus.RemoveMethodHandler(path);
            if (_focusedId == node.Id)
                _focusedId = null;
            if (!announce)
                continue;
            EmitObject(FramePath, "ChildrenChanged", "remove", -1, 0, (ref MessageWriter w) => WriteRefVariant(ref w, _busName, path));
            EmitObject(path, "StateChanged", "defunct", 1, 0, WriteNoData);
            Emit(CachePath, ICache, "RemoveAccessible", "(so)", (ref MessageWriter w) => WriteRef(ref w, _busName, path));
        }

        for (var i = 0; i < snapshot.Length; i++)
        {
            var node = snapshot[i];
            if (!previousIndex.TryGetValue(node.Id, out var before))
            {
                var path = NodePath(node.Id);
                bus.AddMethodHandler(new PathHandler(this, path));
                if (!announce)
                    continue;
                var index = i;
                EmitObject(FramePath, "ChildrenChanged", "add", index, 0, (ref MessageWriter w) => WriteRefVariant(ref w, _busName, path));
                Emit(CachePath, ICache, "AddAccessible", "((so)(so)(so)iiassusau)", (ref MessageWriter w) => WriteCacheItem(ref w, node, index));
                continue;
            }

            if (announce)
                AnnounceChanges(previous[before], node);
        }
    }

    // what a screen reader reads again on a node it already knows: its name, value and checked / pressed / enabled state
    private void AnnounceChanges(AccessibilityNode was, AccessibilityNode now)
    {
        var path = NodePath(now.Id);
        var name = Name(now);
        if (Name(was) != name)
            EmitObject(path, "PropertyChange", "accessible-name", 0, 0, (ref MessageWriter w) => w.WriteVariantString(name));

        if (now.Value is { } value && was.Value?.Now != value.Now)
            EmitObject(path, "PropertyChange", "accessible-value", 0, 0, (ref MessageWriter w) => w.WriteVariantDouble(value.Now));

        if (was.IsPressed != now.IsPressed && now.IsPressed is { } pressed)
            EmitObject(path, "StateChanged", Role(now) == RoleToggleButton ? "pressed" : "checked", pressed ? 1 : 0, 0, WriteNoData);

        var enabled = IsEnabled(now);
        if (IsEnabled(was) != enabled)
        {
            EmitObject(path, "StateChanged", "enabled", enabled ? 1 : 0, 0, WriteNoData);
            EmitObject(path, "StateChanged", "sensitive", enabled ? 1 : 0, 0, WriteNoData);
        }
    }

    private void OnSnapshotChanged()
    {
        lock (_lock)
            Publish(_manager.Snapshot, announce: true);
    }

    // Orca speaks the node that gets the focused state; the one that lost it is told after
    private void OnFocusChanged(ISkiaAccessibilityNode? focused)
    {
        _manager.NotifyReaderFocused(focused);
        if (focused is SkiaControl control)
            SkiaScroll.EnsureVisible(control);

        int? old, now = null;
        lock (_lock)
        {
            old = _focusedId;
            if (focused != null && _index.ContainsKey(focused.AccessibilityId))
                now = focused.AccessibilityId;
            _focusedId = now;
        }

        if (old == now)
            return;
        if (now is { } id)
            EmitObject(NodePath(id), "StateChanged", "focused", 1, 0, WriteNoData);
        if (old is { } oldId)
            EmitObject(NodePath(oldId), "StateChanged", "focused", 0, 0, WriteNoData);
    }

    // the reader's node left with its page: focus moves to the first node that says something, as on Windows
    private void OnReaderRefocus(AccessibilityNode next)
    {
        var source = next.Source;
        if (source != null)
            MainThread.BeginInvokeOnMainThread(() => _manager.NotifyFocused(source));
    }

    private void OnLiveRegionUpdated(ISkiaAccessibilityNode node)
    {
        var text = node.AccessibilityLabel ?? string.Empty;
        var value = node.GetAccessibilityValue()?.Text;
        if (!string.IsNullOrEmpty(value))
            text = string.IsNullOrEmpty(text) ? value : $"{text}, {value}";
        if (string.IsNullOrEmpty(text))
            return;

        var politeness = node.AccessibilityLive == DrawnUi.Models.Aria.LiveAssertive ? 2 : 1;
        EmitObject(NodePath(node.AccessibilityId), "Announcement", string.Empty, politeness, 0, (ref MessageWriter w) => w.WriteVariantString(text));
    }

    // a page that just opened may draw no more frames: rebuild the skipped snapshot once the rate limit allows
    private void OnRebuildSkipped(long remainingMs)
    {
        var due = Math.Max(1, remainingMs) + 16;
        if (_refresh == null)
            _refresh = new System.Threading.Timer(_ => _manager.RefreshIfStale(Math.Max(_getScale(), 1f)), null, due, System.Threading.Timeout.Infinite);
        else
            _refresh.Change(due, System.Threading.Timeout.Infinite);
    }

    /// <summary>The window gained or lost focus: Orca follows the active window.</summary>
    internal void SetActive(bool active)
    {
        if (_active == active)
            return;
        _active = active;
        if (_bus != null && Volatile.Read(ref _connecting) == 1)
            EmitActivate(active);
    }

    private void EmitActivate(bool active)
    {
        var title = _getTitle() ?? string.Empty;
        Emit(FramePath, IEventWindow, active ? "Activate" : "Deactivate", "siiva{sv}", (ref MessageWriter w) =>
        {
            w.WriteString(string.Empty);
            w.WriteInt32(0);
            w.WriteInt32(0);
            w.WriteVariantString(title);
            WriteEmptyProperties(ref w);
        });
        EmitObject(FramePath, "StateChanged", "active", active ? 1 : 0, 0, WriteNoData);
        if (!active)
            return;

        EmitObject(RootPath, "ActiveDescendantChanged", string.Empty, 0, 0, (ref MessageWriter w) => WriteRefVariant(ref w, _busName, FramePath));
        int? focused;
        lock (_lock)
            focused = _focusedId;
        if (focused is { } id)
            EmitObject(NodePath(id), "StateChanged", "focused", 1, 0, WriteNoData);
    }

    #endregion

    #region Node data

    private static string NodePath(int id) => NodePrefix + id.ToString(CultureInfo.InvariantCulture);

    // a node named by a title text inside is not named again (said once)
    private static string Name(AccessibilityNode node) => node.NamedByChild ? string.Empty : node.Label ?? string.Empty;

    // a control role that takes no input reads as unavailable (drawnui-cross 6c rule 2)
    private static bool IsEnabled(AccessibilityNode node) => node.CanInteract || !DrawnUi.Models.Aria.IsInteractiveRole(node.Role);

    private static uint Role(AccessibilityNode node) => node.Role switch
    {
        "button" => node.IsPressed.HasValue ? RoleToggleButton : RoleButton,
        "link" => 88,
        "checkbox" => 7,
        "radio" => 44,
        "switch" => RoleToggleButton,
        "slider" => 51,
        "spinbutton" => 52,
        "textbox" or "searchbox" => 79,
        "combobox" => 11,
        "listbox" => 98,
        "option" or "listitem" => 32,
        "tab" => 37,
        "tablist" => 38,
        "tabpanel" or "group" or "radiogroup" => 39,
        "menu" => 33,
        "menubar" => 34,
        "menuitem" => 35,
        "menuitemcheckbox" => 8,
        "menuitemradio" => 45,
        "scrollbar" => 48,
        "separator" => 50,
        "progressbar" => 42,
        "text" => 29,
        "heading" => 83,
        "img" => 27,
        "list" => 31,
        "grid" => 55,
        "toolbar" => 63,
        "tooltip" => 64,
        "dialog" or "alertdialog" => 16,
        "status" => 54,
        "alert" => 101,
        "region" or "navigation" or "main" => 110,
        _ => 85,
    };

    private static string RoleName(uint role) => role switch
    {
        RoleApplication => "application",
        RoleFrame => "frame",
        RoleButton => "push button",
        RoleToggleButton => "toggle button",
        7 => "check box",
        44 => "radio button",
        51 => "slider",
        42 => "progress bar",
        29 => "label",
        83 => "heading",
        27 => "image",
        88 => "link",
        31 => "list",
        32 => "list item",
        79 => "entry",
        63 => "tool bar",
        55 => "table",
        54 => "status bar",
        39 => "panel",
        _ => "section",
    };

    private ulong NodeState(AccessibilityNode node, bool focused)
    {
        var state = Bit(StVisible) | Bit(StShowing);
        if (IsEnabled(node))
            state |= Bit(StEnabled) | Bit(StSensitive);
        if (node.CanInteract)
            state |= Bit(StFocusable);
        if (focused)
            state |= Bit(StFocused);
        if (node.IsPressed is { } pressed)
        {
            if (Role(node) == RoleToggleButton)
            {
                if (pressed)
                    state |= Bit(StPressed);
            }
            else
            {
                state |= Bit(StCheckable);
                if (pressed)
                    state |= Bit(StChecked);
            }
        }

        if (node.Value is { } value)
            state |= Bit(value.Vertical ? StVertical : StHorizontal);
        if (node.Role is "textbox" or "searchbox" && node.CanInteract)
            state |= Bit(StEditable) | Bit(StSingleLine);
        return state;
    }

    private ulong FrameState()
    {
        var state = Bit(StEnabled) | Bit(StSensitive) | Bit(StVisible) | Bit(StShowing);
        if (_active)
            state |= Bit(StActive);
        return state;
    }

    private static ulong Bit(int bit) => 1UL << bit;

    private static string[] NodeInterfaces(AccessibilityNode node)
    {
        var list = new List<string>(4) { IAccessible, IComponent };
        if (node.CanInteract)
            list.Add(IAction);
        if (node.Value != null)
            list.Add(IValue);
        return list.ToArray();
    }

    // node rect in window pixels (units times the rendering scale)
    private (int X, int Y, int Width, int Height) WindowRect(AccessibilityNode node)
    {
        var scale = Math.Max(_getScale(), 1f);
        var r = node.Rect;
        return ((int)(r.Left * scale), (int)(r.Top * scale), (int)(r.Width * scale), (int)(r.Height * scale));
    }

    // coord type: 0 screen, 1 window, 2 parent (the frame, whose origin is the window's)
    private (int X, int Y, int Width, int Height) Extents(AccessibilityNode? node, uint coordType)
    {
        var rect = node == null ? (0, 0, _clientWidth, _clientHeight) : WindowRect(node);
        if (coordType == 0)
            rect = (rect.Item1 + _clientX, rect.Item2 + _clientY, rect.Item3, rect.Item4);
        else if (coordType == 2 && node == null)
            rect = (_clientX, _clientY, rect.Item3, rect.Item4); // the frame within the desktop
        return rect;
    }

    #endregion

    #region D-Bus methods

    private enum Kind { Root, Frame, Node, Cache }

    private sealed class PathHandler(LinuxAtSpiProvider owner, string path) : IMethodHandler
    {
        public string Path => path;

        public bool RunMethodHandlerSynchronously(Message message) => true;

        public ValueTask HandleMethodAsync(MethodContext context)
        {
            owner.Handle(path, context);
            return default;
        }
    }

    private void Handle(string path, MethodContext context)
    {
        if (context.IsDBusIntrospectRequest)
        {
            context.ReplyIntrospectXml([]);
            return;
        }

        Kind kind;
        AccessibilityNode? node = null;
        var index = -1;
        AccessibilityNode[] nodes;
        int? focusedId;
        lock (_lock)
        {
            nodes = _nodes;
            focusedId = _focusedId;
            if (path == RootPath)
                kind = Kind.Root;
            else if (path == FramePath)
                kind = Kind.Frame;
            else if (path == CachePath)
                kind = Kind.Cache;
            else if (int.TryParse(path.AsSpan(NodePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                     && _index.TryGetValue(id, out index))
            {
                kind = Kind.Node;
                node = nodes[index];
            }
            else
            {
                context.ReplyError("org.freedesktop.DBus.Error.UnknownObject", $"{path} is gone");
                return;
            }
        }

        var target = new Target(kind, node, index, nodes, node != null && node.Id == focusedId);
        var request = context.Request;
        var iface = request.InterfaceAsString ?? string.Empty;
        var member = request.MemberAsString ?? string.Empty;

        var handled = iface switch
        {
            IProperties => HandleProperties(context, target, member),
            "org.freedesktop.DBus.Peer" => HandlePeer(context, member),
            IAccessible when kind != Kind.Cache => HandleAccessible(context, target, member),
            IComponent when kind is Kind.Frame or Kind.Node => HandleComponent(context, target, member),
            IAction when node is { CanInteract: true } => HandleAction(context, node, member),
            IApplication when kind == Kind.Root && member == "GetLocale" => Reply(context, "s", (ref MessageWriter w) => w.WriteString(string.Empty)),
            ICache when kind == Kind.Cache && member == "GetItems" => Reply(context, "a((so)(so)(so)iiassusau)", (ref MessageWriter w) => WriteCacheItems(ref w, nodes)),
            _ => false,
        };

        if (!handled)
            context.ReplyError("org.freedesktop.DBus.Error.UnknownMethod", $"{iface}.{member} is not supported on {path}");
    }

    private readonly record struct Target(Kind Kind, AccessibilityNode? Node, int Index, AccessibilityNode[] Nodes, bool Focused);

    private static bool HandlePeer(MethodContext context, string member) => member switch
    {
        "Ping" => Reply(context, null, (ref MessageWriter _) => { }),
        "GetMachineId" => Reply(context, "s", (ref MessageWriter w) => w.WriteString(string.Empty)),
        _ => false,
    };

    private bool HandleAccessible(MethodContext context, Target t, string member)
    {
        switch (member)
        {
            case "GetChildAtIndex":
            {
                var i = context.Request.GetBodyReader().ReadInt32();
                var child = ChildPath(t, i);
                return Reply(context, "(so)", (ref MessageWriter w) =>
                    WriteRef(ref w, child == NullPath ? string.Empty : _busName, child));
            }
            case "GetChildren":
                return Reply(context, "a(so)", (ref MessageWriter w) =>
                {
                    var array = w.WriteArrayStart(DBusType.Struct);
                    var count = ChildCount(t);
                    for (var i = 0; i < count; i++)
                        WriteRef(ref w, _busName, ChildPath(t, i));
                    w.WriteArrayEnd(array);
                });
            case "GetIndexInParent":
                return Reply(context, "i", (ref MessageWriter w) => w.WriteInt32(t.Kind switch
                {
                    Kind.Root => -1,
                    Kind.Frame => 0,
                    _ => t.Index,
                }));
            case "GetRelationSet":
                return Reply(context, "a(ua(so))", (ref MessageWriter w) =>
                {
                    var array = w.WriteArrayStart(DBusType.Struct);
                    w.WriteArrayEnd(array);
                });
            case "GetRole":
                return Reply(context, "u", (ref MessageWriter w) => w.WriteUInt32(RoleOf(t)));
            case "GetRoleName":
            case "GetLocalizedRoleName":
                return Reply(context, "s", (ref MessageWriter w) => w.WriteString(RoleName(RoleOf(t))));
            case "GetState":
                return Reply(context, "au", (ref MessageWriter w) => WriteState(ref w, StateOf(t)));
            case "GetAttributes":
                return Reply(context, "a{ss}", (ref MessageWriter w) =>
                {
                    var dictionary = w.WriteDictionaryStart();
                    w.WriteDictionaryEnd(dictionary);
                });
            case "GetApplication":
                return Reply(context, "(so)", (ref MessageWriter w) => WriteRef(ref w, _busName, RootPath));
            case "GetInterfaces":
                return Reply(context, "as", (ref MessageWriter w) => w.WriteArray(InterfacesOf(t)));
            default:
                return false;
        }
    }

    private bool HandleComponent(MethodContext context, Target t, string member)
    {
        switch (member)
        {
            case "GetExtents":
            {
                var extents = Extents(t.Node, context.Request.GetBodyReader().ReadUInt32());
                return Reply(context, "(iiii)", (ref MessageWriter w) =>
                {
                    w.WriteStructureStart();
                    w.WriteInt32(extents.X);
                    w.WriteInt32(extents.Y);
                    w.WriteInt32(extents.Width);
                    w.WriteInt32(extents.Height);
                });
            }
            case "GetPosition":
            {
                var extents = Extents(t.Node, context.Request.GetBodyReader().ReadUInt32());
                return Reply(context, "ii", (ref MessageWriter w) =>
                {
                    w.WriteInt32(extents.X);
                    w.WriteInt32(extents.Y);
                });
            }
            case "GetSize":
            {
                var extents = Extents(t.Node, 1);
                return Reply(context, "ii", (ref MessageWriter w) =>
                {
                    w.WriteInt32(extents.Width);
                    w.WriteInt32(extents.Height);
                });
            }
            case "Contains":
            {
                var reader = context.Request.GetBodyReader();
                int x = reader.ReadInt32(), y = reader.ReadInt32();
                var e = Extents(t.Node, reader.ReadUInt32());
                var inside = x >= e.X && y >= e.Y && x < e.X + e.Width && y < e.Y + e.Height;
                return Reply(context, "b", (ref MessageWriter w) => w.WriteBool(inside));
            }
            case "GetAccessibleAtPoint":
            {
                var reader = context.Request.GetBodyReader();
                int x = reader.ReadInt32(), y = reader.ReadInt32();
                var coordType = reader.ReadUInt32();
                var hit = NullPath;
                if (t.Kind == Kind.Frame)
                {
                    // the last node in reading order that contains the point: a title over its card
                    for (var i = t.Nodes.Length - 1; i >= 0; i--)
                    {
                        var e = Extents(t.Nodes[i], coordType);
                        if (x >= e.X && y >= e.Y && x < e.X + e.Width && y < e.Y + e.Height)
                        {
                            hit = NodePath(t.Nodes[i].Id);
                            break;
                        }
                    }
                }

                return Reply(context, "(so)", (ref MessageWriter w) => WriteRef(ref w, hit == NullPath ? string.Empty : _busName, hit));
            }
            case "GetLayer":
                return Reply(context, "u", (ref MessageWriter w) => w.WriteUInt32(t.Kind == Kind.Frame ? 7u : 3u));
            case "GetMDIZOrder":
                return Reply(context, "n", (ref MessageWriter w) => w.WriteInt16(-1));
            case "GetAlpha":
                return Reply(context, "d", (ref MessageWriter w) => w.WriteDouble(1));
            case "GrabFocus":
            {
                var source = t.Node?.Source;
                if (source != null && t.Node!.CanInteract)
                    MainThread.BeginInvokeOnMainThread(() => _manager.NotifyFocused(source));
                var granted = source != null && t.Node!.CanInteract;
                return Reply(context, "b", (ref MessageWriter w) => w.WriteBool(granted));
            }
            case "ScrollTo":
            case "ScrollToPoint":
            {
                var source = t.Node?.Source;
                if (source != null)
                    MainThread.BeginInvokeOnMainThread(() => SkiaAccessibilityManager.ScrollIntoView(source));
                return Reply(context, "b", (ref MessageWriter w) => w.WriteBool(source != null));
            }
            default:
                return false;
        }
    }

    private static bool HandleAction(MethodContext context, AccessibilityNode node, string member)
    {
        switch (member)
        {
            case "GetName":
            case "GetLocalizedName":
            {
                var i = context.Request.GetBodyReader().ReadInt32();
                return Reply(context, "s", (ref MessageWriter w) => w.WriteString(i == 0 ? "click" : string.Empty));
            }
            case "GetDescription":
            case "GetKeyBinding":
                return Reply(context, "s", (ref MessageWriter w) => w.WriteString(string.Empty));
            case "GetActions":
                return Reply(context, "a(sss)", (ref MessageWriter w) =>
                {
                    var array = w.WriteArrayStart(DBusType.Struct);
                    w.WriteStructureStart();
                    w.WriteString("click");
                    w.WriteString(string.Empty);
                    w.WriteString(string.Empty);
                    w.WriteArrayEnd(array);
                });
            case "DoAction":
            {
                var i = context.Request.GetBodyReader().ReadInt32();
                var source = node.Source;
                if (i == 0 && source != null)
                    MainThread.BeginInvokeOnMainThread(() => SkiaAccessibilityManager.Activate(source));
                return Reply(context, "b", (ref MessageWriter w) => w.WriteBool(i == 0 && source != null));
            }
            default:
                return false;
        }
    }

    private bool HandleProperties(MethodContext context, Target t, string member)
    {
        var reader = context.Request.GetBodyReader();
        switch (member)
        {
            case "Get":
            {
                var iface = reader.ReadString();
                var name = reader.ReadString();
                if (!HasProperty(t, iface, name))
                {
                    context.ReplyError("org.freedesktop.DBus.Error.UnknownProperty", $"{iface}.{name}");
                    return true;
                }

                return Reply(context, "v", (ref MessageWriter w) => WriteProperty(ref w, t, iface, name));
            }
            case "GetAll":
            {
                var iface = reader.ReadString();
                return Reply(context, "a{sv}", (ref MessageWriter w) =>
                {
                    var dictionary = w.WriteDictionaryStart();
                    foreach (var name in PropertyNames(iface))
                    {
                        if (!HasProperty(t, iface, name))
                            continue;
                        w.WriteDictionaryEntryStart();
                        w.WriteString(name);
                        WriteProperty(ref w, t, iface, name);
                    }

                    w.WriteDictionaryEnd(dictionary);
                });
            }
            case "Set":
            {
                var iface = reader.ReadString();
                var name = reader.ReadString();
                var value = reader.ReadVariantValue();
                if (iface == IApplication && name == "Id" && t.Kind == Kind.Root)
                    _applicationId = value.GetInt32();
                else if (iface == IValue && name == "CurrentValue" && t.Node?.Source is { } source && t.Node.Value != null)
                {
                    var target = value.GetDouble();
                    MainThread.BeginInvokeOnMainThread(() => SkiaAccessibilityManager.SetValue(source, target));
                }
                else
                {
                    context.ReplyError("org.freedesktop.DBus.Error.PropertyReadOnly", $"{iface}.{name}");
                    return true;
                }

                return Reply(context, null, (ref MessageWriter _) => { });
            }
            default:
                return false;
        }
    }

    private static readonly string[] AccessibleProperties = ["Name", "Description", "Parent", "ChildCount", "Locale", "AccessibleId", "HelpText"];
    private static readonly string[] ApplicationProperties = ["ToolkitName", "Version", "AtspiVersion", "Id"];
    private static readonly string[] ActionProperties = ["NActions"];
    private static readonly string[] ValueProperties = ["MinimumValue", "MaximumValue", "MinimumIncrement", "CurrentValue", "Text"];

    private static string[] PropertyNames(string iface) => iface switch
    {
        IAccessible => AccessibleProperties,
        IApplication => ApplicationProperties,
        IAction => ActionProperties,
        IValue => ValueProperties,
        _ => [],
    };

    private static bool HasProperty(Target t, string iface, string name) => iface switch
    {
        IAccessible => t.Kind != Kind.Cache && Array.IndexOf(AccessibleProperties, name) >= 0,
        IApplication => t.Kind == Kind.Root && Array.IndexOf(ApplicationProperties, name) >= 0,
        IAction => t.Node is { CanInteract: true } && name == "NActions",
        IValue => t.Node?.Value != null && Array.IndexOf(ValueProperties, name) >= 0,
        _ => false,
    };

    private void WriteProperty(ref MessageWriter w, Target t, string iface, string name)
    {
        switch (iface, name)
        {
            case (IAccessible, "Name"):
                w.WriteVariantString(t.Kind switch
                {
                    Kind.Root => Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "DrawnUI",
                    Kind.Frame => _getTitle() ?? string.Empty,
                    _ => Name(t.Node!),
                });
                break;
            case (IAccessible, "Description"):
                w.WriteVariantString(t.Node?.Hint ?? string.Empty);
                break;
            case (IAccessible, "Parent"):
                var parent = t.Kind switch
                {
                    Kind.Root => _desktop,
                    Kind.Frame => (_busName, RootPath),
                    _ => (_busName, FramePath),
                };
                WriteRefVariant(ref w, parent.Item1, parent.Item2);
                break;
            case (IAccessible, "ChildCount"):
                w.WriteVariantInt32(ChildCount(t));
                break;
            case (IAccessible, "Locale"):
            case (IAccessible, "AccessibleId"):
            case (IAccessible, "HelpText"):
                w.WriteVariantString(string.Empty);
                break;
            case (IApplication, "ToolkitName"):
                w.WriteVariantString("DrawnUI");
                break;
            case (IApplication, "Version"):
                w.WriteVariantString(typeof(LinuxAtSpiProvider).Assembly.GetName().Version?.ToString() ?? string.Empty);
                break;
            case (IApplication, "AtspiVersion"):
                w.WriteVariantString("2.1");
                break;
            case (IApplication, "Id"):
                w.WriteVariantInt32(_applicationId);
                break;
            case (IAction, "NActions"):
                w.WriteVariantInt32(1);
                break;
            case (IValue, _):
                // the live value: an adjust is read back at once, the snapshot follows on the next frame
                var value = t.Node!.Source?.GetAccessibilityValue() ?? t.Node.Value!.Value;
                if (name == "Text")
                    w.WriteVariantString(value.Text ?? string.Empty);
                else
                    w.WriteVariantDouble(name switch
                    {
                        "MinimumValue" => value.Min,
                        "MaximumValue" => value.Max,
                        "MinimumIncrement" => value.Step,
                        _ => value.Now,
                    });
                break;
        }
    }

    private static int ChildCount(Target t) => t.Kind switch
    {
        Kind.Root => 1,
        Kind.Frame => t.Nodes.Length,
        _ => 0,
    };

    private static string ChildPath(Target t, int i) => t.Kind switch
    {
        Kind.Root when i == 0 => FramePath,
        Kind.Frame when i >= 0 && i < t.Nodes.Length => NodePath(t.Nodes[i].Id),
        _ => NullPath,
    };

    private static uint RoleOf(Target t) => t.Kind switch
    {
        Kind.Root => RoleApplication,
        Kind.Frame => RoleFrame,
        _ => Role(t.Node!),
    };

    private ulong StateOf(Target t) => t.Kind switch
    {
        Kind.Root => 0,
        Kind.Frame => FrameState(),
        _ => NodeState(t.Node!, t.Focused),
    };

    private static string[] InterfacesOf(Target t) => t.Kind switch
    {
        Kind.Root => [IAccessible, IApplication],
        Kind.Frame => [IAccessible, IComponent],
        _ => NodeInterfaces(t.Node!),
    };

    // org.a11y.atspi.Cache items: object, application, parent, index in parent, child count, interfaces, name, role,
    // description, state. The application comes first, under the desktop.
    private void WriteCacheItems(ref MessageWriter w, AccessibilityNode[] nodes)
    {
        int? focused;
        lock (_lock)
            focused = _focusedId;

        var array = w.WriteArrayStart(DBusType.Struct);
        WriteCacheEntry(ref w, RootPath, _desktop, -1, 1, [IAccessible, IApplication],
            Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "DrawnUI", RoleApplication, string.Empty, 0);
        WriteCacheEntry(ref w, FramePath, (_busName, RootPath), 0, nodes.Length, [IAccessible, IComponent],
            _getTitle() ?? string.Empty, RoleFrame, string.Empty, FrameState());
        for (var i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            WriteCacheEntry(ref w, NodePath(node.Id), (_busName, FramePath), i, 0, NodeInterfaces(node), Name(node), Role(node),
                node.Hint ?? string.Empty, NodeState(node, node.Id == focused));
        }

        w.WriteArrayEnd(array);
    }

    private void WriteCacheItem(ref MessageWriter w, AccessibilityNode node, int index) =>
        WriteCacheEntry(ref w, NodePath(node.Id), (_busName, FramePath), index, 0, NodeInterfaces(node), Name(node), Role(node),
            node.Hint ?? string.Empty, NodeState(node, false));

    private void WriteCacheEntry(ref MessageWriter w, string path, (string Name, string Path) parent, int index, int childCount,
        string[] interfaces, string name, uint role, string description, ulong state)
    {
        w.WriteStructureStart();
        WriteRef(ref w, _busName, path);
        WriteRef(ref w, _busName, RootPath);
        WriteRef(ref w, parent.Name, parent.Path);
        w.WriteInt32(index);
        w.WriteInt32(childCount);
        w.WriteArray(interfaces);
        w.WriteString(name);
        w.WriteUInt32(role);
        w.WriteString(description);
        WriteState(ref w, state);
    }

    #endregion

    #region Wire helpers

    private static bool Reply(MethodContext context, string? signature, Body body)
    {
        var writer = context.CreateReplyWriter(signature!);
        try
        {
            body(ref writer);
            context.Reply(writer.CreateMessage());
        }
        finally
        {
            writer.Dispose();
        }

        return true;
    }

    private void Emit(string path, string iface, string member, string signature, Body body)
    {
        var bus = _bus;
        if (bus == null)
            return;

        var writer = bus.GetMessageWriter();
        try
        {
            writer.WriteSignalHeader(null, path, iface, member, signature);
            body(ref writer);
            bus.TrySendMessage(writer.CreateMessage());
        }
        finally
        {
            writer.Dispose();
        }
    }

    // org.a11y.atspi.Event.Object signals: kind, detail1, detail2, any_data, properties (siiva{sv})
    private void EmitObject(string path, string member, string kind, int detail1, int detail2, Body anyData) =>
        Emit(path, IEventObject, member, "siiva{sv}", (ref MessageWriter w) =>
        {
            w.WriteString(kind);
            w.WriteInt32(detail1);
            w.WriteInt32(detail2);
            anyData(ref w);
            WriteEmptyProperties(ref w);
        });

    private static void WriteNoData(ref MessageWriter w) => w.WriteVariantUInt32(0);

    private static void WriteEmptyProperties(ref MessageWriter w)
    {
        var dictionary = w.WriteDictionaryStart();
        w.WriteDictionaryEnd(dictionary);
    }

    private static void WriteRef(ref MessageWriter w, string name, string path)
    {
        w.WriteStructureStart();
        w.WriteString(name);
        w.WriteObjectPath(path);
    }

    private static void WriteRefVariant(ref MessageWriter w, string name, string path)
    {
        w.WriteSignature("(so)");
        WriteRef(ref w, name, path);
    }

    // a state set is two uint32: bits 0-31, then bits 32-63
    private static void WriteState(ref MessageWriter w, ulong state)
    {
        var array = w.WriteArrayStart(DBusType.UInt32);
        w.WriteUInt32((uint)state);
        w.WriteUInt32((uint)(state >> 32));
        w.WriteArrayEnd(array);
    }

    #endregion

    public void Dispose()
    {
        _disposed = true;
        _manager.Changed -= OnSnapshotChanged;
        _manager.FocusChanged -= OnFocusChanged;
        _manager.LiveRegionUpdated -= OnLiveRegionUpdated;
        _manager.ReaderRefocusRequested -= OnReaderRefocus;
        _manager.RebuildSkipped -= OnRebuildSkipped;
        _refresh?.Dispose();
        _refresh = null;
        _statusWatch?.Dispose();
        _statusWatch = null;
        _bus?.Dispose();
        _bus = null;
    }
}
