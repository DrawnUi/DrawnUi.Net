# DrawnUi.Net

`DrawnUi.Net` is the platform-agnostic DrawnUI target used for developing, debugging, and validating shared DrawnUI rendering logic without a framework-specific host, like .NET MAUI, Blazor etc.

## Folder layout

- `DrawnUi/`
  - Contains the `DrawnUi.Net.csproj` project.
  - Uses the shared DrawnUI sources plus `.Net`-specific shims.
- `Samples/`
  - Contains headless or test-oriented sample projects built on top of `DrawnUi.Net`.

## When to use DrawnUi.Net

Use `DrawnUi.Net` when you need to:

- use DrawnUI layouts on server to generate images/PDF etc
- debug shared drawing/layout logic without a platform UI stack
- validate text layout, selection, and cursor rendering offscreen
- build small repro harnesses for controls that mostly live in shared code
- test rendering behavior with predictable, non-native input sequences

### Harness development workflow

1. Reproduce shared behavior in existing sample `SkiaEditorHarness`.
2. Fix shared DrawnUI logic in the main source tree.
3. Re-run the harness to confirm the visual/state change.
4. Validate the same scenario on a real platform if native text input is involved.

Useful for virtual drawing scenarios where we want fast (AI) iteration on shared logic before involving native platform controls.

In those cases, use `DrawnUi.Net` to isolate shared logic first, then confirm behavior on Blazor, or .NET MAUI Android, Apple, or Windows.

### When not to rely on it alone

Do not treat `DrawnUi.Net` as a full replacement for real-platform validation when behavior depends on native services such as:

- soft keyboard / IME behavior
- native selection updates
- platform focus handling
- OS text editing quirks

## Samples

`Samples/SkiaEditorHarness` is a headless, non-platform drawing harness for `SkiaEditor`.

Allows us to:

- render editor states offscreen into PNGs
- script typing, selection, and caret movement
- capture text/cursor summaries per step
- debug visual editor behavior without launching a MAUI app

## Harness commands

Harness commands are now split into two layers:

- generic harness commands
  - resolved by the harness runtime against the current tagged control
- control-specific commands
  - resolved by an adapter for the current target control type

The current sample is still `SkiaEditorHarness`, but its runtime is no longer hard-wired to one `SkiaEditor` instance.

### Current tagged controls

The sample scene currently exposes:

- `editor`
  - the `SkiaEditor` under test
- `status`
  - a plain `SkiaLabel` for generic property-targeting checks
- `scene`
  - the root layout

### Generic commands

These commands are handled directly by the harness runtime:

- `target:<tag>`
  - selects which tagged control receives later commands
- `setprop:<property>=<value>`
  - sets a public writable property on the current target
- `focus`
- `blur`
- `render`
  - explicit render step marker
- `snapshot[:name]`
  - explicit snapshot step marker

`setprop` currently supports:

- `string`
- `bool`
- `int`
- `float`
- `double`
- enums
- `Thickness`
- color-like types with a static `Parse(string)` method

### SkiaEditor

`SkiaEditorHarness` accepts a sequence of command arguments and executes them in order.

These commands require the current target to be a `SkiaEditor`:

- `markdown:true|false`
  - switches `SkiaEditor.UseMarkdown`
- `richtext:true|false`
  - compatibility alias for `markdown:true|false`
- `settext:<text>`
  - replaces editor text and moves caret to the end
- `type:<text>`
  - inserts text at the current selection/caret
- `enter`
  - inserts a newline in multiline mode
- `backspace[:count]`
  - deletes backward
- `delete[:count]`
  - deletes forward
- `left[:count]`
- `right[:count]`
- `shiftleft[:count]`
- `shiftright[:count]`
- `select:start,length`
- `movelc:line,column`
  - moves caret to line/column
- `shiftmovelc:line,column`
  - extends selection to line/column
- `selectlc:startLine,startColumn,endLine,endColumn`
- `selectall`

Escapes supported inside text payloads:

- `\\n`
- `\\r`
- `\\t`

Example:

```powershell
dotnet run --project .\src\Net\Samples\SkiaEditorHarness\SkiaEditorHarness.csproj --configuration Debug -- `
  "target:status" `
  "setprop:Text=Harness is about to switch to the editor" `
  "target:editor" `
  "markdown:true" `
  "settext:# Title\n\n- item 1\n- item 2\n\n**bold** and _italic_" `
  "movelc:2,3" `
  "shiftmovelc:3,6"
```

The harness writes PNG snapshots plus text summaries for every step. The summary now includes the current target, discovered tagged controls, current target type, and editor-specific state for each tagged `SkiaEditor`.





