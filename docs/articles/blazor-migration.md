# Blazor Migration

This page is about adopting DrawnUI in an existing Blazor codebase without forcing the whole app into a new rendering model on day one.

## Migration Strategy

The safe approach is incremental:

1. keep the existing Blazor shell, routing, and layouts
2. introduce one DrawnUI island where custom rendering actually pays off
3. choose runtime based on the surface behavior, not on branding alone
4. expand only after the first slice proves its value

## If You Already Have A Blazor Server App

Start with `DrawnUi.Blazor.Server` when:

- the target surface is event-driven
- the page is mostly normal Razor/HTML with one or two drawn islands
- you want minimum architecture churn first

Move that specific surface to `DrawnUi.Blazor.Wasm` later if you discover it really needs local animation or richer input handling.

## If You Already Have A WASM App

Start with `DrawnUi.Blazor.Wasm` when:

- the target surface needs local responsiveness
- DrawnUI will own a large part of the interaction model
- you expect animation, gesture work, or frequent redraw

## If You Are Building A New Blazor Web App

Use a mixed architecture only when the app genuinely needs both runtime behaviors.

Do not choose hybrid just because it seems more flexible on paper. Mixed render modes add structure and deployment complexity. Use them when one surface clearly belongs on the server and another clearly belongs in the browser.

## Good First Candidates

- custom dashboards
- inspectors and property panels
- interactive cards and branded widgets
- drawing-heavy surfaces that are awkward in plain HTML/CSS
- areas where normal Blazor controls still make sense around the edges

## Poor First Candidates

- text-entry-heavy workflows if you expect browser-native editing behavior inside the DrawnUI area
- high-FPS scenes on the server path
- drag-heavy interactions on the server path
- app-wide migration before the first isolated surface is validated

## Practical Path

1. start with one page or one panel
2. validate the runtime fit with the matching sample pattern from this repo
3. keep surrounding app structure conventional
4. widen usage only after the interaction and performance model is proven

## Related Docs

- [Blazor](blazor.md)
- [Blazor Packages](blazor-packages.md)
- [Blazor Capabilities](blazor-capabilities.md)
- [Blazor FAQ](blazor-faq.md)
