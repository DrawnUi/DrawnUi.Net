using System.ComponentModel;
using System.Diagnostics;
using DrawnUi.Controls;
using DrawnUi.Draw;

namespace DrawnUiRepro;

/// <summary>
/// Deco-triangle tail glued to the left of an incoming bubble. Dedicated subclass with fixed
/// visuals + CacheSharing=Shared: every instance across all recycled cells reuses ONE physical
/// cache surface for the whole type instead of rendering/storing its own.
/// </summary>
public class IncomingBubbleSign : SkiaShape
{
    /// <summary>
    /// Deco-triangle tail glued to the right of an outgoing bubble. Same shared-cache trick.
    /// Shown for the first message in a day group for every direction.
    /// </summary>
    public IncomingBubbleSign()
    {
        Type = ShapeType.Polygon;
        // relative 0..1: vertical right edge glued to the bubble, apex pointing left-top
        Points = new List<SkiaPoint> { new(1, 0), new(0, 0), new(1, 1) };
        BackgroundColor = ChatCell.ColorIncoming;
        WidthRequest = 8;
        HeightRequest = 10;
        Top = 6;
        Left = 2;
        UseCache = SkiaCacheType.Operations;
        CacheSharing = CacheSharingType.Shared; //same cache used fo ALL instances!
    }
}

/// <summary>
/// Deco-triangle tail glued to the right of an outgoing bubble. Same shared-cache trick.
/// Shown for the first message in a day group for every direction.
/// </summary>
public class OutcomingBubbleSign : SkiaShape
{
    public OutcomingBubbleSign()
    {
        Type = ShapeType.Polygon;
        // mirrored: vertical left edge glued to the bubble, apex pointing right-top
        Points = new List<SkiaPoint> { new(0, 0), new(1, 0), new(0, 1) };
        BackgroundColor = ChatCell.ColorOutgoing;
        WidthRequest = 8;
        HeightRequest = 10;
        Top = 6;
        Left = -2;
        UseCache = SkiaCacheType.Operations;
        CacheSharing = CacheSharingType.Shared; //same cache used fo ALL instances!
    }
}

/// <summary>
/// Recycled chat bubble cell: incoming/outgoing alignment + colors, optional image banner
/// (Image type), tappable link (Link type), date separator on day change and a bubble tail
/// on the first message of a consecutive same-sender run, runs resetting on a new day
/// (ghost tail on follow-ups keeps alignment).
/// All visual states are reset in SetContent because the same instance is recycled
/// across message types.
/// </summary>
public class ChatCell : SkiaDynamicDrawnCell
{
    private const float MaxBubbleWidth = 280f;

    public static readonly Color ColorIncoming = ChatTheme.Incoming;
    public static readonly Color ColorOutgoing = ChatTheme.Outgoing;
    public static readonly Color ColorCheck = ChatTheme.Check;
    public static readonly Color ColorCheckRead = ChatTheme.AccentBright;

    private readonly SkiaLayout _row;
    private readonly SkiaShape _bubble;
    private readonly IncomingBubbleSign _tailIn;
    private readonly OutcomingBubbleSign _tailOut;
    private readonly SkiaImage _banner;
    private readonly SkiaSvg _fileIcon;
    private readonly SkiaLayout _quoteBox;
    private readonly SkiaLabel _quoteName;
    private readonly SkiaLabel _quoteText;
    private readonly SkiaRichLabel _label;
    private readonly SkiaLabel _time;
    private readonly SkiaSvg _checkSent;
    private readonly SkiaSvg _checkDelivered;
    private readonly SkiaShape _dayChip;
    private readonly SkiaLabel _day;

    //inline so the sample needs no assets, same look as the original app's SvgCheck
    private const string SvgCheck =
        "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'><path d='M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z'/></svg>";

