using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;
using Canvas = DrawnUi.Views.Canvas;

namespace Sandbox;

public class MainPagePicker : BasePageReloadable, IDisposable
{
    private Canvas? _canvas;
    private readonly List<string> _habitats = ["Forest", "Wetlands", "Savanna", "Coast", "Highlands", "Tundra"];
    private readonly Dictionary<SkiaPicker, SkiaLabel> _selectionLabels = new();

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
        _selectionLabels.Clear();

        _canvas = new Canvas()
        {
            RenderingMode = RenderingModeType.Accelerated,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            BackgroundColor = new Color(0.95f, 0.93f, 0.89f, 1f),
            Gestures = GesturesMode.Lock,
            Content = new SkiaScroll()
            {
                HorizontalOptions = LayoutOptions.Fill,
                Content = BuildCanvasContent()
            }
        };

        Content = new Grid()
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                _canvas
            }
    };
    }

    private SkiaControl BuildCanvasContent()
    {
        return new SkiaLayout()
        {
            Margin = new Thickness(20),
            WidthRequest = 360,
            HeightRequest = 860,
            Padding = new Thickness(18),
            BackgroundColor = new Color(1f, 0.989f, 0.959f, 1f),
            Type = LayoutType.Column,
            Spacing = 14,
            Children =
            {
                new SkiaLabel()
                {
                    Text = "SkiaPicker Demo",
                    FontSize = 26,
                    FontWeight = FontWeights.Bold,
                    TextColor = new Color(0.208f, 0.169f, 0.118f, 1f),
                    HorizontalOptions = LayoutOptions.Fill
                },
                new SkiaLabel()
                {
                    Text = "This page isolates SkiaPicker and shows how DrawnUI ControlStyle changes the built-in field content before MAUI opens DisplayActionSheet.",
                    FontSize = 13,
                    TextColor = new Color(0.365f, 0.314f, 0.231f, 1f),
                    HorizontalOptions = LayoutOptions.Fill
                },
                CreatePickerCard("Platform", PrebuiltControlStyle.Platform, "Uses the current platform style defaults."),
                CreatePickerCard("Cupertino", PrebuiltControlStyle.Cupertino, "Rounded iOS-style field treatment."),
                CreatePickerCard("Material", PrebuiltControlStyle.Material, "Material-style field with denser spacing."),
                CreatePickerCard("Windows", PrebuiltControlStyle.Windows, "Squared field treatment for Windows-style visuals."),
#if DEBUG
                new SkiaLabelFps()
                {
                    Margin = new(0, 0, 4, 24),
                    VerticalOptions = LayoutOptions.End,
                    HorizontalOptions = LayoutOptions.End,
                    Rotation = -45,
                    BackgroundColor = Colors.DarkRed,
                    TextColor = Colors.White,
                    ZIndex = 110,
                }
#endif
            }
        };
    }

    private SkiaShape CreatePickerCard(string title, PrebuiltControlStyle style, string description)
    {
        var selectionLabel = new SkiaLabel()
        {
            Text = "Selected: None",
            FontSize = 16,
            TextColor = new Color(0.208f, 0.169f, 0.118f, 1f),
            HorizontalOptions = LayoutOptions.Fill,
            HorizontalTextAlignment = DrawTextAlignment.Center
        };

        var picker = new SkiaPicker()
        {
            Title = $"{title} habitat",
            Placeholder = "Choose a habitat",
            CancelText = "dismiss",
            ControlStyle = style,
            Items = _habitats,
            WidthRequest = 284,
            HeightRequest = 50,
            HorizontalOptions = LayoutOptions.Center,
            FillColor = Colors.White,
            StrokeColor = new Color(0.816f, 0.776f, 0.698f, 1f),
            TextColor = new Color(0.188f, 0.251f, 0.353f, 1f),
            PlaceholderColor = new Color(0.475f, 0.451f, 0.404f, 1f),
            ChevronColor = new Color(0.545f, 0.482f, 0.376f, 1f),
            FontSize = 15,
            PlaceholderFontSize = 15,
        };

        _selectionLabels[picker] = selectionLabel;
        picker.SelectedIndexChanged += OnSelectedIndexChanged;

        return new SkiaShape()
        {
            Type = ShapeType.Rectangle,
            CornerRadius = 18,
            BackgroundColor = new Color(0.976f, 0.957f, 0.922f, 1f),
            StrokeColor = new Color(0.851f, 0.827f, 0.776f, 1f),
            StrokeWidth = 1,
            HorizontalOptions = LayoutOptions.Fill,
            Padding = new Thickness(14, 16),
            Children =
            {
                new SkiaLayout()
                {
                    Type = LayoutType.Column,
                    Spacing = 8,
                    HorizontalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new SkiaLabel()
                        {
                            Text = title,
                            FontSize = 18,
                            FontWeight = FontWeights.Bold,
                            TextColor = new Color(0.208f, 0.169f, 0.118f, 1f),
                            HorizontalOptions = LayoutOptions.Fill
                        },
                        new SkiaLabel()
                        {
                            Text = description,
                            FontSize = 12,
                            TextColor = new Color(0.365f, 0.314f, 0.231f, 1f),
                            HorizontalOptions = LayoutOptions.Fill
                        },
                        picker,
                        selectionLabel
                    }
                }
            }
        };
    }

    private void OnSelectedIndexChanged(object? sender, int index)
    {
        if (sender is not SkiaPicker picker || !_selectionLabels.TryGetValue(picker, out var label))
        {
            return;
        }

        label.Text = index >= 0 && index < _habitats.Count
            ? $"Selected: {_habitats[index]}"
            : "Selected: None";
    }
}
