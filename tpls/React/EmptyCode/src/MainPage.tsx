// <fiddle:content>
// This whole file is the UI: one component, default-exported, drawn inside the <Canvas> that
// src/main.tsx owns. It is the same contract as a DrawnUI Fiddle TSX snippet, so a snippet
// pastes in whole, and the Fiddle React export replaces this file with it.
import { useState } from "react";
import { SkiaButton, SkiaLabel, SkiaStack, SkiaSvg, Thickness } from "drawnui-react";

// Object props are created once: a new Thickness on every render would remeasure the stack each time.
const PADDING = new Thickness(24);

/**
 * Your UI starts here. Replace the body with your own tree.
 * Everything below is drawn on one canvas; the stack is cached as one image and redrawn only when it changes.
 */
export default function MainPage() {
  const [taps, setTaps] = useState(0);

  return (
    <SkiaStack UseCache="Image" Spacing={16} Padding={PADDING} HorizontalOptions="Center" VerticalOptions="Center">
      <SkiaSvg Source="drawnui.svg" TintColor="#4C8DFF" WidthRequest={64} HeightRequest={64} HorizontalOptions="Center" />

      <SkiaLabel Text="DrawnUI" FontFamily="FontTextTitle" FontSize={28} TextColor="#FFFFFF" HorizontalOptions="Center" />

      <SkiaLabel Text="Everything here is drawn on one canvas." FontSize={14} TextColor="#A9B4C6"
        HorizontalTextAlignment="Center" HorizontalOptions="Center" />

      <SkiaLabel Text={taps === 0 ? "Nothing tapped yet" : taps === 1 ? "Tapped once" : `Tapped ${taps} times`}
        FontSize={14} TextColor="#4C8DFF" HorizontalOptions="Center" />

      <SkiaButton Text="Tap me" UseCache="Image" BackgroundColor="#4C8DFF" TextColor="#FFFFFF" CornerRadius={10}
        WidthRequest={200} HeightRequest={44} HorizontalOptions="Center"
        Tapped={async (me) => {
          setTaps((t) => t + 1);
          await me.ScaleToAsync(0.96, 0.96, 60);
          await me.ScaleToAsync(1, 1, 60);
        }} />
    </SkiaStack>
  );
}
// </fiddle:content>
