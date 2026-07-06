using DrawnUi.Controls;
using DrawnUi.Draw;
using Microsoft.Maui.Devices;

namespace DrawnUiRecycleMeasureRepro;

// A recycled chat-style cell, modelled on a real shrink-wrapping, width-capped chat bubble.
//
// The bubble (a SkiaShape) is HorizontalOptions=Start with WidthRequest=-1 (auto / shrink-wrap to
// content) but capped at ~75% of the list width via MaximumWidthRequest. The text label inside is
// likewise capped at the bubble's inner content width. So:
//   - a LONG message measures wide, wraps several lines, and the bubble lands at the 75% cap.
//   - a SHORT message ("Ok") measures narrow and the bubble shrink-wraps tight around the word.
//
// SkiaDynamicDrawnCell.ApplyBindingContext drives SetContent(ctx) for both the first bind and every
// recycle. On recycle it wraps SetContent in LockUpdate(true)…LockUpdate(false), which SUPPRESSES the
// re-measure that the new content's property changes would otherwise trigger — so the recycled cell can
// retain the previous (donor) item's MEASURED SIZE (both width and height). In a grown, churned window
// this mis-sizing mispositions rows: rows overlap and a page of items collapses (the visible indices jump).
public sealed class MessageCell : SkiaDynamicDrawnCell
{
	private SkiaShape _bubble = null!;
	private SkiaLabel _label = null!;

	private const double BUBBLE_PADDING = 10;

	private static double _listWidthDip = -1;
	private static double ListWidthDip()
	{
		if (_listWidthDip < 0)
		{
			// Display width less a little horizontal page padding, in DIPs.
			_listWidthDip = (DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density) - 20;
		}
		return _listWidthDip;
	}

	// Bubbles shrink-wrap to content but are capped at 75% of the list width.
	private static double BubbleMaxWidth() => 0.75 * ListWidthDip();

	// The bubble's inner content width: the 75% cap less the bubble's own padding (each side).
	private static double BubbleContentWidth() => BubbleMaxWidth() - (BUBBLE_PADDING * 2);

	public MessageCell()
	{
		Padding = new Thickness(12, 5);

		HorizontalOptions = LayoutOptions.Fill;

		_label = new SkiaLabel
		{
			FontSize = 15,
			TextColor = Colors.White,
		};

		_bubble = new SkiaShape
		{
			Type = ShapeType.Rectangle,
			CornerRadius = 12,
			Padding = new Thickness(BUBBLE_PADDING),
			BackgroundColor = Color.FromArgb("#2A6FF0"),
			HorizontalOptions = LayoutOptions.Start,
			Children = new List<SkiaControl>
			{
				new SkiaLayout
				{
					Type = LayoutType.Column,
					Spacing = 3,
					HorizontalOptions = LayoutOptions.Start,
					Children = new List<SkiaControl> { _label },
				},
			},
		};

		Children = new List<SkiaControl>
        {
            _bubble
        };
	}

    // Called for the first bind AND every recycle (the base wraps the recycle path in LockUpdate).
	protected override void SetContent(object ctx)
	{
		base.SetContent(ctx);

		if (ctx is Row row)
		{
			// Auto (shrink-wrap) width, capped at 75% of the list width — exactly the chat-bubble cap.
			_bubble.WidthRequest = -1;
			//_bubble.MaximumWidthRequest = BubbleMaxWidth();

			_label.Text = row.Text;
			// Pin the label's width budget to the bubble's content width so a long message wraps at the
			// width it is arranged at (measure == arrange). A short message still shrink-wraps tighter.
			//_label.MaximumWidthRequest = BubbleContentWidth();
			_label.LineBreakMode = LineBreakMode.WordWrap;
		}
	}
}
