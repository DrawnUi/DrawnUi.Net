using System.Windows;

namespace DrawnUi.Draw;

/// <summary>
/// Grid placement written the MAUI way in WPF XAML — <c>draw:SkiaLayout.Column="1"</c>,
/// <c>draw:SkiaLayout.RowSpan="2"</c>. On this head the storage is the Net <see cref="Grid"/>
/// attached BindableProperties, whose instance accessors on SkiaLayout cannot be XAML attachable
/// members; these DependencyProperties are the XAML face of that storage and forward every write to it.
/// <c>draw:Grid.Column</c> works as well, through Grid's own static accessors.
/// </summary>
public partial class SkiaLayout
{
    /// <summary>Attached <c>Column</c> for XAML.</summary>
    public static readonly DependencyProperty ColumnProperty = DependencyProperty.RegisterAttached(
        "Column", typeof(int), typeof(SkiaLayout),
        new FrameworkPropertyMetadata(0, (d, e) => { if (d is BindableObject b) Grid.SetColumn(b, (int)e.NewValue); }));

    /// <summary>Attached <c>Row</c> for XAML.</summary>
    public static readonly DependencyProperty RowProperty = DependencyProperty.RegisterAttached(
        "Row", typeof(int), typeof(SkiaLayout),
        new FrameworkPropertyMetadata(0, (d, e) => { if (d is BindableObject b) Grid.SetRow(b, (int)e.NewValue); }));

    /// <summary>Attached <c>ColumnSpan</c> for XAML.</summary>
    public static readonly DependencyProperty ColumnSpanProperty = DependencyProperty.RegisterAttached(
        "ColumnSpan", typeof(int), typeof(SkiaLayout),
        new FrameworkPropertyMetadata(1, (d, e) => { if (d is BindableObject b) Grid.SetColumnSpan(b, (int)e.NewValue); }));

    // XAML attachable-member lookup wants static Get/Set accessors next to the DependencyProperty;
    // the engine's own accessors are instance methods taking BindableObject, so these take
    // DependencyObject — a distinct overload the compiler still resolves the instance ones over.

    /// <summary>XAML accessor for <see cref="ColumnProperty"/>.</summary>
    public static int GetColumn(DependencyObject target) => (int)target.GetValue(ColumnProperty);

    /// <summary>XAML accessor for <see cref="ColumnProperty"/>.</summary>
    public static void SetColumn(DependencyObject target, int value) => target.SetValue(ColumnProperty, value);

    /// <summary>XAML accessor for <see cref="RowProperty"/>.</summary>
    public static int GetRow(DependencyObject target) => (int)target.GetValue(RowProperty);

    /// <summary>XAML accessor for <see cref="RowProperty"/>.</summary>
    public static void SetRow(DependencyObject target, int value) => target.SetValue(RowProperty, value);

    /// <summary>XAML accessor for <see cref="ColumnSpanProperty"/>.</summary>
    public static int GetColumnSpan(DependencyObject target) => (int)target.GetValue(ColumnSpanProperty);

    /// <summary>XAML accessor for <see cref="ColumnSpanProperty"/>.</summary>
    public static void SetColumnSpan(DependencyObject target, int value) => target.SetValue(ColumnSpanProperty, value);

    /// <summary>XAML accessor for <see cref="RowSpanProperty"/>.</summary>
    public static int GetRowSpan(DependencyObject target) => (int)target.GetValue(RowSpanProperty);

    /// <summary>XAML accessor for <see cref="RowSpanProperty"/>.</summary>
    public static void SetRowSpan(DependencyObject target, int value) => target.SetValue(RowSpanProperty, value);

    /// <summary>Attached <c>RowSpan</c> for XAML.</summary>
    public static readonly DependencyProperty RowSpanProperty = DependencyProperty.RegisterAttached(
        "RowSpan", typeof(int), typeof(SkiaLayout),
        new FrameworkPropertyMetadata(1, (d, e) => { if (d is BindableObject b) Grid.SetRowSpan(b, (int)e.NewValue); }));
}
