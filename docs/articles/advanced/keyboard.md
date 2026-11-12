# On-screen Keyboard over a Form

On phones (MAUI, iOS and Android) the on-screen keyboard covers the bottom of the screen. A DrawnUI form keeps the focused field visible in three steps: the page reports the keyboard height, a spacer makes room at the end of the scrolling content, and the `SkiaScroll` brings the focused field just above the keyboard.

## What you get

- The focused field's bottom lands `AdaptToKeyboardGap` points above the keyboard's top (8 by default).
- A field that is already visible above the keyboard does not move the page.
- When the keyboard closes, the page stays where it is.
- Moving to the next field while the keyboard stays open scrolls that field into view too.

On iOS, MAUI's own keyboard scrolling (`KeyboardAutoManagerScroll`) is switched off under DrawnUI, so the `SkiaScroll` is the only thing that moves the form.

## 1. The page reports the keyboard height

Derive the page from `DrawnUiBasePage` (a `SkiaShell` page already is one). It sets `KeyboardSize` to the keyboard height in points whenever the keyboard opens, resizes or closes, and you can adjust that value by overriding `OnKeyboardResized`.

Your canvas content usually ends above the bottom inset. On Android, `KeyboardSize` already leaves that inset out. On iOS it is the keyboard's full height, down to the bottom of the screen, so subtract the inset (the home indicator) there:

```csharp
public override double OnKeyboardResized(double size)
{
#if IOS
    size -= Super.Screen.BottomInset;
#endif
    return Math.Max(0, size);
}
```

## 2. A spacer at the end of the scrolling content

Add an empty control as the last child of the scrolling content, with the keyboard height. It lets the last fields scroll high enough to sit above the keyboard. Keep it hidden while there is no keyboard, so a stack's `Spacing` adds nothing.

## 3. The scroll follows the focused field

Set two properties on the form's `SkiaScroll`:

- `AdaptToKeyboardFor`: the focused control (`Canvas.FocusedChild`).
- `AdaptToKeyboardSize`: the keyboard height from step 1.

`AdaptToKeyboardGap` sets the space between the field and the keyboard, in points (8 by default).

Set them when the keyboard height changes and again when the focus moves to another field (`Canvas.FocusedItemChanged`), because moving between fields does not change the keyboard height.

## Example

```csharp
public class FormPage : DrawnUiBasePage
{
    Canvas Canvas;
    SkiaScroll _scroll;

    public FormPage()
    {
        Content = Canvas = new Canvas
        {
            Gestures = GesturesMode.Enabled,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaScroll
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                AdaptToKeyboardGap = 8, // the default
                Content = new SkiaStack
                {
                    Spacing = 16,
                    Padding = 16,
                    Children =
                    {
                        // ... your fields (SkiaEditor, SkiaMauiEntry, buttons)

                        // spacer: room for the keyboard
                        new SkiaControl { HorizontalOptions = LayoutOptions.Fill, IsVisible = false }
                            .ObserveProperty(this, nameof(KeyboardSize), me =>
                            {
                                me.HeightRequest = KeyboardSize;
                                me.IsVisible = KeyboardSize > 0;
                            })
                    }
                }
            }
            .Assign(out _scroll)
            .ObserveProperty(this, nameof(KeyboardSize), me => FollowFocus())
        };

        Canvas.FocusedItemChanged += (s, e) => FollowFocus();
    }

    public override double OnKeyboardResized(double size)
    {
#if IOS
        size -= Super.Screen.BottomInset;
#endif
        return Math.Max(0, size);
    }

    void FollowFocus()
    {
        _scroll.AdaptToKeyboardFor = Canvas.FocusedChild as SkiaControl;
        _scroll.AdaptToKeyboardSize = KeyboardSize;
    }
}
```

A complete form using this recipe: `src/Maui/Samples/FastRepro/MainPageEditors.cs` in the DrawnUi repository.
