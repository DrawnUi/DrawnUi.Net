# Tutorials

DrawnUI tutorials currently span two different host models:

- **.NET MAUI tutorials** use `DrawnUi.Maui` and usually show `Canvas` inside MAUI XAML pages or MAUI code-behind pages.
- **Blazor tutorials and samples** use `DrawnUi.Blazor.*`, initialize DrawnUI with `UseDrawnUiAsync(...)`, and host drawn content inside a Razor `<Canvas ... />` component.

If you are choosing a host first, start with [Platforms and Packages](platforms.md).

## MAUI tutorial lane

For the full MAUI-first tutorial index, see [MAUI Tutorials](maui/tutorials.md).

## Blazor-compatible tutorial references

These topics also have matching or near-matching Blazor sandbox pages in `src/Blazor/Samples/BlazorSandbox`:

- [Your First DrawnUI App: C# Fluent Syntax](first-app-code.md) - Blazor sample route: `tutorial-first-app`
- [A Custom Drawn Control](interactive-button.md) - Blazor sample route: `tutorial-custom-button`
- [Interactive Cards](interactive-cards.md) - Blazor sample route: `cards`
- [News Feed Scroller](news-feed-tutorial.md) - Blazor sample route: `tutorial-news-feed`

## Read host notes first

Some tutorial articles are intentionally MAUI/XAML-only. Others describe shared DrawnUI concepts but need small host-specific adjustments for Blazor.

Before following a tutorial literally:

- If the article shows `ContentPage`, `MauiProgram.cs`, or `xmlns:draw="http://schemas.appomobi.com/drawnUi/2023/draw"`, read it as a MAUI tutorial.
- If you are building the same concept in Blazor, keep the drawn control tree but switch startup to `UseDrawnUiAsync(...)` and host it inside a Razor `<Canvas ... />` component.
- For current browser-side examples, use the Blazor sandbox pages as the executable reference implementation.

