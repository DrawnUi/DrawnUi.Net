namespace DrawnUi.Infrastructure;

/// <summary>
/// Standard paper formats
/// </summary>
public enum PaperFormat
{
    Custom,
    A4,
    A5,
    A6,
    Letter,
    Legal
}


public struct PdfPagePosition
{
    /// <summary>
    /// Page index
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Offset of the page start inside the content, apply it as the viewport offset when rendering the page.
    /// </summary>
    public SKPoint Position { get; set; }

    /// <summary>
    /// Printable height of this page: the paper height, or less when the page ends above a row that
    /// did not fit, or when it is the last page and the content ends earlier.
    /// </summary>
    public float Height { get; set; }
}

public static class Pdf
{
    /// <summary>
    /// Gets the paper size in pixels for a given paper format and DPI.
    /// </summary>
    /// <param name="format">The paper format.</param>
    /// <param name="dpi">The dots per inch (DPI) value.</param>
    /// <returns>The paper size in pixels as an SKSize.</returns>
    public static SKSize GetPaperSizePixels(PaperFormat format, int dpi)
    {
        var paperSizeInInches = GetPaperSizeInInches(format);
        return GetPaperSizePixels(paperSizeInInches, dpi);
    }

    /// <summary>
    /// Gets the paper size in pixels for a custom paper size in inches and DPI.
    /// </summary>
    /// <param name="paperSizeInInches">The paper size in inches.</param>
    /// <param name="dpi">The dots per inch (DPI) value.</param>
    /// <returns>The paper size in pixels as an SKSize.</returns>
    public static SKSize GetPaperSizePixels(SKSize paperSizeInInches, int dpi)
    {
        float widthPixels = paperSizeInInches.Width * dpi;
        float heightPixels = paperSizeInInches.Height * dpi;
        return new SKSize(widthPixels, heightPixels);
    }

    /// <summary>
    /// Gets the paper size in pixels for a custom paper size in millimeters and DPI.
    /// </summary>
    /// <param name="paperSizeInMillimeters">The paper size in millimeters.</param>
    /// <param name="dpi">The dots per inch (DPI) value.</param>
    /// <returns>The paper size in pixels as an SKSize.</returns>
    public static SKSize GetPaperSizePixelsFromMillimeters(SKSize paperSizeInMillimeters, int dpi)
    {
        var paperSizeInInches = new SKSize(paperSizeInMillimeters.Width / 25.4f, paperSizeInMillimeters.Height / 25.4f);
        return GetPaperSizePixels(paperSizeInInches, dpi);
    }

    /// <summary>
    /// Splits content of any shape into fixed slices of the paper height, considering height only.
    /// A slice can cut through a row or a line of text; use <see cref="SplitStackToPages"/> to break between rows.
    /// </summary>
    /// <param name="content">The size of the content to be split, same units as paper.</param>
    /// <param name="paper">The size of the paper to split the content into.</param>
    /// <returns>Contiguous pages: Position is the content offset of the page, Height its printable height.</returns>
    public static List<PdfPagePosition> SplitToPages(SKSize content, SKSize paper)
    {
        var positions = new List<PdfPagePosition>();

        if (content.Height <= paper.Height)
        {
            positions.Add(new PdfPagePosition
            {
                Index = 0,
                Position = new SKPoint(0, 0),
                Height = content.Height
            });
            return positions;
        }

        for (float y = 0; y < content.Height; y += paper.Height)
        {
            positions.Add(new PdfPagePosition
            {
                Index = positions.Count,
                Position = new SKPoint(0, y),
                Height = Math.Min(paper.Height, content.Height - y)
            });
        }

        return positions;
    }

