# DrawnUI Accessibility (contributor notes)

DrawnUI renders through SkiaSharp: the OS and the browser see one canvas surface, no native control tree. Every head mirrors the drawn controls into a virtual layer that assistive technology reads. Consumer documentation: [docs/articles/advanced/accessibility.md](docs/articles/advanced/accessibility.md).

## Where each head lives

| Head | Mechanism | Code | Checked with |
|---|---|---|---|
| Shared | `SkiaAccessibilityManager` snapshot, actions, keyboard rules | `src/Shared/DrawnUi/Views/SkiaAccessibilityManager.cs`, `ISkiaAccessibilityNode`, `SkiaControl.Shared.cs` (accessibility region), `DrawnView.HandleKeyboardNavigation` | `src/Net/Tests/DrawnUi.Net.Tests` (Accessibility*, Keyboard* tests) |
| MAUI Windows | WinUI automation peers on the canvas element | `src/Maui/DrawnUi/Platforms/Windows/Accessibility/MauiWindowsAutomationPeer.cs`, `DrawnView.Windows.cs` | UI Automation client |
| WPF | WPF automation peers | `src/Wpf/Drawnui.Wpf/Views/Accessibility/DrawnUiAutomationPeers.cs`, `DrawnUiElement.cs` | UI Automation client |
| OpenTK Windows | UIA fragment root on `WM_GETOBJECT` | `src/OpenTk/DrawnUi/Accessibility/WindowsUiaProvider.cs`, COM types in `src/Shared/DrawnUi/Platforms/Windows/WindowsUiaInterfaces.cs` | UI Automation client |
| OpenTK Linux | AT-SPI2 over D-Bus (`Tmds.DBus.Protocol`) | `src/OpenTk/DrawnUi/Accessibility/LinuxAtSpiProvider.cs` | pyatspi and Orca in WSL |
| OpenTK keyboard | Tab / Enter / Space / arrows / Escape | `src/OpenTk/DrawnUi/DrawnUiWindow.cs` (`OnKeyDown`) | posted keys on Windows |
| MAUI Android | AndroidX `ExploreByTouchHelper` | `src/Maui/DrawnUi/Platforms/Android/DrawnView.Accessibility.Android.cs` | uiautomator on the emulator, TalkBack on a phone |
| MAUI iOS / Mac Catalyst | `UIAccessibilityElement` container | `src/Maui/DrawnUi/Platforms/Apple/DrawnView.Accessibility.Apple.cs` | VoiceOver |
| MAUI Mac Catalyst keyboard | `HandleKeyboardNavigation` | `src/Maui/DrawnUi/Platforms/MacCatalyst/DrawnUiBasePageHandler.cs` | |
| Blazor | ARIA overlay | `src/Blazor/DrawnUi/Views/Canvas.razor` | Chrome DOM and accessibility tree |
| WebAssembly (`DrawnUi.Web`) | the same ARIA overlay, built in JS | `src/Wasm/DrawnUi/WebAccessibility.cs`, `src/Wasm/DrawnUi/wwwroot/drawnui-web.js` (accessibility overlay section) | Chrome DOM and accessibility tree |

## Contract every head follows

The same rules hold in DrawnUi.Rust and DrawnUi.React. When one engine changes them, the others follow.

- **Snapshot.** Nodes in reading order (rows top to bottom, a row left to right), rects in device-independent pixels where the control is drawn now. Rebuilt at most once per `MinUpdateIntervalMs` (1000 ms) at frame end, raising `Changed` only when something differs. `RebuildSkipped` tells a head that frames were drawn inside the interval; a head whose canvas can go idle (a page that just opened) calls `RefreshIfStale` after the interval from a timer.
- **Names said once.** A node whose text or heading child repeats its label gets no name (`NamedByChild`, `Label` null). Selectable text keeps its name.
- **Disabled control roles.** A node with a control role (`Aria.IsInteractiveRole`) that cannot take input reads as unavailable (UIA `IsEnabled` false, `aria-disabled`, TalkBack disabled, VoiceOver not enabled, AT-SPI no enabled / sensitive).
- **Range values.** `GetAccessibilityValue()` gives now / min / max / step / spoken text / orientation; the value is never put in the name. `OnAccessibilitySetValue(double)` sets it, snapped to the step.
- **Actions** (static on `SkiaAccessibilityManager`, all gated on what the pointer could do):
  - `Activate`: tap at the center;
  - `Adjust`: one arrow-key step, through `OnAccessibilityKey`;
  - `SetValue`;
  - `ScrollIntoView`: `SkiaScroll.EnsureVisible`;
  - `Page`: `SkiaScroll.AccessibilityPage`, false when nothing can move;
  - `Key`: arrows to the node, then its group.
