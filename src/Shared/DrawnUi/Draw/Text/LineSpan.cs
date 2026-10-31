namespace DrawnUi.Draw;

public struct LineSpan
{
    public LineSpan()
    {
        Size = SKSize.Empty;
    }

    public string Text { get; set; }

    public TextSpan Span { get; set; }

    public LineGlyph[] Glyphs { get; set; }

    public bool NeedsShaping { get; set; }

    /// <summary>
    /// A run of a right-to-left level made only of neutral characters (brackets, punctuation): shaped right to left
    /// on purpose, so brackets mirror, since shaping would guess left to right from such text.
    /// </summary>
    public bool ShapeRightToLeft { get; set; }

    public SKSize Size { get; set; }

    //public SKRect DrawingRect { get; set; }

    public static LineSpan Default
    {
        get
        {
            return new LineSpan();
        }
    }
}
