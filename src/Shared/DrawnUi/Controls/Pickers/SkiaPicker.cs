using System.Collections;

namespace DrawnUi.Controls;

public partial class SkiaPicker : SkiaLayout
{
    private static readonly Color BaseFillColor = Colors.White;
    private static readonly Color BaseStrokeColor = Colors.Gray;
    private static readonly Color BaseTextColor = Colors.Black;
    private static readonly Color BasePlaceholderColor = Colors.Gray;
    private static readonly Color BaseChevronColor = Colors.Gray;
    private const float BaseStrokeWidth = 1.0f;
    private const float BaseCornerRadius = 12.0f;
    private const double BaseFontSize = 15.0;
    private const double BasePlaceholderFontSize = 14.0;

    private SkiaShape? _frame;
    private SkiaLabel? _displayLabel;
    private SkiaShape? _chevron;
    private SkiaLayout? _contentRow;
    private bool _isSynchronizingSelection;

    public SkiaPicker()
    {
        HeightRequest = 48;
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Start;
    }

    protected override void CreateDefaultContent()
    {
        if (Views.Count == 0)
        {
            BuildDefaultContent();
        }

        ApplyVisualState();
        UpdateDisplayText();
    }

    private void BuildDefaultContent()
    {
        var contentRow = new SkiaLayout()
        {
            Tag = "PickerRow",
            Type = LayoutType.Row,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            Spacing = 12,
            Children =
            {
                new SkiaLabel()
                {
                    Tag = "PickerText",
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalFillRatio = 1,
                    VerticalTextAlignment = TextAlignment.Center,
                    HorizontalTextAlignment = DrawTextAlignment.Start,
                }.Assign(out _displayLabel),
                new SkiaShape()
                {
                    Tag = "PickerChevron",
                    UseCache = SkiaCacheType.Operations,
                    Type = ShapeType.Polygon,
                    WidthRequest = 12,
                    HeightRequest = 8,
                    VerticalOptions = LayoutOptions.Center,
                    Points =
                    [
                        new SkiaPoint(0.0, 0.0),
                        new SkiaPoint(1.0, 0.0),
                        new SkiaPoint(0.5, 0.7),
                    ]
                }.Assign(out _chevron)
            }
        }.Assign(out _contentRow);

        var frame = new SkiaShape()
        {
            Tag = "PickerFrame",
            UseCache = SkiaCacheType.Operations,
            Type = ShapeType.Rectangle,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Padding = new Thickness(14, 10),
            Children =
            {
                contentRow
            }
        }
        .Assign(out _frame)
        .WithGestures((me, args, apply) =>
        {
            if (args.Type == TouchActionResult.Down)
            {
                SetPressedState(true);
                return me;
            }

            if (args.Type == TouchActionResult.Up)
            {
                SetPressedState(false);
                return me;
            }

            if (args.Type == TouchActionResult.Tapped)
            {
                SetPressedState(false);
                _ = OpenSelectionAsync();
                return me;
            }

            return me;
        });

        AddSubView(frame);
    }

    public event EventHandler<int>? SelectedIndexChanged;
    public event EventHandler<object?>? SelectedItemChanged;

    public async Task<bool> OpenSelectionAsync()
    {
        var options = GetOptions();
        if (options.Count == 0)
        {
            return false;
        }

        var title = string.IsNullOrWhiteSpace(Title) ? Placeholder : Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = "Select an item";
        }

        var result = await ShowSelectionAsyncPlatform(title, CancelText, options, SelectedIndex);
        if (result is >= 0 && result < options.Count)
        {
            SelectedIndex = result.Value;
            return true;
        }

        return false;
    }

#if DRAWNUI_NET
    private Task<int?> ShowSelectionAsyncPlatform(string title, string cancelText, IReadOnlyList<string> options, int selectedIndex)
    {
        return Task.FromResult<int?>(null);
    }
#else
    private partial Task<int?> ShowSelectionAsyncPlatform(string title, string cancelText, IReadOnlyList<string> options, int selectedIndex);
