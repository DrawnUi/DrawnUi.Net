# Tooltips and Menus

Desktop apps expect two things from every control under the mouse: a short hint when the mouse rests on it, and a
menu where it is right-clicked. DrawnUI draws both: **tooltips** on any control, and **popup windows** that hold a
context menu, a dropdown or the menus of a menu bar. They work on every head; on Windows (WPF, MAUI Windows, OpenTK)
they open in windows of their own, so they can go past the edge of the app window.

The Hello apps show all of it on their **Tooltips & Menus** screen.

## Tooltips

Give any control a `Tooltip`:

```csharp
new SkiaSvg { SvgString = saveIcon, WidthRequest = 24, HeightRequest = 24 }
    .WithTooltip("Save")
    .OnTapped(me => Save());
```

- It shows after the mouse rests on the control for `ShowDelayMs` (600 ms). Moving straight to the next control with a
  tooltip shows that one at once.
- It is attached to its control: centered 6 pt above it, below it where there is no room. It stays in place while the
  mouse moves over the control.
- It closes when the mouse leaves the control, on a press, the wheel, any key, when content scrolls under the pointer,
  and when the app window loses the focus. After a press it stays closed until the mouse moves to another control.
- Touch never shows tooltips. A tooltip does not make a control take hover (`IsHovered`).

### One text for tooltips and screen readers

Icon buttons already carry their name for screen readers. Turn on `Super.TooltipsFromAccessibility` and a control
without a `Tooltip` shows its `AccessibilityHint`, or, when it shows no text of its own (an icon, an image), its
`AccessibilityLabel`:

```csharp
Super.TooltipsFromAccessibility = true;

new SkiaShape { AccessibilityLabel = "Settings", WidthRequest = 40, HeightRequest = 40 } // shows "Settings"
    .WithChildren(new SkiaSvg { SvgString = gearIcon });
```

A control that shows its own text (a button with a caption, a label) gets no tooltip from its accessibility label.

### Look, timing and position

