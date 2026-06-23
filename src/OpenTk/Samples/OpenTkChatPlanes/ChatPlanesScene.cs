using System.Windows.Input;
using AppoMobi.Specials;
using DrawnUi.Infrastructure.Enums;
using DrawnUiRepro;

namespace OpenTkChatPlanes;

/// <summary>
/// GPU reproduction using the REAL ChatCell + ChatMessage + windowing copied from the Android repro
/// ChatPage, so the OpenTK sample exercises exactly what breaks on device (no simplified cell). Inverted
/// scroll + windowed 150-cap ItemsSource + bidirectional LoadMore + trim, UsePlaneCache on.
/// </summary>
public sealed partial class ChatPlanesScene : IChatCellActions
{
    private readonly ObservableRangeCollection<ChatMessage> _items = new();
    private readonly List<ChatMessage> _all = new();
    private int _windowStart;
    private int _windowEnd;
    private const int TotalItems = 322; // mirror MAUI ChatPage
    private const int LoadBatch = 50;
    private const bool LimitMemoryWindow = true;
    private const int MaxItemsInMemory = 150;
    private const int HardCap = MaxItemsInMemory + 2 * LoadBatch; // 250: force-trim ceiling even mid-fling
    private const int TrimSafetyBuffer = 30; // keep this many resident cells beyond the viewport each side

    public int LoadOlderCalls;
    public int LoadNewerCalls;
    public int TrimEvents;

    public SkiaScroll MainScroll = null!;
    public SkiaLayout ChatStack = null!;

    public int ResidentCount => _items.Count;
    public int WindowStart => _windowStart;
    public int WindowEnd => _windowEnd;

    /// <summary>Local index of an image cell roughly in the middle of the resident window (for tap tests).</summary>
    public int MiddleImageLocal()
    {
        int mid = _items.Count / 2;
        for (int off = 0; off < _items.Count; off++)
        {
            int i = mid + off; if (i < _items.Count && _items[i].Type == ChatMessageType.Image) return i;
            i = mid - off; if (i >= 0 && _items[i].Type == ChatMessageType.Image) return i;
        }
        return -1;
    }

    public ChatPlanesScene()
    {
        for (int i = 0; i < TotalItems; i++)
            _all.Add(ChatMessage.CreateMock(i));

        _windowEnd = _all.Count;
        _windowStart = Math.Max(0, _windowEnd - LoadBatch);
        _items.AddRange(ReversedRange(_windowStart, _windowEnd - _windowStart));
    }

    private List<ChatMessage> ReversedRange(int from, int count)
    {
        var batch = new List<ChatMessage>(count);
        for (int i = from + count - 1; i >= from; i--)
            batch.Add(_all[i]);
        return batch;
    }

    // INLINE trim-before-load, matching MAUI ChatPage and the documented windowed contract: trim the
    // OPPOSITE end atomically before growing. The previous DEFERRED trim (SafeAction) raced the head-insert
    // index shift and produced duplicate local indices (DUP-idx structure corruption).
    private void LoadOlder()
    {
        LoadOlderCalls++;
        if (_windowStart <= 0) return;
        int n = Math.Min(LoadBatch, _windowStart);

        if (LimitMemoryWindow)
        {
            int over = _items.Count + n - MaxItemsInMemory;
            if (over > 0) { _items.RemoveRange(0, over); _windowEnd -= over; TrimEvents++; } // trim newest (front)
        }

        _windowStart -= n;
        _items.AddRange(ReversedRange(_windowStart, n));
    }

    private void LoadNewer()
    {
        LoadNewerCalls++;
        if (_windowEnd >= _all.Count) return;
        int n = Math.Min(LoadBatch, _all.Count - _windowEnd);

        if (LimitMemoryWindow)
        {
            int over = _items.Count + n - MaxItemsInMemory;
            if (over > 0) { _items.RemoveRange(_items.Count - over, over); _windowStart += over; TrimEvents++; } // trim oldest (tail)
        }

        _items.InsertRange(0, ReversedRange(_windowEnd, n));
        _windowEnd += n;
    }

