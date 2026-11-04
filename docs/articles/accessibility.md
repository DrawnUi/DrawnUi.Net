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

- **Ready-made controls are accessible:** buttons, switches, checkboxes, radio buttons, sliders, progress bars, the text editor, lists and dialogs carry their role, state and value.
- **Names said once:** a card whose title already says its name is read once, not twice.
- **Values:** a slider reads as "Volume, 65%", and a screen reader can step it up and down or set it.
- **Keyboard (desktop and browser):** Tab and Shift+Tab follow the reading order. A list, toolbar, menu or radio group is one Tab stop that you walk with the arrow keys, Home, End, PageUp and PageDown, also through the rows of a long recycled list. Enter and Space press, and the focus ring shows only when you use the keyboard.
- **Dialogs:** while a dialog is open, the screen reader and Tab stay inside it. It is announced by its name, and the keyboard goes back where it was when the dialog closes.
- **Menus and popups:** a menu opened from the keyboard takes the keyboard, and Escape closes it.
- **Live regions:** a status text marked as live is read as soon as it changes.
- **Scrolling:** the screen reader brings any control into view, and on iOS and Android VoiceOver and TalkBack page a scroll with their scroll gestures.
- **Selectable text:** a label can let people select and copy its text.

## What your app does

1. Name every control that has no text of its own by what it controls: "Wi-Fi" on a switch, "Volume" on a slider, never the role ("Switch" is read anyway).
2. Give a container a role when it is a group a screen reader should know (a list, a toolbar, a radio group, a menu): the arrow keys then move inside it.
3. Mark a status text as live to have it read when it changes.
4. Mark a drawn dialog as modal, so readers and the keyboard stay in it. Shell popups and modals do this by themselves.
5. Give a custom control a role (a tappable card is a button) and, for a custom range control, its value.
6. Listen to your app once with the platform's screen reader.

The property names for each framework:

| | .NET | React | Rust |
|---|---|---|---|
| Name | `AccessibilityLabel` | `AccessibilityLabel` | `.accessibility_label()` |
| Role | `AccessibilityRole = Aria.RoleButton` | `AccessibilityRole={Aria.RoleButton}` | `.accessibility_role(Aria::BUTTON)` |
| Live text | `AccessibilityLive = "polite"` | `AccessibilityLive="polite"` | `.accessibility_live("polite")` |
| Dialog | `AccessibilityModal = true` | `AccessibilityModal={true}` | `.accessibility_modal(true)` |
| Hidden from readers | `Aria.RolePresentation` | `Aria.RolePresentation` | `Aria::PRESENTATION` |

## Not covered yet

The system text size, high contrast and reduced motion settings are not applied by DrawnUI yet.

## Details per framework

- DrawnUI for .NET: [Accessibility](advanced/accessibility.md), every head and how it maps to the platform.
- DrawnUI for React: [DrawnUI for React](react/index.md), and the [Accessibility page](https://helloreact.drawnui.net/#/a11y) of its demo.
- DrawnUI for Rust: [Accessibility](rust/index.md#accessibility), and the [Accessibility page](https://hellorust.drawnui.net/#/a11y) of its demo.
