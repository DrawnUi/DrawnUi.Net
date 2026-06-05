using System.Collections.ObjectModel;
using DrawnUi.Draw;

namespace DrawnUiRecycleMeasureRepro;

// One bound row. Items alternate VERY long text (wraps several lines, hits the 75% width cap) and
// VERY short text ("Ok", "Hi", "K", "No") so that recycling churns a short message onto a cell whose
// last (donor) content was a long, wide bubble — and vice versa.
public sealed class Row
{
	public int Index { get; set; }
	public string Text { get; set; } = string.Empty;
}

public partial class MainPageRepro : ContentPage
{
	private static readonly string[] ShortTexts = { "Ok", "Hi", "K", "No", "Yep", "Sure" };

	private const string LongText =
		"This is a deliberately long message that wraps across several lines so the bubble grows to the " +
		"75% maximum width cap. When this wide cell is recycled for a short message the recycled cell keeps " +
		"this wide measured width instead of shrink-wrapping to the new short text.";

	private const int INITIAL = 30;   // start small
	private const int PAGE = 30;      // append a page each time we near the end
	private const int MAX = 400;      // grow up to ~400 (grow-only, like a chat LoadOlder)

	public ObservableCollection<Row> Items { get; } = new();

	public string Status => $"Items: {Items.Count}  —  scroll DOWN; watch for short bubbles rendered wide / clipped";

	public MainPageRepro()
	{
		InitializeComponent();

		for (int i = 0; i < INITIAL; i++)
		{
			Items.Add(MakeRow(i));
		}

		BindingContext = this;

		// Grow the list as the user scrolls toward the end (simulating a chat's grow-only paging).
		Scroll.Scrolled += OnScrolled;
	}

	private static Row MakeRow(int i)
	{
		// Even = long (wide, multi-line). Odd = short (tight shrink-wrap).
		string text = (i % 2 == 0)
			? $"[{i}] {LongText}"
			: $"[{i}] {ShortTexts[i % ShortTexts.Length]}";
		return new Row { Index = i, Text = text };
	}

	private void OnScrolled(object? sender, ScaledPoint e)
	{
		if (Items.Count >= MAX)
		{
			return;
		}

		// Append another page once scrolled within ~2 viewport-heights of the bottom.
		double viewport = Scroll.Height;
		double contentHeight = Scroll.ContentSize.Units.Height;
		double offsetY = -e.Units.Y; // SkiaScroll offset is negative as you scroll down

		if (contentHeight - (offsetY + viewport) < viewport * 2)
		{
			int start = Items.Count;
			int end = Math.Min(start + PAGE, MAX);
			for (int i = start; i < end; i++)
			{
				Items.Add(MakeRow(i));
			}
			OnPropertyChanged(nameof(Status));
		}
	}
}
