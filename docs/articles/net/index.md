# DrawnUi.Net

`DrawnUi.Net` is the platform-agnostic DrawnUI target.

## Install

```bash
dotnet add package DrawnUi.Net
```

## When to use it

Use `DrawnUi.Net` when you need to:

- run on server or inside a console app
- generate images, PDFs etc from DrawnUI layouts and controls
- debug/test/benchmark engine/custom controls without platform host
- build harnesses for controls that mostly live in shared code

## When not to rely on it alone

Do not treat `DrawnUi.Net` as a full replacement for real-platform validation when behavior depends on native services such as:

- soft keyboard or IME behavior
- native focus handling
- OS text-editing quirks
- platform-specific input integration

In those cases, use `DrawnUi.Net` to isolate the shared logic first, then validate the same scenario on MAUI or Blazor.

## Current samples

The `src/Net/Samples/SkiaEditorHarness` sample demonstrates the headless-harness style. More to come.

## Related docs

- [Platforms and Packages](../platforms.md)
- [Blazor](../blazor/index.md)
- [Installation and Setup](../maui/getting-started.md)