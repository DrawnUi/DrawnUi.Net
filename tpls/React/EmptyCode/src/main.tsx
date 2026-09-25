import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { Aria, Canvas, Super } from "drawnui-react";
import { SkiaButton, SkiaLabel } from "drawnui-react/core";
import MainPage from "./MainPage";

// Same startup shape as DrawnUI for .NET: fonts and styles first, then the canvas.
await Super.UseDrawnUi()
  .ConfigureFonts((fonts) => fonts
    // Same aliases the other DrawnUI templates and the DrawnUI Fiddle use.
    .AddFont("fonts/OpenSans-Regular.ttf", "FontText")
    .AddFont("fonts/OpenSans-Semibold.ttf", "FontTextTitle")
    // Symbol and emoji subsets (public/fonts): a browser canvas has no system fonts to fall back on.
    .AddSymbols()
    .AddEmojis())
  // Labels and button captions without a FontFamily draw in FontText.
  .ConfigureStyles((styles) => styles
    .AddStyle({ TargetType: SkiaLabel, ApplyToDerivedTypes: true, Setters: { FontFamily: "FontText" } })
    .AddStyle({ TargetType: SkiaButton, ApplyToDerivedTypes: true, Setters: { FontFamily: "FontText" } }))
  .BuildAsync();

// Screen readers: every label is read as text and every button is a button.
SkiaLabel.DefaultAccessibilityRole = Aria.RoleText;
SkiaButton.DefaultAccessibilityRole = Aria.RoleButton;

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <Canvas BackgroundColor="#0B0E14" RenderingMode="Accelerated" Gestures="Enabled" style={{ height: "100%" }}>
      <MainPage />
    </Canvas>
  </StrictMode>,
);