#endif

    private void SetPressedState(bool pressed)
    {
        if (_frame != null)
        {
            _frame.Opacity = pressed ? 0.9 : 1.0;
        }
    }

    private IReadOnlyList<string> GetOptions()
    {
        if (Items == null || Items.Count == 0)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>(Items.Count);
        foreach (var item in Items)
        {
            result.Add(GetItemText(item));
        }

        return result;
    }

    private void ApplyVisualState()
    {
        if (_frame == null || _displayLabel == null || _chevron == null || _contentRow == null)
        {
            return;
        }

        var style = GetStyleMetrics();

        _frame.Padding = style.Padding;
        _frame.BackgroundColor = ResolveColor(FillColor, BaseFillColor, style.FillColor);
        _frame.StrokeColor = ResolveColor(StrokeColor, BaseStrokeColor, style.StrokeColor);
        _frame.StrokeWidth = ResolveFloat(StrokeWidth, BaseStrokeWidth, style.StrokeWidth);
        _frame.CornerRadius = ResolveFloat(CornerRadius, BaseCornerRadius, style.CornerRadius);
        _frame.Shadows = style.Shadows;

        _contentRow.Spacing = style.ContentSpacing;

        _displayLabel.FontSize = SelectedItem == null
            ? ResolveDouble(PlaceholderFontSize, BasePlaceholderFontSize, style.PlaceholderFontSize)
            : ResolveDouble(FontSize, BaseFontSize, style.FontSize);
        _displayLabel.TextColor = SelectedItem == null
            ? ResolveColor(PlaceholderColor, BasePlaceholderColor, style.PlaceholderColor)
            : ResolveColor(TextColor, BaseTextColor, style.TextColor);

        _chevron.WidthRequest = style.ChevronWidth;
        _chevron.HeightRequest = style.ChevronHeight;
        _chevron.BackgroundColor = ResolveColor(ChevronColor, BaseChevronColor, style.ChevronColor);
    }

    private void UpdateDisplayText()
    {
        if (_displayLabel == null)
        {
            return;
        }

        _displayLabel.Text = SelectedItem == null
            ? Placeholder
            : GetItemText(SelectedItem);

        ApplyVisualState();
    }

    private PickerStyleMetrics GetStyleMetrics()
    {
        return UsingControlStyle switch
        {
            PrebuiltControlStyle.Cupertino => new PickerStyleMetrics(
                new Thickness(16, 11),
                14f,
                1f,
                Color.FromArgb("#FFFFFF"),
                Color.FromArgb("#D1D1D6"),
                Color.FromArgb("#1C1C1E"),
                Color.FromArgb("#8E8E93"),
                Color.FromArgb("#8E8E93"),
                17,
                16,
                10,
                6,
                10,
                [new SkiaShadow { X = 0, Y = 4, Blur = 12, Opacity = 0.12, Color = Colors.Black }]),
            PrebuiltControlStyle.Material => new PickerStyleMetrics(
                new Thickness(16, 12),
                8f,
                1.5f,
                Color.FromArgb("#FAFAFA"),
                Color.FromArgb("#5F6368"),
                Color.FromArgb("#202124"),
                Color.FromArgb("#5F6368"),
                Color.FromArgb("#1A73E8"),
                15,
                15,
                10,
                6,
                12,
                null),
            PrebuiltControlStyle.Windows => new PickerStyleMetrics(
                new Thickness(12, 8),
                4f,
                1.2f,
                Color.FromArgb("#FFFFFF"),
                Color.FromArgb("#8A8A8A"),
                Color.FromArgb("#111111"),
                Color.FromArgb("#666666"),
                Color.FromArgb("#3A3A3A"),
                14,
                14,
                10,
                6,
                10,
                null),
            _ => new PickerStyleMetrics(
                new Thickness(14, 10),
                12f,
                1f,
                BaseFillColor,
                BaseStrokeColor,
                BaseTextColor,
                BasePlaceholderColor,
                BaseChevronColor,
                BaseFontSize,
                BasePlaceholderFontSize,
                12,
                8,
                12,
                null),
        };
    }

    private static Color ResolveColor(Color actual, Color baseline, Color styled)
    {
        return actual == baseline ? styled : actual;
    }

    private static float ResolveFloat(float actual, float baseline, float styled)
    {
        return Math.Abs(actual - baseline) < 0.001f ? styled : actual;
    }

    private static double ResolveDouble(double actual, double baseline, double styled)
    {
        return Math.Abs(actual - baseline) < 0.001 ? styled : actual;
    }

    private sealed record PickerStyleMetrics(
        Thickness Padding,
        float CornerRadius,
        float StrokeWidth,
        Color FillColor,
        Color StrokeColor,
        Color TextColor,
        Color PlaceholderColor,
        Color ChevronColor,
        double FontSize,
        double PlaceholderFontSize,
        double ChevronWidth,
        double ChevronHeight,
        double ContentSpacing,
        List<SkiaShadow>? Shadows);

    private void SynchronizeSelectionFromIndex(bool raiseEvents)
    {
        if (_isSynchronizingSelection)
        {
            UpdateDisplayText();
            if (raiseEvents)
            {
                SelectedIndexChanged?.Invoke(this, SelectedIndex);
            }
            return;
        }

        _isSynchronizingSelection = true;
        try
        {
            object? item = null;
            if (Items != null && SelectedIndex >= 0 && SelectedIndex < Items.Count)
            {
                item = Items[SelectedIndex];
            }

            if (!Equals(SelectedItem, item))
            {
                SetValue(SelectedItemProperty, item);
            }
        }
        finally
        {
            _isSynchronizingSelection = false;
        }

        UpdateDisplayText();
        if (raiseEvents)
        {
            SelectedIndexChanged?.Invoke(this, SelectedIndex);
        }
    }

    private void SynchronizeSelectionFromItem(bool raiseEvents)
    {
        if (_isSynchronizingSelection)
        {
            UpdateDisplayText();
            if (raiseEvents)
            {
                SelectedItemChanged?.Invoke(this, SelectedItem);
            }
            return;
        }

        _isSynchronizingSelection = true;
        try
        {
            var index = FindIndexForItem(SelectedItem);
            if (SelectedIndex != index)
            {
                SetValue(SelectedIndexProperty, index);
            }
        }
        finally
        {
            _isSynchronizingSelection = false;
        }

        UpdateDisplayText();
        if (raiseEvents)
        {
            SelectedItemChanged?.Invoke(this, SelectedItem);
        }
    }

    private void SynchronizeSelectionFromItems()
    {
        if (Items == null || Items.Count == 0)
        {
            _isSynchronizingSelection = true;
            try
            {
                if (SelectedIndex != -1)
                {
                    SetValue(SelectedIndexProperty, -1);
                }

                if (SelectedItem != null)
                {
                    SetValue(SelectedItemProperty, null);
                }
            }
            finally
            {
                _isSynchronizingSelection = false;
            }

            UpdateDisplayText();
            return;
        }

        if (SelectedItem != null)
        {
            var indexFromItem = FindIndexForItem(SelectedItem);
            if (indexFromItem >= 0)
            {
                if (SelectedIndex != indexFromItem)
                {
                    SelectedIndex = indexFromItem;
                }

                UpdateDisplayText();
                return;
            }
        }

        if (SelectedIndex >= 0 && SelectedIndex < Items.Count)
        {
            SynchronizeSelectionFromIndex(false);
            return;
        }

        UpdateDisplayText();
    }

    private int FindIndexForItem(object? item)
    {
        if (item == null || Items == null)
        {
            return -1;
        }

        for (var index = 0; index < Items.Count; index++)
        {
            if (Equals(Items[index], item))
            {
                return index;
            }
        }

        return -1;
    }

    private static string GetItemText(object? item)
    {
        return item switch
        {
            null => string.Empty,
            string text => text,
            IHasStringTitle titled => titled.Title,
            _ => item.ToString() ?? string.Empty,
        };
    }

    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items),
        typeof(IList),
        typeof(SkiaPicker),
        null,
        propertyChanged: OnItemsChanged);

    public IList? Items
    {
        get => (IList?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
        nameof(SelectedIndex),
        typeof(int),
        typeof(SkiaPicker),
        -1,
        BindingMode.TwoWay,
        propertyChanged: OnSelectedIndexPropertyChanged);

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
        nameof(SelectedItem),
        typeof(object),
        typeof(SkiaPicker),
        null,
        BindingMode.TwoWay,
        propertyChanged: OnSelectedItemPropertyChanged);

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title),
        typeof(string),
        typeof(SkiaPicker),
        string.Empty);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder),
        typeof(string),
        typeof(SkiaPicker),
        "Select",
        propertyChanged: OnVisualPropertyChanged);

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public static readonly BindableProperty CancelTextProperty = BindableProperty.Create(
        nameof(CancelText),
        typeof(string),
        typeof(SkiaPicker),
        "Cancel");

    public string CancelText
    {
        get => (string)GetValue(CancelTextProperty);
        set => SetValue(CancelTextProperty, value);
    }

    public static readonly BindableProperty FillColorProperty = BindableProperty.Create(
        nameof(FillColor),
        typeof(Color),
        typeof(SkiaPicker),
        BaseFillColor,
        propertyChanged: OnVisualPropertyChanged);

    public Color FillColor
    {
        get => (Color)GetValue(FillColorProperty);
        set => SetValue(FillColorProperty, value);
    }

    public static readonly BindableProperty StrokeColorProperty = BindableProperty.Create(
        nameof(StrokeColor),
        typeof(Color),
        typeof(SkiaPicker),
        BaseStrokeColor,
        propertyChanged: OnVisualPropertyChanged);

    public Color StrokeColor
    {
        get => (Color)GetValue(StrokeColorProperty);
        set => SetValue(StrokeColorProperty, value);
    }

    public static readonly BindableProperty StrokeWidthProperty = BindableProperty.Create(
        nameof(StrokeWidth),
        typeof(float),
        typeof(SkiaPicker),
        BaseStrokeWidth,
        propertyChanged: OnVisualPropertyChanged);

    public float StrokeWidth
    {
        get => (float)GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius),
        typeof(float),
        typeof(SkiaPicker),
        BaseCornerRadius,
        propertyChanged: OnVisualPropertyChanged);

    public float CornerRadius
    {
        get => (float)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor),
        typeof(Color),
        typeof(SkiaPicker),
        BaseTextColor,
        propertyChanged: OnVisualPropertyChanged);

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public static readonly BindableProperty PlaceholderColorProperty = BindableProperty.Create(
        nameof(PlaceholderColor),
        typeof(Color),
        typeof(SkiaPicker),
        BasePlaceholderColor,
        propertyChanged: OnVisualPropertyChanged);

    public Color PlaceholderColor
    {
        get => (Color)GetValue(PlaceholderColorProperty);
        set => SetValue(PlaceholderColorProperty, value);
    }

    public static readonly BindableProperty ChevronColorProperty = BindableProperty.Create(
        nameof(ChevronColor),
        typeof(Color),
        typeof(SkiaPicker),
        BaseChevronColor,
        propertyChanged: OnVisualPropertyChanged);

    public Color ChevronColor
    {
        get => (Color)GetValue(ChevronColorProperty);
        set => SetValue(ChevronColorProperty, value);
    }

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize),
        typeof(double),
        typeof(SkiaPicker),
        BaseFontSize,
        propertyChanged: OnVisualPropertyChanged);

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public static readonly BindableProperty PlaceholderFontSizeProperty = BindableProperty.Create(
        nameof(PlaceholderFontSize),
        typeof(double),
        typeof(SkiaPicker),
        BasePlaceholderFontSize,
        propertyChanged: OnVisualPropertyChanged);

    public double PlaceholderFontSize
    {
        get => (double)GetValue(PlaceholderFontSizeProperty);
        set => SetValue(PlaceholderFontSizeProperty, value);
    }

    private static void OnItemsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaPicker control)
        {
            control.SynchronizeSelectionFromItems();
        }
    }

    private static void OnSelectedIndexPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaPicker control)
        {
            control.SynchronizeSelectionFromIndex(true);
        }
    }

    private static void OnSelectedItemPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaPicker control)
        {
            control.SynchronizeSelectionFromItem(true);
        }
    }

    private static void OnVisualPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaPicker control)
        {
            control.UpdateDisplayText();
        }
    }
}