    /// <summary>
    /// Depth-first search for the first Column layout, templated or not as requested.
    /// </summary>
    static SkiaLayout FindVStack(SkiaControl control, bool needTemplated)
    {
        if (control is SkiaLayout maybe && maybe.Type == LayoutType.Column && (!needTemplated || maybe.IsTemplated))
        {
            return maybe;
        }

        foreach (SkiaControl child in control.Views)
        {
            var found = FindVStack(child, needTemplated);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Splits content into pages that break between the children of the first vertical stack found inside it,
    /// so a table row is never cut in two. A single child taller than a page is sliced at the paper height.
    /// Without a stack the content is split into fixed slices like <see cref="SplitToPages"/>.
    /// </summary>
    /// <remarks>
    /// Rows are read from the stack's render tree, so render <paramref name="control"/> once at unlimited height
    /// before calling (every row must have been drawn). Offsets are relative to the top of <paramref name="control"/>,
    /// ready to be applied as the viewport offset of a scroll that hosts it. Pixels throughout.
    /// </remarks>
    /// <param name="control">The content to paginate, the control whose offset you will set per page.</param>
    /// <param name="isTemplated">True to look for a templated stack (rows from ItemsSource), false for the first Column layout.</param>
    /// <param name="paper">Printable page size in pixels.</param>
    /// <param name="scale">Unused, kept for compatibility: render tree and paper are both in pixels.</param>
    /// <returns>Contiguous pages: Position is the content offset of the page, Height its printable height.</returns>
    public static List<PdfPagePosition> SplitStackToPages(SkiaControl control, bool isTemplated, SKSize paper, float scale = 1)
    {
        var vstack = FindVStack(control, isTemplated);

        // no stack, or a stack that was never rendered (no render tree yet): plain slices
        if (vstack?.RenderTree == null)
        {
            return SplitToPages(control.MeasuredSize.Pixels, paper);
        }

        var positions = new List<PdfPagePosition>();
        var pageHeight = paper.Height;
        var origin = control.DrawingRect.Top;
        var contentHeight = control.DrawingRect.Height;
        var offset = 0f;

        foreach (var cell in vstack.RenderTree)
        {
            var top = cell.Rect.Top - origin;
            var bottom = cell.Rect.Bottom - origin;

            // the row does not fit the current page: break right above it
            if (bottom > offset + pageHeight && top > offset)
            {
                positions.Add(new PdfPagePosition
                {
                    Index = positions.Count,
                    Position = new SKPoint(0, offset),
                    Height = top - offset
                });
                offset = top;
            }

            // a single row taller than a page can only be sliced
            while (bottom > offset + pageHeight)
            {
                positions.Add(new PdfPagePosition
                {
                    Index = positions.Count,
                    Position = new SKPoint(0, offset),
                    Height = pageHeight
                });
                offset += pageHeight;
            }
        }

        positions.Add(new PdfPagePosition
        {
            Index = positions.Count,
            Position = new SKPoint(0, offset),
            Height = Math.Min(pageHeight, Math.Max(0, contentHeight - offset))
        });

        return positions;
    }


    /// <summary>
    /// Calculates the paper size in pixels based on the paper size in inches and DPI.
    /// </summary>
    /// <param name="paperSizeInInches">The paper size in inches.</param>
    /// <param name="dpi">The dots per inch (DPI) value.</param>
    /// <returns>The paper size in pixels as an SKSize.</returns>
    public static SKSize GetPaperSizePixels(SKSize paperSizeInInches, float dpi)
    {
        float widthPixels = paperSizeInInches.Width * dpi;
        float heightPixels = paperSizeInInches.Height * dpi;
        return new SKSize(widthPixels, heightPixels);
    }

    /// <summary>
    /// Gets the paper size in inches for a given paper format.
    /// </summary>
    /// <param name="format">The paper format.</param>
    /// <returns>The paper size in inches as an SKSize.</returns>
    public static SKSize GetPaperSizeInInches(PaperFormat format)
    {
        switch (format)
        {
            case PaperFormat.A4:
                return new SKSize(8.27f, 11.69f);
            case PaperFormat.A5:
                return new SKSize(5.83f, 8.27f);
            case PaperFormat.A6:
                return new SKSize(4.13f, 5.83f);
            case PaperFormat.Letter:
                return new SKSize(8.5f, 11f);
            case PaperFormat.Legal:
                return new SKSize(8.5f, 14f);
            case PaperFormat.Custom:
                throw new ArgumentException("Custom size must be provided separately, use the SKSize overloads.", nameof(format));
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }
}