`TooltipStyle` sets them for the whole app (`Super.TooltipStyle`) or for one canvas (`TooltipStyle` on the canvas,
which wins over the app's). A change applies from the next tooltip shown.

```csharp
Super.TooltipStyle = new TooltipStyle
{
    BackgroundColor = Color.Parse("#262B3B"),
    BorderColor = Color.Parse("#3B4256"),
    TextColor = Colors.White,
    AtCursor = true,   // at the mouse, as the Windows and macOS tooltips
    OffsetY = 4,
};
```

| Property | Default | |
|---|---|---|
| `BackgroundColor` | #2B2D31 | fill of the box |
| `TextColor` | #F2F3F5 | |
| `BorderColor` | white at 14% | 1 pt border, null or transparent for none |
| `ShadowColor` | black at 35% | soft shadow, null or transparent for none |
| `FontFamily`, `FontSize` | default font, 13 | |
| `CornerRadius` | 6 | |
| `Padding` | 12, 8 | |
| `MaxWidth` | 320 | longer text wraps |
| `ShowDelayMs` | 600 | how long the mouse rests before it shows |
| `FollowWindowMs` | 500 | the next tooltip shows at once within this time after one closed |
| `AtCursor` | false | false: attached to the control; true: below and to the right of the pointer where it rested, above it where there is no room |
| `OffsetX`, `OffsetY` | 0, 0 | moves it right / down from where it would go, in points; on the flipped side the vertical offset moves it the other way |
| `CreateTooltip` | null | builds your own content for a text |

### Where tooltips show

| Head | Tooltips | Past the window edge |
|---|---|---|
| WPF | Yes | Yes, in a popup window |
| MAUI Windows | Yes | Yes, in a popup window |
| OpenTK on Windows | Yes | Yes, in a popup window |
| MAUI Mac Catalyst | Yes | Not yet: drawn inside the canvas |
| OpenTK on Linux and macOS | Yes | Not yet: drawn inside the canvas |
| Blazor, WebAssembly | Yes | No: drawn inside the canvas |
| MAUI Android (with a mouse) | Yes | No: drawn inside the canvas |
| MAUI iOS | No | No |

## Popup windows

A popup window shows any content over everything, opening from a point or from a control the way native menus do.
It is the same API as DrawnUI for Rust's `open_popup_window`.

```csharp
PopupId menu = canvas.OpenPopupWindow(content, anchor, options);
canvas.ClosePopupWindow(menu);
canvas.ClosePopupWindows();
canvas.IsPopupWindowOpen(menu);
```

- **Anchor** (`PopupAnchor`): a point in canvas points (where a right click was): the popup's top-left corner is
  there; or a control or a rect (a dropdown's button): the popup opens below it, left edges together. Where there is
  no room it flips to the other side and stays inside. A control anchor follows the control where it is now, through
  scrolling and caches.
- **Where it shows**: on WPF, MAUI Windows and OpenTK on Windows a popup opens in a window of its own, so it can go
  past the edge of the app window and stays inside the screen. On the other heads it is drawn inside the canvas and
  stays inside it.
- **Options** (`PopupWindowOptions`): `Size` (else the size its content measures to), `CloseOnPointerLeave`,
  `OnCanvas` (drawn inside the canvas on every head), `Cpu`, `Passive` (shows only, as a tooltip: takes no input).
- **It closes** with a press outside it (that press goes nowhere, as with native menus), Escape, the wheel outside it,
  the app window losing the focus, and from code. `PopupWindowClosed` tells which one closed, whatever closed it.
- **Input**: over the popup its controls take the mouse; nothing below it sees that input.

### Context menu

Right-click anywhere on a page (`OnContextMenu`, raised by the desktop and browser heads), then open the menu at the
point:

```csharp
page.OnContextMenu((me, e) =>
{
    _menu = me.Superview.OpenPopupWindow(Menu("Cut", "Copy", "Paste"), e.Location);
    return true; // taken: the browser's own menu does not show
});
```

A menu is any content. The Hello apps use a rounded panel of items, each highlighted under the mouse and closing the
menu when picked:

```csharp
SkiaControl Menu(params string[] items) => new SkiaShape
{
    CornerRadius = 8,
    BackgroundColor = Color.Parse("#262B3B"),
    StrokeColor = Color.Parse("#3B4256"),
    StrokeWidth = 1.5,
    WidthRequest = 200,
    Padding = new Thickness(4),
    AccessibilityRole = Aria.RoleMenu,
    Children = new List<SkiaControl>
    {
        new SkiaStack
        {
            Spacing = 0,
            Children = items.Select(text => (SkiaControl)new SkiaShape
            {
                CornerRadius = 6,
                HorizontalOptions = LayoutOptions.Fill,
                Padding = new Thickness(12, 7),
                AccessibilityRole = Aria.RoleMenuItem,
                Children = new List<SkiaControl> { new SkiaLabel(text) { TextColor = Colors.White } },
            }
            .OnHovered((me, on) => me.BackgroundColor = on ? Color.Parse("#343B52") : Colors.Transparent)
            .OnTapped(me => { Pick(text); me.Superview?.ClosePopupWindow(_menu); })).ToList(),
        },
    },
};
```

### Dropdown

Anchor the popup to the button: it opens below it, or above it where there is no room.

```csharp
new SkiaButton("Choose a fruit")
    .OnTapped(me => _menu = me.Superview.OpenPopupWindow(Menu("Apple", "Banana", "Cherry"), me));
```

### Menu bar

A desktop menu bar is a row of titles (`Aria.RoleMenuBar`), each opening its menu below it with
`OpenPopupWindow(menu, title)`. The Hello apps' menu bar shows the usual desktop behavior built from these pieces:

- A click opens a menu; while one is open, the mouse over another title opens that one.
- Alt with the underlined letter opens a menu (Alt+F for File), its first line ready for the keyboard; Left and Right
  open the next menu, Up and Down move through it, Enter picks, Escape closes. The keys come from
  `KeyboardManager.KeyDown`, the modifiers from `KeyboardManager.IsAltPressed` / `IsControlPressed`.
- Each line shows its shortcut (Ctrl+S, F1); the shortcuts work with the menus closed too.
- The open title is highlighted, and goes back to normal when its menu closes for any reason (`PopupWindowClosed`).

The full code is the `TooltipsMenusPage` of the HelloMaui sample.
