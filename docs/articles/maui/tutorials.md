# MAUI Tutorials

These tutorials are the .NET MAUI tutorial lane for DrawnUI.

They assume a MAUI app host, `DrawnUi.Maui`, and a DrawnUI `Canvas` inside MAUI pages. They are still useful for understanding shared DrawnUI concepts, but they are not the primary onboarding path for Blazor or `DrawnUi.Net`.

If you are on **Blazor**, stop here and switch lanes before opening one of the generic tutorial titles below:

- Start with [Blazor Overview](../blazor/index.md) for the correct package and startup model.
- Use the shared [Tutorial Host Guide](../tutorials.md) to see which of these concepts already have Blazor sandbox equivalents.
- Expect `UseDrawnUiAsync(...)` and Razor `<Canvas ... />` hosting in Blazor, not `MauiProgram.cs`, `ContentPage`, or MAUI XAML.

If you are choosing a host first, start with [Platforms and Packages](../platforms.md).

## Beginner tutorials

- **[Your First DrawnUI App (XAML)](../first-app.md)** - Complete setup from scratch, basic controls and layouts, simple animations, and MAUI deployment.
- **[Your First DrawnUI App (C# Fluent)](../first-app-code.md)** - The same app built with fluent C# syntax and DrawnUI observation patterns, with a Blazor host equivalent noted in the article.

## Intermediate tutorials

- **[Interactive Cards](../interactive-cards.md)** - Animated cards, gesture handling, effects, and reusable composition patterns. The article now points to the matching Blazor sandbox route.
- **[A Custom Drawn Control](../interactive-button.md)** - A custom game-style button with composed visuals and press feedback, plus a Blazor host note for the same control pattern.

## Advanced tutorial

- **[News Feed Scroller](../news-feed-tutorial.md)** - Data binding, recycled cells, virtualization, and large-data-list patterns, with a matching Blazor sandbox reference.

## Shared docs you should read with the tutorials

- [Installation and Setup](getting-started.md)
- [Startup Settings](../startup-settings.md)
- [Handling Gestures](../gestures.md)
- [Fluent Extensions](../fluent-extensions.md)