    /// <summary>
    /// DEFERRED far-side trim. Call every frame (or on a scroll-settle debounce). Trims the resident window
    /// back to <see cref="MaxItemsInMemory"/> ONLY when scrolling has settled and no structure rebase is in
    /// flight — so the trim's index/measurement shift never overlaps a head-insert (the fragmentation race),
    /// and its cost lands on a calm frame instead of a fast-scroll frame. Removes cells from the end FARTHEST
    /// from the viewport so it doesn't reflow visible content.
    /// </summary>
    public void MaybeTrimDeferred()
    {
        // Trim now happens INLINE in LoadOlder/LoadNewer (see above). Deferred far-side trim raced the
        // head-insert and corrupted indices (DUP-idx), so it is disabled. Kept as a no-op so the harness
        // call site is unchanged.
    }

    // Safety net: window overshot the hard cap (e.g. an endless fling) — queue a trim so memory can't run away.
    private void ForceTrimIfOverHardCap()
    {
        if (!LimitMemoryWindow) return;
        if (_items.Count > HardCap)
            EnqueueTrim();
    }

    private bool _trimEnqueued;

    private void EnqueueTrim()
    {
        if (_trimEnqueued) return;
        _trimEnqueued = true;
        ChatStack.SafeAction(() => { _trimEnqueued = false; TrimFarSideNow(); });
    }

    // Runs at the safe drain point (no measurement in flight, structure consistent). Recomputes from current state.
    private void TrimFarSideNow()
    {
        int want = _items.Count - MaxItemsInMemory;
        if (want <= 0 || _items.Count == 0) return;

        int first = ChatStack.FirstVisibleIndex;
        int last = ChatStack.LastVisibleIndex;
        if (first < 0) first = 0;
        if (last < 0) last = _items.Count - 1;

        int slackFront = Math.Max(0, first - TrimSafetyBuffer);                   // droppable above viewport
        int slackEnd = Math.Max(0, (_items.Count - 1 - last) - TrimSafetyBuffer); // droppable below viewport

        if (slackEnd >= slackFront)
        {
            int cut = Math.Min(want, slackEnd);
            if (cut > 0) { _items.RemoveRange(_items.Count - cut, cut); _windowStart += cut; TrimEvents++; }
        }
        else
        {
            int cut = Math.Min(want, slackFront);
            if (cut > 0) { _items.RemoveRange(0, cut); _windowEnd -= cut; TrimEvents++; }
        }
    }

    // tap diagnostics
    public int LastTapMsgIndex = -1;
    public string LastTapAction = "";
    public int LastChildIndex = -1;   // ANY cell tapped (cell-level hit)
    public int LastImageIndex = -1;   // inner image tapped (inner hit-rect)

    // IChatCellActions — log so the harness can verify a tap landed on the right cell
    public void ShowImageFullscreen(ChatMessage msg) { LastImageIndex = msg.Index; LastTapMsgIndex = msg.Index; LastTapAction = "Image"; }
    public void ReplyToMessage(ChatMessage msg) { LastTapMsgIndex = msg.Index; LastTapAction = "Reply"; }
    public void ScrollToMessage(ChatMessage msg)
    {
        int local = _windowEnd - 1 - msg.Index;
        if (local >= 0 && local < _items.Count)
            MainScroll.ScrollToIndex(local, true, RelativePositionType.Start, true);
    }

    public Canvas BuildCanvas()
    {
        var canvas = CreateCanvas();

        // Cells reach the actions via Parent.BindingContext (same as the real ChatPage).
        ChatStack.BindingContext = this;

        // fires for ANY child tap (text or image) -> tells us the tap reached a cell + which index
        ChatStack.ChildTapped += (s, e) =>
        {
            var c = e.Control as SkiaControl;
            LastChildIndex = c?.ContextIndex ?? -2;
        };
        return canvas;
    }

