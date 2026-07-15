using System.ComponentModel;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;
using Canvas = DrawnUi.Views.Canvas;

namespace Sandbox;

/// <summary>
/// Isolates SkiaWheelScroll (the wheel picker scroller) for manual testing after the planes removal.
/// SelectedIndex is two-way bound (fluent ObservePropertyTwoWay) to a tiny model; the label observes it.
/// IsLooped is toggled at runtime.
/// </summary>
public class MainPageWheel : BasePageReloadable, IDisposable
{
    private Canvas? _canvas;

    private readonly List<string> _items =
        Enumerable.Range(0, 30).Select(i => i.ToString()).ToList();

    private readonly Color _textColor = new(0.486f, 0.451f, 0.404f, 1f);

    private readonly WheelModel _model = new() { SelectedIndex = 5 };

    private SkiaWheelScroll _wheel = null!;
    private SkiaButton _loopButton = null!;

    private bool _looped = true;

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            Content = null;
            _canvas?.Dispose();
        }

        base.Dispose(isDisposing);
    }

    public override void Build()
    {
        _canvas?.Dispose();

        _canvas = new Canvas()
        {
            RenderingMode = RenderingModeType.Accelerated,
            Gestures = GesturesMode.Lock,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            BackgroundColor = new Color(0.95f, 0.93f, 0.89f, 1f),
            Content = new SkiaLayout()
            {
                Type = LayoutType.Column,
                Spacing = 16,
                Padding = new Thickness(24),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Children =
                {
                    new SkiaLabel()
                    {
                        Text = "SkiaWheelScroll test",
                        FontSize = 24,
                        FontWeight = FontWeights.Bold,
                        TextColor = new Color(0.208f, 0.169f, 0.118f, 1f),
                        HorizontalOptions = LayoutOptions.Center,
                    },

                    // label reacts to the model's SelectedIndex (updated via the two-way binding below)
                    new SkiaLabel()
                    {
                        Text = FormatIndex(_model.SelectedIndex),
                        FontSize = 18,
                        TextColor = new Color(0.157f, 0.278f, 0.420f, 1f),
                        HorizontalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = DrawTextAlignment.Center,
                    }
                    .ObserveProperty(_model, nameof(WheelModel.SelectedIndex),
                        me => me.Text = FormatIndex(_model.SelectedIndex)),

                    // wheel card
                    new SkiaShape()
                    {
                        Type = ShapeType.Rectangle,
                        CornerRadius = 18,
                        WidthRequest = 220,
                        HeightRequest = 240,
                        BackgroundColor = Colors.White,
                        StrokeColor = new Color(0.851f, 0.827f, 0.776f, 1f),
                        StrokeWidth = 1,
                        HorizontalOptions = LayoutOptions.Center,
                        Children =
                        {
                            new SkiaWheelScroll()
                            {
                                LinesColor = new Color(0.788f, 0.808f, 0.843f, 1f),
                                IsLooped = _looped,
                                VisibleItemCount = 5,
                                FadeStrength = 1.5f,
                                Margin = new Thickness(0, 10, 0, 10),
                                Bounces = true,
                                FrictionScrolled = 0.8f,
                                Orientation = ScrollOrientation.Vertical,
                                SnapToChildren = SnapToChildrenType.Center,
                                TrackIndexPosition = RelativePositionType.Center,
                                HorizontalOptions = LayoutOptions.Fill,
                                VerticalOptions = LayoutOptions.Fill,
                                Content = new SkiaWheelStack()
                                {
                                    Spacing = 1,
                                    WidthRequest = -1,
                                    BackgroundColor = Colors.Transparent,
                                    HorizontalOptions = LayoutOptions.Fill,
                                    VerticalOptions = LayoutOptions.Fill,
                                    ItemsSource = _items,
                                    ItemTemplate = new DataTemplate(() => new SkiaWheelPickerCell(_textColor)),
                                },
                            }
                            .Assign(out _wheel)
                            .ObservePropertyTwoWay(_model,
                                nameof(WheelModel.SelectedIndex), me => me.SelectedIndex = _model.SelectedIndex,
                                nameof(SkiaWheelScroll.SelectedIndex), (model, me) => model.SelectedIndex = me.SelectedIndex),
                        }
                    },

                    new SkiaButton()
                    {
                        Text = LoopButtonText(),
                        WidthRequest = 220,
                        HeightRequest = 50,
                        HorizontalOptions = LayoutOptions.Center,
                    }
                    .Assign(out _loopButton)
                    .OnTapped(me => ToggleLooped()),

#if DEBUG
                    new SkiaLabelFps()
                    {
                        Margin = new Thickness(0, 0, 4, 24),
                        VerticalOptions = LayoutOptions.End,
                        HorizontalOptions = LayoutOptions.End,
                        Rotation = -45,
                        BackgroundColor = Colors.DarkRed,
                        TextColor = Colors.White,
                        ZIndex = 110,
                    },
#endif
                }
            }
        };

        Content = new Grid()
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { _canvas }
        };
    }

    private string FormatIndex(int index) => $"Index: {index}  ({_items[index]})";

    private string LoopButtonText() => _looped ? "Looped: ON" : "Looped: OFF";

    private void ToggleLooped()
    {
        _looped = !_looped;
        _wheel.IsLooped = _looped;

        // Looped positions the wheel around LoopedOffset (~10000); non-looped clamps to 0..count.
        // Reset to the start on every toggle so switching looped->not doesn't leave us in the void.
        // Driving the wheel directly (not the model) forces ApplySelectedIndex even when the index is
        // unchanged; the two-way binding then syncs the model -> label. Nudge first so a 0->0 set still fires.
        if (_wheel.SelectedIndex == 0)
            _wheel.SelectedIndex = Math.Min(1, _items.Count - 1);
        _wheel.SelectedIndex = 0;

        _loopButton.Text = LoopButtonText();
    }

    private sealed class WheelModel : INotifyPropertyChanged
    {
        private int _selectedIndex;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex == value)
                    return;
                _selectedIndex = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedIndex)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
