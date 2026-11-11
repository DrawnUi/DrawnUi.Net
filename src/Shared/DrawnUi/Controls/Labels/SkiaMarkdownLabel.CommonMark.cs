using System.ComponentModel.Design;
using CommonMark;
using CommonMark.Syntax;

namespace DrawnUi.Draw;

/// <summary>
/// Will internally create spans from markdown.
/// Spans property must not be set directly.
/// </summary>
public partial class SkiaRichLabel : SkiaLabel
{
    protected override void SetTextInternal()
    {
        if (IsDisposed || IsDisposing)
            return;

        ReplaceFont();

        lock (LockParsing)
        {
            try
            {
                base.SetTextInternal();

                // Markdown OFF: keep the rich (font-run/emoji) rendering but treat text as literal —
                // one plain span, no CommonMark parse. This is the "unicode/emoji, no formatting" mode.
                SourceIndexes = null;

                if (!MarkdownEnabled)
                {
                    Spans.Clear();
                    if (!string.IsNullOrEmpty(TextInternal))
                        AddTextSpan(TextInternal);
                    return;
                }

                if (!string.IsNullOrEmpty(TextInternal))
                {
                    var markdownDocument = CommonMarkConverter.Parse(TextInternal, MarkdownSettings);

                    Spans.Clear();
                    _sourceMap = new List<int>(TextInternal.Length);

                    isBold = false;
                    isItalic = false;
                    isHeading1 = false;
                    isHeading2 = false;
                    isHeading3 = false;
                    isCodeBlock = false;
                    hadParagraph = false;
                    isStrikethrough = false;

                    if (markdownDocument != null)
                    {
                        var block = markdownDocument.FirstChild;
                        while (block != null)
                        {
                            RenderBlock(block);
                            block = block.NextSibling;
                        }
                    }

                    // every drawn character got its source index, or the map is not trusted
                    var drawn = 0;
                    foreach (var span in Spans)
                        drawn += span.Text?.Length ?? 0;
                    if (drawn == _sourceMap.Count)
                        SourceIndexes = _sourceMap;
                    _sourceMap = null;
                }
                else
                {
                    Spans.Clear();
                }
            }
            finally
            {
            }
        }
    }

    List<int> _sourceMap;

    /// <summary>
    /// For each character of the drawn text (the spans' text in order), its index in the markdown source
    /// (<see cref="SkiaLabel.Text"/> as parsed), -1 for characters the markdown adds (list bullets, breaks between blocks).
    /// Null when markdown is off or the text is empty. An editor uses it to map its text index to the drawn character.
    /// </summary>
    public IReadOnlyList<int> SourceIndexes { get; private set; }

    /// <summary>
    /// Records the source index of each character of <paramref name="text"/> about to be drawn: the next equal
    /// character of the source slice [<paramref name="start"/>, start + <paramref name="length"/>) from
    /// <paramref name="from"/> on (markup in the slice is skipped), -1 when none. Returns the slice index after the last
    /// match.
    /// </summary>
    int MapSource(string text, int start, int length, int from = -1)
    {
        var pos = from >= 0 ? from : start;
        if (_sourceMap == null || text == null)
            return pos;
        var source = TextInternal ?? string.Empty;
        var end = Math.Min(start + length, source.Length);
        foreach (var c in text)
        {
            var at = -1;
            for (var j = pos; j < end; j++)
            {
                if (source[j] == c)
                {
                    at = j;
                    break;
                }
            }
            if (at >= 0)
                pos = at + 1;
            _sourceMap.Add(at);
        }
        return pos;
    }

    /// <summary>
    /// CommonMark parser settings: the defaults plus ~~strikethrough~~, with source positions (see
    /// <see cref="SourceIndexes"/>).
    /// </summary>
    static readonly CommonMarkSettings MarkdownSettings = CreateMarkdownSettings();

    static CommonMarkSettings CreateMarkdownSettings()
    {
        var settings = CommonMarkSettings.Default.Clone();
        settings.AdditionalFeatures |= CommonMarkAdditionalFeatures.StrikethroughTilde;
        settings.TrackSourcePosition = true;
        return settings;
    }