- **Refresh after an action.** Activate / Adjust / SetValue (and a value control's key) rebuild the snapshot on the next frame.
- **Refocus.** Heads report the screen reader's node (`NotifyReaderFocused`). When a rebuild drops it, `ReaderRefocusRequested` carries the first node that says something; the head moves the reader there:
  - UIA and AT-SPI through `NotifyFocused`;
  - TalkBack through accessibility focus;
  - VoiceOver through LayoutChanged with the element (never ScreenChanged);
  - web through DOM focus.
- **Pressed state on the web.** The attribute follows the role (`Aria.PressedStateAttribute`): `aria-checked` for checkbox / switch / radio / menuitemcheckbox / menuitemradio, `aria-selected` for option / tab, `aria-pressed` otherwise.
- **Keyboard.**
  - One Tab stop per node; an arrow-key group (composite role) is one stop.
  - Arrows move by item index (the row length is counted on the first row).
  - Enter / Space activate; Escape leaves on desktop.
  - Tab leaves a drawn editor, never types a tab.
  - The ring shows only after keyboard use.
  - OpenTK sends Enter / Space / arrows to the focused node only while it has keyboard focus and no editor has the caret, so an app's own keys stay its own.

## Head notes

- **MAUI Windows.** `DrawnUi.Draw.IInvokeProvider` exists in the shared code, so a peer must name `Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider` in full (a using alias loses to the enclosing namespace). Shadowing it made Narrator's Invoke fail with E_NOINTERFACE.
- **UIA COM interfaces** in `WindowsUiaInterfaces.cs` carry the `Uia` prefix (`IUiaRangeValueProvider`, ...) so they never shadow the WinUI / WPF interfaces of the same name. GUIDs must match UIAutomationCore exactly (the old `IInvokeProvider` GUID was wrong).
- **UIA focus events** are not delivered for a window in the background; a refocus check needs the window in the foreground, or the headless tests.
- **Android.** Snapshot changes invalidate the root only while touch exploration is on. Scrolling actions sit on the canvas node (the virtual views are flat) and are offered only while the scroll around TalkBack's node can move.
- **iOS / Catalyst.** `accessibilityElements` is set through KVC on the platform view (the binding has no container interface on `UIView`); `accessibilityActivate` is an `[Export]`. Scroll directions follow AccessKit and Flutter: `Down` shows what is below. On iOS the elements exist only while VoiceOver or Switch Control runs.
- **Blazor.** Overlay elements take the pointer, so a control with a role gets no canvas hover.
- **WebAssembly.** Overlay elements have `pointer-events: none` and `overflow: clip` on the overlay (a focused node outside the canvas must never scroll it). The canvas draws the focus ring. C# callbacks reach the overlay through JS interop function marshaling, so apps' `main.js` needs no change.
- **Linux.**
  - Shaped after AccessKit's `accesskit_unix` / `accesskit_atspi_common`: same role numbers, state bits, events.
  - Waits for `org.a11y.Status` `IsEnabled` / `ScreenReaderEnabled`, and watches for the change. Honors `AT_SPI_BUS_ADDRESS`.
  - Test recipe (WSL with WSLg):
    1. Run inside `dbus-run-session`.
    2. Start `/usr/libexec/at-spi-bus-launcher --launch-immediately`.
    3. Run `orca --replace --debug-file=<file>` and read its `SPEECH OUTPUT:` lines.
    4. Afterwards, stop a leftover `speech-dispatcher` and remove `$XDG_RUNTIME_DIR/speech-dispatcher`.

## Remaining work

- [ ] Blazor: canvas hover on controls with a role (the overlay takes the pointer; WebAssembly avoids it with `pointer-events: none`).
- [ ] Web overlays: `aria-expanded`, `aria-describedby` (the hint is a `title`).
- [ ] OpenTK Windows UIA: live-region events (MAUI Windows and WPF raise them).
- [ ] Android and iOS: hardware keyboard navigation.
- [ ] AT-SPI: the Text interface (labels are read through their name), tree hierarchy (nodes are flat under the frame).
- [ ] Shell bar title and the page's own heading are sibling nodes, so the page name is read twice.
- [ ] XAML bindable properties for the accessibility props; `MinUpdateIntervalMs` as a `Canvas` parameter.
