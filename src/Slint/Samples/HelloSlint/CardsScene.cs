using DrawnUi.Views;
using DrawnUi.Draw;
using HelloSlint;
using Color = DrawnUi.Color;

/// A button that talks to the Slint side, an editor, and a list of cards that light up on hover.
internal sealed class CardsScene
{
    private readonly MainWindow _window;
    private SkiaLabel _taps = null!;
    private int _count;

    public CardsScene(MainWindow window)
    {
        _window = window;
        Root = new SkiaStack
        {
            Spacing = 12,
            Children = new List<SkiaControl>
            {
                new SkiaLabel("DrawnUI inside a Slint window").WithFontSize(22).WithTextColor(Color.Parse("#263040")),
                new SkiaRow
                {
                    Spacing = 16,
                    Children = new List<SkiaControl>
                    {
                        new SkiaButton("Tap me") { Clicked = (me, args) => Tap() }.Assign(out Button),
                        new SkiaLabel("Taps: 0").WithFontSize(18).WithTextColor(Color.Parse("#263040")).CenterY().Assign(out _taps),
                        new SkiaShape
                        {
                            Type = ShapeType.Rectangle,
                            CornerRadius = 8,
                            WidthRequest = 36,
                            HeightRequest = 36,
                            BackgroundColor = Color.Parse("#4F8DFF"),
                        }.CenterY().AnimateRotation(0, 360, seconds: 2, repeat: -1),
                    },
                },
                new SkiaEditor
                {
                    UseCache = SkiaCacheType.None,
                    HeightRequest = 44,
                    BackgroundColor = Colors.White,
                    TextColor = Color.Parse("#263040"),
                    CursorColor = Color.Parse("#2563EB"),
                    PlaceholderText = "Type here…",
                    PlaceholderColor = Color.Parse("#8A94A6"),
                    FontSize = 16,
                    Padding = new Thickness(12, 10),
                }.FillX().OnTextChanged(text => _window.Status = $"Editor: {text}").Assign(out Editor),
                new SkiaScroll
                {
                    Content = new SkiaStack
                    {
                        Spacing = 8,
                        Children = Enumerable.Range(1, 60).Select(i => (SkiaControl)new SkiaShape
                        {
                            Type = ShapeType.Rectangle,
                            CornerRadius = 10,
                            HeightRequest = 56,
                            BackgroundColor = Colors.White,
                            Children = new List<SkiaControl>
                            {
                                new SkiaLabel($"Card {i}").WithFontSize(16).WithTextColor(Color.Parse("#263040")).CenterY().WithMargin(16, 0),
                            },
                        }.FillX().OnHovered((me, on) => me.BackgroundColor = on ? Color.Parse("#DCE7FF") : Colors.White)).ToList(),
                    },
                }.FillY().Assign(out Scroll),
            },
        }.WithPadding(16);
    }

    public SkiaControl Root { get; }
    public SkiaButton Button = null!;
    public SkiaEditor Editor = null!;
    public SkiaScroll Scroll = null!;

    public void Reset()
    {
        _count = 0;
        _taps.Text = "Taps: 0";
        _window.Status = "Reset from Slint";
    }

    private void Tap()
    {
        _count++;
        _taps.Text = $"Taps: {_count}";
        _window.Status = $"DrawnUI taps: {_count}";
    }
}