    /// <summary>
    /// Renders a markdown block element into text spans
    /// </summary>
    protected void RenderBlock(Block block, Inline prefix = null)
    {
        var wasHeading1 = isHeading1;
        var wasHeading2 = isHeading2;
        var wasHeading3 = isHeading3;
        var wasCode = isCodeBlock;

        if (block.Tag == BlockTag.AtxHeading || block.Tag == BlockTag.SetextHeading)
        {
            var level = block.Heading.Level;
            isHeading1 = level == 1;
            isHeading2 = level == 2;
            isHeading3 = level >= 3;

            if (block.InlineContent != null)
            {
                if (hadParagraph)
                {
                    RenderInline(CreateLineBreak());
                }

                hadParagraph = true;

                var currentInline = block.InlineContent;
                while (currentInline != null)
                {
                    RenderInline(currentInline);
                    currentInline = currentInline.NextSibling;
                }
            }
        }
        else if (block.Tag == BlockTag.Paragraph)
        {
            if (hadParagraph)
            {
                RenderInline(CreateLineBreak());
            }

            if (prefix != null)
            {
                RenderInline(prefix);
            }

            hadParagraph = true;

            var currentInline = block.InlineContent;
            while (currentInline != null)
            {
                RenderInline(currentInline);
                currentInline = currentInline.NextSibling;
            }
        }
        else if (block.Tag == BlockTag.IndentedCode || block.Tag == BlockTag.FencedCode)
        {
            if (hadParagraph)
            {
                RenderInline(CreateLineBreak());
            }

            if (prefix != null)
            {
                RenderInline(prefix);
            }

            hadParagraph = true;
            isCodeBlock = true;

            var content = block.StringContent.ToString();
            var lines = content.Split('\n');
            var mapFrom = block.SourcePosition;
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    RenderInline(CreateLineBreak());
                }

                mapFrom = MapSource(lines[i], block.SourcePosition, block.SourceLength, mapFrom);
                AddTextSpan(lines[i], (span) =>
                {
                    span.TextColor = CodeTextColor;
                    span.ParagraphColor = ColorCodeBlock;
                });
            }
        }
        else if (block.Tag == BlockTag.List)
        {
            var itemNumber = 1;
            var listData = block.ListData;
            var listItem = block.FirstChild;
            while (listItem != null)
            {
                if (listData.ListType == ListType.Ordered)
                {
                    RenderOrderedListItem(listItem, itemNumber);
                    itemNumber++;
                }
                else
                {
                    RenderBulletListItem(listItem);
                }

                listItem = listItem.NextSibling;
            }
        }
        else if (block.Tag == BlockTag.ListItem)
        {
            var childBlock = block.FirstChild;
            while (childBlock != null)
            {
                RenderBlock(childBlock);
                childBlock = childBlock.NextSibling;
            }
        }

        isCodeBlock = wasCode;
        isHeading1 = wasHeading1;
        isHeading2 = wasHeading2;
        isHeading3 = wasHeading3;
    }

    // Replace RenderOrderedListItem method:
    protected void RenderOrderedListItem(Block listItem, int startNumber)
    {
        Inline prefix;
        var firstLine = true;
        var childBlock = listItem.FirstChild;

        while (childBlock != null)
        {
            if (firstLine)
            {
                prefix = CreateLiteral(string.Format(PrefixNumbered, startNumber));
                firstLine = false;
            }
            else
            {
                prefix = null;
            }

            RenderBlock(childBlock, prefix);
            childBlock = childBlock.NextSibling;
        }
    }

    // Replace RenderBulletListItem method:
    protected void RenderBulletListItem(Block listItem)
    {
        Inline prefix;
        var firstLine = true;
        var childBlock = listItem.FirstChild;

        while (childBlock != null)
        {
            if (firstLine)
            {
                prefix = CreateLiteral(PrefixBullet);
                firstLine = false;
            }
            else
            {
                prefix = null;
            }

            RenderBlock(childBlock, prefix);
            childBlock = childBlock.NextSibling;
        }
    }

    // Replace RenderInline method:
    protected void RenderInline(Inline inline)
    {
        bool wasBold = isBold;
        bool wasItalic = isItalic;
        bool wasStrikethrough = isStrikethrough;

        switch (inline.Tag)
        {
            case InlineTag.LineBreak:
            case InlineTag.SoftBreak:
                MapSource("\n", inline.SourcePosition, inline.SourceLength);
                var last = Spans.LastOrDefault();
                if (last == null)
                {
                    Spans.Add(new() { Text = "\n" });
                }
                else
                {
                    last.Text += "\n";
                }

                break;

            case InlineTag.String:
                MapSource(inline.LiteralContent, inline.SourcePosition, inline.SourceLength);
                AddTextSpan(inline.LiteralContent);
                break;

            case InlineTag.Code:
                AddCodeSpan(inline);
                break;

            case InlineTag.Link:
                AddLinkSpan(inline);
                break;

            case InlineTag.Strong:
                isBold = true;
                var strongChild = inline.FirstChild;
                while (strongChild != null)
                {
                    RenderInline(strongChild);
                    strongChild = strongChild.NextSibling;
                }

                break;

            case InlineTag.Emphasis:
                isItalic = true;
                var emphasisChild = inline.FirstChild;
                while (emphasisChild != null)
                {
                    RenderInline(emphasisChild);
                    emphasisChild = emphasisChild.NextSibling;
                }

                break;

            case InlineTag.Strikethrough:
                isStrikethrough = true;
                var strikeChild = inline.FirstChild;
                while (strikeChild != null)
                {
                    RenderInline(strikeChild);
                    strikeChild = strikeChild.NextSibling;
                }

                break;
        }

        isBold = wasBold;
        isItalic = wasItalic;
        isStrikethrough = wasStrikethrough;
    }

    protected void RenderBlock(Inline inline)
    {
        bool wasBold = isBold;
        bool wasItalic = isItalic;
        bool wasStrikethrough = isStrikethrough;

        switch (inline.Tag)
        {
            case InlineTag.LineBreak:
            case InlineTag.SoftBreak:
                MapSource("\n", inline.SourcePosition, inline.SourceLength);
                var last = Spans.LastOrDefault();
                if (last == null)
                {
                    Spans.Add(new() { Text = "\n" });
                }
                else
                {
                    last.Text += "\n";
                }

                break;

            case InlineTag.String:
                MapSource(inline.LiteralContent, inline.SourcePosition, inline.SourceLength);
                AddTextSpan(inline.LiteralContent);
                break;

            case InlineTag.Code:
                AddCodeSpan(inline);
                break;

            case InlineTag.Link:
                AddLinkSpan(inline);
                break;

            case InlineTag.Strong:
                isBold = true;
                var strongChild = inline.FirstChild;
                while (strongChild != null)
                {
                    RenderInline(strongChild);
                    strongChild = strongChild.NextSibling;
                }

                break;

            case InlineTag.Emphasis:
                isItalic = true;
                var emphasisChild = inline.FirstChild;
                while (emphasisChild != null)
                {
                    RenderInline(emphasisChild);
                    emphasisChild = emphasisChild.NextSibling;
                }

                break;

            case InlineTag.Strikethrough:
                isStrikethrough = true;
                var strikeChild = inline.FirstChild;
                while (strikeChild != null)
                {
                    RenderInline(strikeChild);
                    strikeChild = strikeChild.NextSibling;
                }

                break;
        }

        isBold = wasBold;
        isItalic = wasItalic;
        isStrikethrough = wasStrikethrough;
    }

    protected virtual void AddCodeSpan(Inline code)
    {
        string text = code.LiteralContent;

        MapSource(text, code.SourcePosition, code.SourceLength);
        AddTextSpan(text, (span) =>
        {
            span.TextColor = this.CodeTextColor;
            span.BackgroundColor = CodeBackgroundColor;
        });
    }


    protected virtual void AddLinkSpan(Inline link)
    {
        string text = GetLinkLabelText(link);
        if (string.IsNullOrEmpty(text))
            text = link.TargetUrl;

        MapSource(text, link.SourcePosition, link.SourceLength);
        AddTextSpan(text, (span) =>
        {
            span.Tag = link.TargetUrl;
            span.TextColor = this.LinkColor;
            span.Underline = this.UnderlineLink;
            span.UnderlineWidth = this.UnderlineWidth;
            span.ForceCaptureInput = true;
        });
    }


    protected virtual string GetLinkLabelText(Inline link)
    {
        var child = link.FirstChild;
        string labelText = "";
        while (child != null)
        {
            if (child.Tag == InlineTag.String)
            {
                labelText += child.LiteralContent;
            }

            child = child.NextSibling;
        }

        return labelText;
    }


    /// <summary>
    /// Creates a line break inline element
    /// </summary>
    protected virtual Inline CreateLineBreak()
    {
        return new Inline(InlineTag.LineBreak);
    }

    /// <summary>
    /// Creates a literal inline element with specified content
    /// </summary>
    protected virtual Inline CreateLiteral(string content)
    {
        return new Inline(InlineTag.String) { LiteralContent = content };
    }
}
