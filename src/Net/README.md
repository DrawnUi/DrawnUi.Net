# DrawnUi.Net

`DrawnUi.Net` is the platform-agnostic DrawnUI target used for developing, debugging, and validating shared DrawnUI rendering logic without a MAUI host.

## Folder layout

- `DrawnUi/`
  - Contains the `DrawnUi.Net.csproj` project.
  - Uses the shared DrawnUI sources plus `.Net`-specific shims.
- `Samples/`
  - Contains headless or test-oriented sample projects built on top of `DrawnUi.Net`.
  - Current sample: `SkiaEditorHarness`.

## When to use DrawnUi.Net

Use `DrawnUi.Net` when you need to:

- use DrawnUI layouts on server to generate images/PDF etc
- debug shared drawing/layout logic without a platform UI stack
- validate text layout, selection, and cursor rendering offscreen
- build small repro harnesses for controls that mostly live in shared code
- test rendering behavior with predictable, non-native input sequences

## When not to rely on it alone

Do not treat `DrawnUi.Net` as a full replacement for real-platform validation when behavior depends on native services such as:

- soft keyboard / IME behavior
- native selection updates
- platform focus handling
- OS text editing quirks

In those cases, use `DrawnUi.Net` to isolate shared logic first, then confirm behavior on Blazor, or .NET MAUI Android, Apple, or Windows.

## Samples

`Samples/SkiaEditorHarness` is a headless, non-platform drawing harness for `SkiaEditor`.

Allows us to:

- render editor states offscreen into PNGs
- script typing, selection, and caret movement
- capture text/cursor summaries per step
- debug visual editor behavior without launching a MAUI app

Useful for virtual drawing scenarios where we want fast iteration on shared logic before involving native platform controls.

## Typical .NET control dev workflow

1. Reproduce shared behavior in `SkiaEditorHarness`.
2. Fix shared DrawnUI logic in the main source tree.
3. Re-run the harness to confirm the visual/state change.
4. Validate the same scenario on a real platform if native text input is involved.