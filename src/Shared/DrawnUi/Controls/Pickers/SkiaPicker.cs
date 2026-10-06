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

    /// <summary>
    /// Height of the floating label line (Material 3 body small); the outline starts at half of it.
    /// </summary>
    private const double FloatingLabelHeight = 16.0;

    private OutlineFrame? _frame;
    private SkiaLabel? _displayLabel;
    private SkiaShape? _chevron;
    private SkiaLayout? _contentRow;
    private SkiaLabel? _floatingLabel;
    private bool _isSynchronizingSelection;
    private bool _isOpen;

    public SkiaPicker()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Start;
    }

    protected override void CreateDefaultContent()
    {
        var style = GetStyleMetrics();

        SetStyleDefault(HeightRequestProperty, style.Height);

        if (Views.Count == 0)
        {
            BuildDefaultContent(style);
        }

        ApplyVisualState();
        UpdateDisplayText();
    }

    /// <summary>
    /// Forgets the children built for the previous <see cref="SkiaControl.ControlStyle"/>, the next measure builds them again.
    /// </summary>
    public override void RebuildDefaultContent()
    {
        _frame = null;
        _displayLabel = null;
        _chevron = null;
        _contentRow = null;
        _floatingLabel = null;
        base.RebuildDefaultContent();
    }

    private void BuildDefaultContent(PickerStyleMetrics style)
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

        var frame = new OutlineFrame()
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

        if (style.LabelFontSize > 0)
        {
            // outlined field: the placeholder floats onto the outline once a value is shown or the list is open
            frame.Margin = new Thickness(0, FloatingLabelHeight / 2, 0, 0);
            frame.NotchLabel = new SkiaLabel()
            {
                Tag = "PickerLabel",
                UseCache = SkiaCacheType.Operations,
                IsVisible = false,
                Margin = new Thickness(12, 0, 12, 0),
                Padding = new Thickness(4, 0),
                HeightRequest = FloatingLabelHeight,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                VerticalTextAlignment = TextAlignment.Center,
                MaxLines = 1,
                LineBreakMode = LineBreakMode.TailTruncation,
            }.Assign(out _floatingLabel);

            AddSubView(_floatingLabel);
        }
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

        int? result;
        SetOpenState(true);
        try
        {
            result = await ShowSelectionAsyncPlatform(title, CancelText, options, SelectedIndex);
        }
        finally
        {
            SetOpenState(false);
        }

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

    private void SetOpenState(bool open)
    {
        if (_isOpen != open)
        {
            _isOpen = open;
            UpdateDisplayText();
        }
    }

    /// <summary>
    /// The placeholder sits on the outline instead of inside the field (looks with a floating label only).
    /// </summary>
    private bool IsLabelFloating => _floatingLabel != null
                                    && !string.IsNullOrEmpty(Placeholder)
                                    && (SelectedItem != null || _isOpen);

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

        // only looks that define a focused state react to the open list
        var focused = _isOpen && style.FocusedStrokeColor != null;

        _frame.Padding = style.Padding;
        _frame.BackgroundColor = ResolveColor(FillColor, BaseFillColor, style.FillColor);
        _frame.StrokeColor = focused
            ? style.FocusedStrokeColor!
            : ResolveColor(StrokeColor, BaseStrokeColor, style.StrokeColor);
        _frame.StrokeWidth = ResolveFloat(StrokeWidth, BaseStrokeWidth,
            focused ? style.FocusedStrokeWidth : style.StrokeWidth);
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
        _chevron.Rotation = focused ? 180 : 0; // arrow up while the list is open

        if (_floatingLabel != null)
        {
            _floatingLabel.FontSize = style.LabelFontSize;
            _floatingLabel.TextColor = focused && style.FocusedLabelColor != null
                ? style.FocusedLabelColor!
                : ResolveColor(PlaceholderColor, BasePlaceholderColor, style.PlaceholderColor);

            var floating = IsLabelFloating;
            if (_floatingLabel.IsVisible != floating || _floatingLabel.Text != Placeholder)
            {
                _floatingLabel.Text = Placeholder;
                _floatingLabel.IsVisible = floating;

                // the outline is cached with the gap for the label: measure the label again before the
                // outline records, then record the outline again
                _frame.Update();
                Invalidate();
            }
        }
    }

    private void UpdateDisplayText()
    {
        if (_displayLabel == null)
        {
            return;
        }

        _displayLabel.Text = SelectedItem == null
            ? (IsLabelFloating ? string.Empty : Placeholder)
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
            // Material 3 outlined exposed dropdown, baseline light scheme (the palette of the other Material3 looks):
            // 56 tall plus room for the floating label, corner 4, outline 1 #79747E, 2 primary #6750A4 while open,
            // text body large 16 #1D1B20, label body small 12 #49454F, arrow_drop_down 10x5 in a 24 slot 12 from the end
            PrebuiltControlStyle.Material3 => new PickerStyleMetrics(
                new Thickness(16, 8, 19, 8),
                4f,
                1f,
                Colors.Transparent,
                Color.FromArgb("#79747E"),
                Color.FromArgb("#1D1B20"),
                Color.FromArgb("#49454F"),
                Color.FromArgb("#49454F"),
                16,
                16,
                10,
                7,
                16,
                null)
            {
                Height = FloatingLabelHeight / 2 + 56,
                FocusedStrokeColor = Color.FromArgb("#6750A4"),
                FocusedStrokeWidth = 2f,
                LabelFontSize = 12,
                FocusedLabelColor = Color.FromArgb("#6750A4"),
            },
            PrebuiltControlStyle.Windows => new PickerStyleMetrics(
                new Thickness(12, 8),
                4f,
                1.5f,
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
        List<SkiaShadow>? Shadows)
    {
        /// <summary>
        /// Height of the picker when the user set none.
        /// </summary>
        public double Height { get; init; } = 48;

        /// <summary>
        /// Outline color while the selection list is open; null when the look has no focused state.
        /// </summary>
        public Color? FocusedStrokeColor { get; init; }

        /// <summary>
        /// Outline width while the selection list is open.
        /// </summary>
        public float FocusedStrokeWidth { get; init; }

        /// <summary>
        /// Font size of the placeholder floating on the outline; 0 when the look has no floating label.
        /// </summary>
        public double LabelFontSize { get; init; }

        /// <summary>
        /// Floating label color while the selection list is open.
        /// </summary>
        public Color? FocusedLabelColor { get; init; }
    }

    /// <summary>
    /// The field frame. With a <see cref="NotchLabel"/> shown it leaves a gap in its top outline where the
    /// label sits (Material 3 outlined field), so the label reads over whatever is behind the picker.
    /// </summary>
    private sealed class OutlineFrame : SkiaShape
    {
        /// <summary>
        /// Floating label laid over the top outline: a sibling placed at the frame's left edge plus its margin.
        /// </summary>
        public SkiaLabel? NotchLabel { get; set; }

        /// <summary>
        /// Clips the label's span out of the top outline, then paints the shape as usual.
        /// </summary>
        protected override void Paint(DrawingContext ctx)
        {
            var label = NotchLabel;
            if (label == null || !label.IsVisible || label.MeasuredSize.Pixels.Width <= 0)
            {
                base.Paint(ctx);
                return;
            }

            var scale = ctx.Scale;
            var inset = (float)(label.Margins.Left * scale);
            var width = label.MeasuredSize.Pixels.Width - (float)(label.Margins.HorizontalThickness * scale); // measured includes margins
            var left = (float)Math.Round(DrawingRect.Left + inset);
            var right = (float)Math.Round(Math.Min(left + width, DrawingRect.Right - inset));
            var bottom = MeasuredStrokeAwareSize.Top + GetStrokePixels(scale) + 1;

            var canvas = ctx.Context.Canvas;
            var saved = canvas.Save();
            canvas.ClipRect(new SKRect(left, DrawingRect.Top - 1, right, bottom), SKClipOperation.Difference);
            base.Paint(ctx);
            canvas.RestoreToCount(saved);
        }
    }

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
