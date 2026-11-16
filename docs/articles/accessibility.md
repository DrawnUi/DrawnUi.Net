# Accessibility in DrawnUI

DrawnUI draws every control itself, on a Skia canvas. A drawn UI still has to work for people who use a screen reader or a keyboard, so every DrawnUI framework keeps a description of what is on screen and hands it to the platform's own accessibility system. Screen readers read your drawn controls, and Tab, Enter and the arrow keys move through them like through native ones.

The model is the same in DrawnUI for .NET, React and Rust: the same properties on a control, the same keyboard rules, the same behavior of dialogs and lists. Accessibility code moves with your UI when you port it from one framework to another.

## Where it works

| Platform | Screen reader | DrawnUI for .NET | DrawnUI for React | DrawnUI for Rust |
|---|---|---|---|---|
| Windows | Narrator, NVDA | MAUI, WPF, OpenTK | in the browser | yes |
| macOS | VoiceOver | MAUI Mac Catalyst | in the browser | yes |
| iOS, iPadOS | VoiceOver | MAUI | in the browser | yes |
| Android | TalkBack | MAUI | in the browser | yes |
| Linux | Orca | OpenTK | in the browser | yes |
| Browser | any browser screen reader | Blazor, WebAssembly | yes | yes |

Each framework talks to the platform API: UI Automation on Windows, the macOS and iOS accessibility APIs for VoiceOver, the Android accessibility node tree for TalkBack, AT-SPI on Linux. In the browser, an invisible layer of ARIA elements sits over the canvas.

Every platform in the table has been tested by ear with its screen reader, except Linux, where Orca's output was checked from its speech log.

## What you get

