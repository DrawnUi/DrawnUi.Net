namespace DrawnUi.Draw;

/// <summary>
/// How <see cref="SkiaLabel.AutoSize"/> changes the font size so the text fits or fills the label's box.
/// Fit modes only shrink below FontSize, Fill modes only grow above it, FitFill modes do both.
/// The box must have a size on that axis (a request, or a constraint from the parent).
/// </summary>
public enum AutoSizeType
{
    /// <summary>
    /// The font size is FontSize.
    /// </summary>
    None,

    /// <summary>
    /// Shrinks the font so every paragraph fits the width on one line, and grows it to fill the width.
    /// Starts from the size used last time, so it is faster than FitHorizontal or FillHorizontal for text
    /// that changes often. The size can end up above FontSize.
    /// </summary>
    FitFillHorizontal,

    /// <summary>
    /// Shrinks the font until the wrapped text is no longer cut by the height or MaxLines, and grows it
    /// while there is room for another line. Starts from the size used last time, so it is faster than
    /// FitVertical or FillVertical for text that changes often. The size can end up above FontSize.
    /// </summary>
    FitFillVertical,

    /// <summary>
    /// Shrinks the font until every paragraph fits the width on one line, nothing is cut by the height or
    /// MaxLines, and no line is wider than the label (NoWrap included). Never goes above FontSize: a text
    /// that fits keeps FontSize, and the size comes back up when the text gets shorter or the label wider.
    /// For text that changes often think about FitFillHorizontal instead.
    /// </summary>
    FitHorizontal,

    /// <summary>
    /// Grows the font from FontSize while the widest line still has room in the width. Never shrinks below
    /// FontSize. For text that changes often think about FitFillHorizontal instead.
    /// </summary>
    FillHorizontal,

    /// <summary>
    /// Shrinks the font until the wrapped text is no longer cut by the height or MaxLines. Never goes above
    /// FontSize. For text that changes often think about FitFillVertical instead.
    /// </summary>
    FitVertical,

    /// <summary>
    /// Grows the font from FontSize while there is room for another line below the text. Never shrinks below
    /// FontSize. For text that changes often think about FitFillVertical instead.
    /// </summary>
    FillVertical,

}