    //paperclip for file attachments, like the original app's SvgAttachment
    public const string SvgAttachment =
        "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'><path d='M16.5 6v11.5a4 4 0 0 1-8 0V5a2.5 2.5 0 0 1 5 0v10.5a1 1 0 0 1-2 0V6H10v9.5a2.5 2.5 0 0 0 5 0V5a4 4 0 0 0-8 0v12.5a5.5 5.5 0 0 0 11 0V6z'/></svg>";

    public ChatCell()
    {
        HorizontalOptions = LayoutOptions.Fill;
        Rotation = 180; //the scroll is rotated 180 (inverted chat), rotate the cell back upright
        FastMeasurement = true; //use fast measurement path with less passes for children fills but faster

        IsParentIndependent = true;

        //we will be suing sell in context of MeasureVisible (background measure) so
        //we about ImageDoubleBuffered and GPU for background thread safe processing
        UseCache = SkiaCacheType.Image;

        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 0,
                Children =
                {
                    //date separator on day change: telegram-style pill chip
                    new SkiaShape
                    {
                        IsVisible = false,
                        UseCache = SkiaCacheType.Image,
                        Type = ShapeType.Rectangle,
                        CornerRadius = 11,
                        BackgroundColor = Color.FromArgb("#40000000"),
                        Padding = new Thickness(10, 3),
                        HorizontalOptions = LayoutOptions.Center,
                        Margin = new Thickness(10, 6, 10, 8),
                        Children =
                        {
                            new SkiaLabel
                            {
                                FontSize = 11,
                                TextColor = Color.FromArgb("#BBC7D3"),
                            }.Assign(out _day),
                        }
                    }.Assign(out _dayChip),

                    //container to glue the bubble with its tail
                    new SkiaLayout
                    {
                        Type = LayoutType.Row,
                        Spacing = 0,
                        Margin = new Thickness(8, 0),
                        Children =
                        {
                            new IncomingBubbleSign().Assign(out _tailIn),

                            new SkiaShape
                            {
                                Type = ShapeType.Rectangle,
                                CornerRadius = 14,
                                Padding = 0,
                                MaximumWidthRequest = MaxBubbleWidth,
                                Children =
                                {
                                    //row glues the optional file icon to the message column (original app structure)
                                    new SkiaLayout
                                    {
                                        Type = LayoutType.Row,
                                        Spacing = 0,
                                        Children =
                                        {
                                            //icon for file attachments
                                            new SkiaSvg
                                            {
                                                IsVisible = false,
                                                UseCache = SkiaCacheType.Operations,
                                                SvgString = SvgAttachment,
                                                TintColor = Color.FromArgb("#88FFFFFF"),
                                                HeightRequest = 20,
                                                LockRatio = 1,
                                                Margin = new Thickness(10, 0, 0, 0),
                                                VerticalOptions = LayoutOptions.Center,
                                            }.Assign(out _fileIcon),

                                            new SkiaStack
                                            {
                                                Spacing = 0,
                                                Children =
                                                {
                                                    //image/link attachment banner
                                                    new SkiaImage
                                                    {
                                                    IsVisible = false,
                                                    UseCache = SkiaCacheType.ImageDoubleBuffered, //avoid spikes when updating
                                                    Aspect = TransformAspect.AspectCover,
                                                    BackgroundColor = Colors.DimGray,
                                                    HeightRequest = 140,
                                                    HorizontalOptions = LayoutOptions.Fill,
                                                    EraseChangedContent = true,
                                                    }.Assign(out _banner),

                                                    //QUOTED MESSAGE this one replies to (original app's
                                                    //AttachedMessageStack): accent bar + author + text,
                                                    //tap jumps to the original message
                                                    new SkiaLayout
                                                    {
                                                        IsVisible = false,
                                                        UseCache = SkiaCacheType.Operations,
                                                        Margin = new Thickness(10, 8, 10, 0),
                                                        HorizontalOptions = LayoutOptions.Fill,
                                                        Children =
                                                        {
                                                            new SkiaShape
                                                            {
                                                                UseCache = SkiaCacheType.Operations,
                                                                Type = ShapeType.Rectangle,
                                                                CornerRadius = 2,
                                                                WidthRequest = 3,
                                                                BackgroundColor = ChatTheme.AccentBright,
                                                                VerticalOptions = LayoutOptions.Fill,
                                                            },

                                                            new SkiaStack
                                                            {
                                                                HorizontalOptions = LayoutOptions.Fill,
                                                                Margin = new(11,0,0,0),
                                                                Spacing = 1,
                                                                Children =
                                                                {
                                                                    new SkiaLabel
                                                                    {
                                                                        FontSize = 11,
                                                                        TextColor = ChatTheme.AccentBright,
                                                                        MaxLines = 1,
                                                                        LineBreakMode = LineBreakMode.TailTruncation,
                                                                    }.Assign(out _quoteName),

                                                                    new SkiaLabel
                                                                    {
                                                                        FontSize = 12,
                                                                        TextColor = Color.FromArgb("#AAFFFFFF"),
                                                                        MaxLines = 1,
                                                                        LineBreakMode = LineBreakMode.TailTruncation,
                                                                    }.Assign(out _quoteText),
                                                                }
                                                            },
                                                        }
                                                    }.Assign(out _quoteBox).OnTapped(me =>
                                                    {
                                                        if (BindingContext is ChatMessage msg
                                                            && msg.ReplyTo != null
                                                            && Parent?.BindingContext is IChatCellActions actions)
                                                        {
                                                            actions.ScrollToMessage(msg.ReplyTo);
                                                        }
                                                    }),

                                                    //message text (markdown links supported)
                                                    new SkiaRichLabel
                                                    {
                                                        FontSize = 14,
                                                        TextColor = Colors.White,
                                                        Margin = new Thickness(12, 8, 12, 0),
                                                    }.Assign(out _label),

                                            //time sent + delivery status checkmarks
                                            new SkiaRow
                                            {
                                                Spacing = 3,
                                                HorizontalOptions = LayoutOptions.End,
                                                Margin = new Thickness(12, 2, 12, 6),
                                                Children =
                                                {
                                                    new SkiaLabel
                                                    {
                                                        FontSize = 9,
                                                        TextColor = Color.FromArgb("#88FFFFFF"),
                                                    }.Assign(out _time),

                                                    // STATUS SENT
                                                    new SkiaSvg
                                                    {
                                                        IsVisible = false,
                                                        UseCache = SkiaCacheType.Operations,
                                                        SvgString = SvgCheck,
                                                        TintColor = ColorCheck,
                                                        HeightRequest = 11,
                                                        WidthRequest = 11,
                                                        VerticalOptions = LayoutOptions.Center,
                                                    }.Assign(out _checkSent),

                                                    // STATUS DELIVERED: overlaps the first check into a double-check
                                                    new SkiaSvg
                                                    {
                                                        IsVisible = false,
                                                        UseCache = SkiaCacheType.Operations,
                                                        SvgString = SvgCheck,
                                                        TintColor = ColorCheck,
                                                        HeightRequest = 11,
                                                        WidthRequest = 11,
                                                        VerticalOptions = LayoutOptions.Center,
                                                        TranslationX = -8,
                                                    }.Assign(out _checkDelivered),
                                                }
                                            },
                                                }
                                            },
                                        }
                                    }
                                }
                            }.Assign(out _bubble).OnTapped(me =>
                            {
                                if (me.BindingContext is ChatMessage msg)
                                {
                                    Debug.WriteLine($"[CHAT] tapped message {msg.Index} ({msg.Type})");

                                    //open tapped image fullscreen, page reached like in the
                                    //original app: cell -> Parent (items stack) -> BindingContext
                                    if (msg.Type == ChatMessageType.Image
                                        && Parent?.BindingContext is IChatCellActions actions)
                                    {
                                        actions.ShowImageFullscreen(msg);
                                    }
                                }
                            }).OnLongPressing(me =>
                            {
                                //long-press a bubble: quote it (original app showed an options
                                //menu here; the sample goes straight to reply)
                                if (me.BindingContext is ChatMessage msg
                                    && Parent?.BindingContext is IChatCellActions actions)
                                {
                                    actions.ReplyToMessage(msg);
                                }
                            }),

                            new OutcomingBubbleSign().Assign(out _tailOut),
                        }
                    }.Assign(out _row),
                }
            }
        };

        _label.LinkTapped += (s, url) => Debug.WriteLine($"[CHAT] link tapped: {url}");
    }

    protected override void SetContent(object ctx)
    {
        if (ctx is not ChatMessage msg)
            return;

        _dayChip.IsVisible = msg.IsFirstDay;
        _day.Text = msg.DayDesc;

        _row.HorizontalOptions = msg.Outgoing ? LayoutOptions.End : LayoutOptions.Start;
        _bubble.BackgroundColor = msg.Outgoing ? ColorOutgoing : ColorIncoming;

        //tail only on the first message of a consecutive same-sender run (run resets on
        //a new day); follow-ups keep an invisible ghost tail so all bubbles stay aligned
        _tailIn.IsVisible = !msg.Outgoing;
        _tailIn.IsGhost = !msg.IsFirstOfGroup;
        _tailOut.IsVisible = msg.Outgoing;
        _tailOut.IsGhost = !msg.IsFirstOfGroup;

        _fileIcon.IsVisible = msg.Type == ChatMessageType.File; //paperclip glued left of the text

        //quoted message block (reset on recycle!)
        if (msg.ReplyTo != null)
        {
            _quoteBox.IsVisible = true;
            _quoteName.Text = msg.ReplyTo.AuthorName;
            _quoteText.Text = msg.ReplyTo.Text;
        }
        else
        {
            _quoteBox.IsVisible = false;
        }

        if (msg.Type == ChatMessageType.Image)
        {
            _banner.PreviewBase64 = msg.PreviewBase64; //instant inline preview while the url loads
            _banner.Source = msg.ImageUrl;
            _bubble.WidthRequest = MaxBubbleWidth; //banner needs a defined width
            _banner.IsVisible = true;
        }
        else
        {
            _banner.IsVisible = false;
            _banner.PreviewBase64 = null;
            _banner.Source = null;
            _bubble.WidthRequest = -1; //auto-width from text
        }

        _label.Text = msg.Type == ChatMessageType.Link
            ? $"{msg.Text}\n<{msg.LinkUrl}>"
            : msg.Text;

        _time.Text = msg.Time;
        _time.TextColor = msg.Outgoing ? ChatTheme.TimeOutgoing : ChatTheme.TimeIncoming;

        UpdateStatus(msg);
    }

    /// <summary>
    /// Delivery checkmarks for outgoing messages, like the original app:
    /// nothing while sending, one check when Sent, two when Delivered, blue when Read.
    /// Called from SetContent on (re)bind and live from ContextPropertyChanged
    /// while the mock api advances the stages.
    /// </summary>
    private void UpdateStatus(ChatMessage msg)
    {
        if (!msg.Outgoing)
        {
            _checkSent.IsVisible = false;
            _checkDelivered.IsVisible = false;
            return;
        }

        var tint = msg.Read ? ColorCheckRead : ColorCheck;
        _checkSent.TintColor = tint;
        _checkDelivered.TintColor = tint;

        _checkSent.IsVisible = msg.Sent;            //nothing while still sending
        _checkDelivered.IsVisible = msg.Delivered;  //second check completes the pair
    }

    protected override void ContextPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChatMessage.Sent)
            or nameof(ChatMessage.Delivered)
            or nameof(ChatMessage.Read))
        {
            if (Context is ChatMessage msg)
            {
                UpdateStatus(msg);
                Update();
            }
        }

        base.ContextPropertyChanged(sender, e);
    }
}