- **Ready-made controls are accessible:** switches, checkboxes, radio buttons, sliders, progress bars and the text editor carry their role, state and value. Buttons and labels are read once you give them a role, one line at startup for all of them, so a button used only as decoration stays silent: you decide what a screen reader sees.
- **Names said once:** a card whose title already says its name is read once, not twice.
- **Values:** a slider reads as "Volume, 65%", and a screen reader can step it up and down or set it.
- **Keyboard (desktop and browser):** Tab and Shift+Tab follow the reading order and reach every control of a scrolling page, also the ones out of view. A list, toolbar, menu or radio group is one Tab stop that you walk with the arrow keys, Home, End, PageUp and PageDown, also through the rows of a long recycled list. Enter and Space press, and the focus ring shows only when you use the keyboard.
- **Dialogs:** while a dialog is open, the screen reader and Tab stay inside it. It is announced by its name, and the keyboard goes back where it was when the dialog closes.
- **Menus and popups:** a menu opened from the keyboard takes the keyboard, and Escape closes it. A right click, or a long press on a phone or tablet, opens the app's context menu.
- **Live regions:** a status text marked as live is read as soon as it changes.
- **Scrolling:** the screen reader brings any control into view, and on iOS and Android VoiceOver and TalkBack page a scroll with their scroll gestures.
- **Selectable text:** people can select and copy the text of a label, across several labels at once (see [Text people can select and copy](#text-people-can-select-and-copy)).

## What your app does

1. Name every control that has no text of its own by what it controls: "Wi-Fi" on a switch, "Volume" on a slider, never the role ("Switch" is read anyway).
2. Give a container a role when it is a group a screen reader should know (a list, a toolbar, a radio group, a menu): the arrow keys then move inside it.
3. Mark a status text as live to have it read when it changes.
4. Mark a drawn dialog as modal, so readers and the keyboard stay in it. Shell popups and modals do this by themselves.
5. Give buttons and labels a role (once at startup for all, or per control), and a custom control too: a tappable card is a button. A custom range control also gives its value.
6. Make text people may want to copy selectable: titles, descriptions, addresses, prices, error messages.
7. Listen to your app once with the platform's screen reader.

The property names for each framework:

| | .NET | React | Rust |
|---|---|---|---|
| Name | `AccessibilityLabel` | `AccessibilityLabel` | `.accessibility_label()` |
| Role | `AccessibilityRole = Aria.RoleButton` | `AccessibilityRole={Aria.RoleButton}` | `.accessibility_role(Aria::BUTTON)` |
| Live text | `AccessibilityLive = "polite"` | `AccessibilityLive="polite"` | `.accessibility_live("polite")` |
| Dialog | `AccessibilityModal = true` | `AccessibilityModal={true}` | `.accessibility_modal(true)` |
| Selectable text | `AccessibilityTextSelectable = true` | `AccessibilityTextSelectable={true}` | `.accessibility_text_selectable(true)` |
| Hidden from readers | `Aria.RolePresentation` | `Aria.RolePresentation` | `Aria::PRESENTATION` |

## Text people can select and copy

A label with `AccessibilityTextSelectable` lets people select its text and copy it, with the mouse, touch and the keyboard:

- **Mouse:** drag over the text, or double click a word. A drag can start beside or above the text and run across several selectable labels, like on a web page.
- **Touch:** a long press selects a word, then drag to extend it. A Copy button appears next to the selection.
- **Keyboard:** Ctrl+C (Cmd+C on Mac) copies. Text selected across several labels is copied one label per line.
- **From code:** the canvas holds the page's selection: `SelectedText`, `CopyTextSelection()` and `ClearTextSelection()`. A label has its own `Select`, `SelectAll`, `CopySelection` and `ClearSelection`.

Copying goes through `Super.SetClipboardText`, which every head fills. A right click, or a long press, on selected text keeps the selection, so your context menu can offer Copy for it.

```csharp
new SkiaLabel("Order #10457 · ships in 2 days")
{
    AccessibilityTextSelectable = true,
}
```

See [Selectable text](controls/text.md#selectable-text) for the look (`SkiaLabel.TextSelectionColor`) and the details.

## Web

A web page is expected to behave like a web page. People select a sentence to quote it, copy an address, copy a link's address from its menu, open a link in a new tab, and move through the page with Tab. A drawn page draws all of its text itself, so the browser's own selection and link menus do not reach it: DrawnUI draws these behaviors instead, and you turn them on where they matter.

**Text acts like web text.** Make the text people read selectable (titles, paragraphs, prices, addresses). One drag selects across several labels and Ctrl+C copies them, as on any page.

**Links act like web links.** Give a link the link role and its address as the hint. A left click or a tap opens it in a new browser tab with `Super.OpenLink(url)`; a right click never opens it, it opens the link's menu instead. That menu, drawn with `OpenPopupWindow`, offers Copy Link, and Copy when text is selected:

```csharp
new SkiaLabel("DrawnUI on GitHub")
{
    AccessibilityTextSelectable = true,
    AccessibilityRole = Aria.RoleLink,
    AccessibilityHint = url,                    // screen readers read the address
}
.OnTapped((me, e) =>
{
    // the main button opens the link; a right click is for its menu
    if (e.Parameters?.Event?.Pointer is not { Button: not MouseButton.Left })
        Super.OpenLink(url);
})
.OnContextMenu((me, e) =>
{
    // your drawn menu: "Copy" (me.Superview.CopyTextSelection()),
    // "Copy Link" (Super.SetClipboardText?.Invoke(url)), "Cancel"
    me.Superview.OpenPopupWindow(LinkMenu(me, url), e.Location);
    return true;
});
```

**Screen readers and the keyboard.** The invisible ARIA layer over the canvas gives every named control its role and name, so browser screen readers read the page, and Tab, Enter, Space and the arrow keys work through it. A link reads as a link with its address.

**Hover.** On a desktop browser, give links and buttons a hover look (`OnHovered`), as web pages do.

The DrawnUI 3D gallery uses this pattern on every title, description, price and link: [add3d.drawnui.net](https://add3d.drawnui.net).

## Display scale and text size

DrawnUI follows the system display scale. When people set 125% or 150% scaling on Windows, a larger display size on their phone, or zoom in the browser, the whole canvas draws larger: text, controls and images together, sharp at any scale. For many people who need bigger text, this is the setting they already use, and your app follows it with no work.

The separate text-size setting (larger fonts only), high contrast and reduced motion are not applied by DrawnUI yet.

## Details per framework

- DrawnUI for .NET: [Accessibility](advanced/accessibility.md), every head and how it maps to the platform.
- DrawnUI for React: [DrawnUI for React](react/index.md), and the [Accessibility page](https://helloreact.drawnui.net/#/a11y) of its demo.
- DrawnUI for Rust: [Accessibility](rust/index.md#accessibility), and the [Accessibility page](https://hellorust.drawnui.net/#/a11y) of its demo.