    public Canvas CreateCanvas()
    {
        return new Canvas
        {
            Tag = "ChatCanvas",
            // NOTE: MAUI-only — Canvas.Gestures has no OpenTk/Net equivalent (only src/Maui + Blazor define it).
            // Dropped to compile under OpenTk; the Net Canvas handles gestures without this property.
            RenderingMode = RenderingModeType.Accelerated,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            BackgroundColor = ChatTheme.Bg,
            Content = new SkiaLayer()
            {
                VerticalOptions = LayoutOptions.Fill,
                Children =
                {
                    new SkiaStack
                    {
                        Spacing = 0,
                        HorizontalOptions = LayoutOptions.Fill,
                        //VerticalOptions = LayoutOptions.Fill,
                        Children =
                        {
                            // NAVBAR: drawn replacement for the MAUI Shell bar (hidden in ctor) —
                            // animated gif avatar + bot name + live "typing…" status
                            new SkiaLayout
                            {
                                UseCache = SkiaCacheType.GPU,
                                Type = LayoutType.Grid,
                                ColumnSpacing = 12,
                                Margin = new(0,20,0,0),//todo nav model
                                Padding = new Thickness(12, 8),
                                BackgroundColor = ChatTheme.BarBg,
                                HorizontalOptions = LayoutOptions.Fill,
                                Children =
                                {
                                    //avatar: gif clipped by a circle
                                    new SkiaShape
                                    {
                                        Type = ShapeType.Circle,
                                        WidthRequest = 40,
                                        LockRatio = 1,
                                        BackgroundColor = ChatTheme.InputBg,
                                        VerticalOptions = LayoutOptions.Center,
                                        Children =
                                        {
                                            new SkiaGif
                                            {
                                                Source = "Images/banana.gif",
                                                Repeat = -1,
                                                HorizontalOptions = LayoutOptions.Fill,
                                                VerticalOptions = LayoutOptions.Fill,
                                            },
                                        }
                                    }.WithColumn(0),

                                    new SkiaStack
                                    {
                                        Spacing = 1,
                                        VerticalOptions = LayoutOptions.Center,
                                        Children =
                                        {
                                            new SkiaLabel
                                            {
                                                UseCache = SkiaCacheType.Operations,
                                                Text = "Banana Bot",
                                                FontSize = 15,
                                                TextColor = Colors.White,
                                            },

                                            new SkiaLabel
                                            {
                                                UseCache = SkiaCacheType.Operations,
                                                Text = "online",
                                                FontSize = 11,
                                                TextColor = Color.FromArgb("#88FFFFFF"),
                                            }.Assign(out StatusLabel),
                                        }
                                    }.WithColumn(1),
                                }
                            }.WithColumnDefinitions("40,*"),

                            new SkiaLayer()
                            {
                                VerticalOptions = LayoutOptions.Fill,
                                Children =
                                {
                                    // MESSAGES
                                    new SkiaScroll
                                        {
                                            Orientation = ScrollOrientation.Vertical,
                                            ResetScrollPositionOnContentSizeChanged = false,

                                            // Inverted chat (original app trick): content rotated 180 so the
                                            // list start (= newest message) sits at the visual bottom; cells
                                            // rotate themselves back upright (ChatCell.Rotation = 180).
                                            Rotation = 180,
                                            ReverseGestures = true,
                                            TrackIndexPosition = RelativePositionType.Start,

                                            // bottom trigger = visually scrolling UP = load history
                                            LoadMoreCommand = new Command(LoadOlder),
                                            LoadMoreOffset = 800,

                                            // top trigger = visually scrolling DOWN = reload trimmed newer part
                                            LoadMoreTopCommand = new Command(LoadNewer),
                                            LoadMoreTopOffset = 800,

                                            HorizontalOptions = LayoutOptions.Fill,
                                            VerticalOptions = LayoutOptions.Fill,

                                            Content = new ChatMessagesStack
                                            {
                                                //UseCache = SkiaCacheType.Operations,
                                                VirtualisationInflatedRatio = 1.0,
                                                ItemTemplateType = typeof(ChatCell),
                                                ItemsSource = _items,
                                                RecyclingTemplate = RecyclingTemplate.Enabled,
                                                MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
                                                Spacing = 4,
                                                Padding = new Thickness(0, 8),
                                            }.Assign(out ChatStack),

                                        }.Assign(out MainScroll)
                                        .Observe(this,
                                            (me, s) =>
                                            {
                                                if (s == nameof(this.KeyboardSize))
                                                {
                                                    me.AdaptToKeyboardFor = Canvas.FocusedChild as SkiaControl;
                                                    me.AdaptToKeyboardSize = KeyboardSize;
                                                }
                                            }),

                                    // ATTACHMENT-REPLY WHILE TYPING: quote panel above the send bar.
                                    // Improvement over the original: tap the panel to JUMP to the quoted
                                    // message, the X cancels (original canceled on any tap).
                                    new SkiaLayout
                                        {
                                            Tag = "ReplyPanel",
                                            IsVisible = false,
                                            Type = LayoutType.Grid,
                                            UseCache = SkiaCacheType.GPU,
                                            ColumnSpacing = 10,
                                            Padding = new Thickness(12, 8),
                                            BackgroundColor = ChatTheme.BarBg,
                                            VerticalOptions = LayoutOptions.End,
                                            HorizontalOptions = LayoutOptions.Fill,
                                            Children =
                                            {
                                                new SkiaSvg
                                                {
                                                    UseCache = SkiaCacheType.Operations,
                                                    SvgString = SvgReply,
                                                    TintColor = ChatTheme.IconMuted,
                                                    HeightRequest = 18,
                                                    LockRatio = 1,
                                                    HorizontalOptions = LayoutOptions.Center,
                                                    VerticalOptions = LayoutOptions.Center,
                                                }.WithColumn(0),

                                                new SkiaStack
                                                {
                                                    Spacing = 1,
                                                    VerticalOptions = LayoutOptions.Center,
                                                    Children =
                                                    {
                                                        new SkiaLabel
                                                        {
                                                            FontSize = 12,
                                                            TextColor = ChatTheme.AccentBright,
                                                            MaxLines = 1,
                                                            LineBreakMode = LineBreakMode.TailTruncation,
                                                        }.Assign(out ReplyName),

                                                        new SkiaLabel
                                                        {
                                                            FontSize = 13,
                                                            TextColor = Color.FromArgb("#AAFFFFFF"),
                                                            MaxLines = 1,
                                                            LineBreakMode = LineBreakMode.TailTruncation,
                                                        }.Assign(out ReplyText),
                                                    }
                                                }.WithColumn(1),

                                                //X cancels the reply
                                                new SkiaLayer
                                                {
                                                    VerticalOptions = LayoutOptions.Fill,
                                                    HorizontalOptions = LayoutOptions.Fill,
                                                    Children =
                                                    {
                                                        new SkiaSvg
                                                        {
                                                            UseCache = SkiaCacheType.Operations,
                                                            SvgString = SvgClose,
                                                            TintColor = Color.FromArgb("#88FFFFFF"),
                                                            HeightRequest = 16,
                                                            LockRatio = 1,
                                                            HorizontalOptions = LayoutOptions.Center,
                                                            VerticalOptions = LayoutOptions.Center,
                                                        },
                                                    }
                                                }.OnTapped(me => CancelReply()).WithColumn(2),
                                            }
                                        }
                                        .WithColumnDefinitions("24,*,40")
                                        .Assign(out ReplyPanel)
                                        .OnTapped(me =>
                                        {
                                            if (_replyTo != null)
                                                ScrollToMessage(_replyTo);
                                        }),
                                }
                            },

                            // SEND BAR: SkiaEditor is a totally drawn control
                            new SkiaLayout
                            {
                                UseCache = SkiaCacheType.Operations,
                                Type = LayoutType.Grid,
                                ColumnSpacing = 8,
                                Padding = new Thickness(8),
                                BackgroundColor = ChatTheme.BarBg,
                                HorizontalOptions = LayoutOptions.Fill,
                                Children =
                                {
                                    // BTN ATTACH IMAGE: mock "photo picker"
                                    new SkiaLayer
                                    {
                                        VerticalOptions = LayoutOptions.Fill,
                                        HorizontalOptions = LayoutOptions.Fill,
                                        UseCache = SkiaCacheType.GPU,
                                        Children =
                                        {
                                            new SkiaSvg
                                            {
                                                UseCache = SkiaCacheType.Operations,
                                                SvgString = SvgImage,
                                                TintColor = ChatTheme.IconMuted,
                                                HeightRequest = 20,
                                                LockRatio = 1,
                                                VerticalOptions = LayoutOptions.Center,
                                                HorizontalOptions = LayoutOptions.Center,
                                            },
                                        }
                                    }.OnTapped(me => SendImage()).WithColumn(0),

                                    // BTN ATTACH FILE: mock "file picker"
                                    new SkiaLayer
                                    {
                                        UseCache = SkiaCacheType.GPU,
                                        VerticalOptions = LayoutOptions.Fill,
                                        HorizontalOptions = LayoutOptions.Fill,
                                        Children =
                                        {
                                            new SkiaSvg
                                            {
                                                UseCache = SkiaCacheType.Operations,
                                                SvgString = ChatCell.SvgAttachment,
                                                TintColor = ChatTheme.IconMuted,
                                                HeightRequest = 20,
                                                LockRatio = 1,
                                                VerticalOptions = LayoutOptions.Center,
                                                HorizontalOptions = LayoutOptions.Center,
                                            },
                                        }
                                    }.OnTapped(me => SendFile()).WithColumn(1),

                                    new SkiaEditor
                                    {
                                        UseCache = SkiaCacheType.Operations,
                                        HorizontalOptions = LayoutOptions.Fill,
                                        VerticalOptions = LayoutOptions.Center,
                                        CornerRadius = 18,
                                        BackgroundColor = ChatTheme.InputBg,
                                        Padding = new Thickness(12, 10),
                                        FontSize = 15,
                                        TextColor = Colors.White,
                                        CursorColor = Colors.Cyan,
                                        PlaceholderText = "Write a message…",
                                        PlaceholderColor = ChatTheme.IconMuted,
                                        MaxLines = 3,
                                        AutoHeight =
                                            true, //will auto-resize when we type more lines, up to MaxLines
                                        ReturnType = ReturnType.Send,
                                        CommandOnSubmit = new Command(SendMessage),
                                    }.Assign(out Editor).WithColumn(2),

                                    // SEND: telegram-style round button with a paper plane
                                    new SkiaShape
                                    {
                                        UseCache = SkiaCacheType.GPU,
                                        Type = ShapeType.Circle,
                                        WidthRequest = 42,
                                        LockRatio = 1,
                                        BackgroundColor = ChatTheme.Accent,
                                        VerticalOptions = LayoutOptions.Center,
                                        Children =
                                        {
                                            new SkiaSvg
                                            {
                                                Left = -1,
                                                UseCache = SkiaCacheType.Operations,
                                                SvgString = SvgSend,
                                                TintColor = Colors.White,
                                                HeightRequest = 19,
                                                LockRatio = 1,
                                                Margin = new Thickness(3, 0, 0, 0), //optical centering
                                                HorizontalOptions = LayoutOptions.Center,
                                                VerticalOptions = LayoutOptions.Center,
                                            },
                                        }
                                    }.OnTapped(me => SendMessage()).WithColumn(3),
                                }
                            }.WithColumnDefinitions("32,32,*,44"),


                            // KEYBOARD SPACER (mobile): pushes the typing bar above the soft keyboard
                            new SkiaControl
                            {
                                UseCache = SkiaCacheType.Operations,
                                HeightRequest = 0,
                                HorizontalOptions = LayoutOptions.Fill,
                            }.Observe(this, (me, prop) =>
                            {
                                if (prop == nameof(KeyboardSize))
                                {
                                    me.HeightRequest = KeyboardSize;
                                }
                            }),
                        }
                    },

                    // DEBUG OVERLAY: shows resident items window for the memory-cap demo
                    new SkiaLabel
                    {
                        UseCache = SkiaCacheType.Operations,
                        Margin = new(16, 0, 16, 100),
                        Padding = 2,
                        BackgroundColor = Color.Parse("#AA000000"),
                        HorizontalOptions = LayoutOptions.Start,
                        InputTransparent = true,
                        TextColor = Colors.LawnGreen,
                        VerticalOptions = LayoutOptions.Center,
                        Rotation = -20,
                        ZIndex = 100,
                    }.ObserveProperty(() => ChatStack, nameof(SkiaLayout.DebugString),
                        me => { me.Text = ChatStack.DebugString; }),

                    // SCROLL TO LAST MESSAGE: appears after scrolling 100+ pts into history
                    new SkiaShape
                    {
                        Type = ShapeType.Circle,
                        UseCache = SkiaCacheType.GPU,
                        IsVisible = false,
                        Opacity = 0,
                        WidthRequest = 46,
                        LockRatio = 1,
                        BackgroundColor = Color.Parse("#F217212B"),
                        HorizontalOptions = LayoutOptions.End,
                        VerticalOptions = LayoutOptions.End,
                        Margin = new Thickness(0, 0, 10, 120),
                        ZIndex = 90,
                        Children =
                        {
                            new SkiaSvg
                            {
                                UseCache = SkiaCacheType.Operations,
                                SvgString = SvgChevronDown,
                                TintColor = Color.FromArgb("#AAFFFFFF"),
                                HeightRequest = 24,
                                LockRatio = 1,
                                HorizontalOptions = LayoutOptions.Center,
                                VerticalOptions = LayoutOptions.Center,
                            },
                        }
                    }.Assign(out BtnScrollToEnd).OnTapped(me => ScrollToNewest(true)),

                    // FULLSCREEN IMAGE VIEWER POPUP: hidden overlay above everything, tap to close
                    // (the original app's GalleryPopup pattern, single image instead of carousel)
                    new SkiaLayer
                    {
                        IsVisible = false,
                        UseCache = SkiaCacheType.Operations,
                        ZIndex = 200,
                        BlockGesturesBelow = true,
                        BackgroundColor = Color.Parse("#EE000000"),
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        Children =
                        {
                            new SkiaImage
                            {
                                UseCache = SkiaCacheType.GPU,
                                Aspect = TransformAspect.AspectFitFill,
                                EraseChangedContent = false, //keep small image shown while hi-res loads
                                LoadSourceOnFirstDraw = false,
                                HorizontalOptions = LayoutOptions.Fill,
                                VerticalOptions = LayoutOptions.Center,
                            }.Assign(out FullscreenImage).Adapt(me =>
                            {
                                //once the cached small image displayed, upgrade to hi-res
                                me.Success += (s, e) =>
                                {
                                    var upgrade = _fullscreenUpgradeUrl;
                                    _fullscreenUpgradeUrl = null;
                                    if (upgrade != null)
                                    {
                                        MainThread.BeginInvokeOnMainThread(() => me.Source = upgrade);
                                    }
                                };
                            }),

                            new SkiaLabel
                            {
                                Text = "Tap to close",
                                FontSize = 12,
                                TextColor = Color.FromArgb("#88FFFFFF"),
                                HorizontalOptions = LayoutOptions.Center,
                                VerticalOptions = LayoutOptions.End,
                                Margin = new Thickness(0, 0, 0, 70),
                                InputTransparent = true,
                            },
                        }
                    }.OnTapped(me => HideImageFullscreen()).Assign(out FullscreenOverlay),
                }
            },
        };
    }

}
