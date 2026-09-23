using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;

namespace HelloMaui.Pages;

/// <summary>
/// SkiaSprite (FastRepro SpriteTestPage) and a SkiaSpriteSet warrior on a tile board moved with the
/// keyboard (SpriteSnappedBoardPage). Ported from the React demo's SpritesPage.tsx.
/// </summary>
public class SpritesPage : SkiaLayer
{
    private const int Tile = 64;
    private const int Cols = 9;
    private const int Rows = 4;

    private SkiaSprite _sprite;
    private SkiaLabel _spriteTitle;
    private SkiaLabel _boardTitle;
    private SkiaButton _playButton;
    private readonly List<SkiaButton> _fpsButtons = new();
    private WarriorSprite _player;
    private string _info = "loading…";
    private double _fps = 15;
    private bool _playing = true;
    private (int Col, int Row) _pos = (1, 1);
    private bool _moving;
    private string _facing = "Right";

    /// <summary>Builds the page and mounts the warrior.</summary>
    public SpritesPage()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;

        Children = new List<SkiaControl>
        {
            new SkiaScroll
            {
                Orientation = ScrollOrientation.Vertical,
                Content = new SkiaStack
                {
                    Spacing = 16,
                    Padding = new Thickness(16),
                    HorizontalOptions = LayoutOptions.Center,
                    MaximumWidthRequest = 720,
                    Children = new List<SkiaControl>
                    {
                        new SkiaLabel("Sprites") { FontSize = 24, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center },

                        Card(Title(SpriteTitle()).Assign(out _spriteTitle),
                            new SkiaRow
                            {
                                Spacing = 16,
                                HorizontalOptions = LayoutOptions.Fill,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaSprite { Source = "anims/BlueWarrior/Warrior_Idle.png", Columns = 8, Rows = 1, FramesPerSecond = 15, Repeat = -1, WidthRequest = 160, HeightRequest = 160, BackgroundColor = Color.Parse("#212529"), UseCache = SkiaCacheType.Image }
                                        .Assign(out _sprite)
                                        .Adapt(me =>
                                        {
                                            // no Success / Error events on SkiaSprite: Started fires once the sheet is cut into frames
                                            me.Started += (_, _) => { _info = $"{me.TotalFrames} frames · {me.FrameWidth}×{me.FrameHeight} px · {Math.Round(me.DurationMs)} ms"; _spriteTitle.Text = SpriteTitle(); };
                                        }),
                                    new SkiaSprite { Source = "anims/RedWarrior/Warrior_Attack1.png", Columns = 4, Rows = 1, FramesPerSecond = 8, Repeat = -1, WidthRequest = 160, HeightRequest = 160, BackgroundColor = Color.Parse("#212529"), UseCache = SkiaCacheType.Image },
                                    new SkiaSprite { Source = "anims/Trees/Tree1.png", Columns = 8, Rows = 1, FramesPerSecond = 6, Repeat = -1, WidthRequest = 160, HeightRequest = 160, BackgroundColor = Color.Parse("#212529"), UseCache = SkiaCacheType.Image },
                                    new SkiaStack
                                    {
                                        Spacing = 8,
                                        VerticalOptions = LayoutOptions.Center,
                                        HorizontalOptions = LayoutOptions.Fill,
                                        Children = new List<SkiaControl>
                                        {
                                            new SkiaWrap
                                            {
                                                Spacing = 8,
                                                Children = new List<SkiaControl>
                                                {
                                                    new SkiaButton("Pause") { BackgroundColor = Color.Parse("#0D6EFD"), FontSize = 13 }.Assign(out _playButton).OnTapped(me => TogglePlay()),
                                                }
                                                .Concat(new[] { 5.0, 15, 30 }.Select(v => (SkiaControl)new SkiaButton($"{v} fps") { FontSize = 13 }
                                                    .Adapt(b => _fpsButtons.Add(b))
                                                    .OnTapped(me => SetFps(v))))
                                                .Concat(new[]
                                                {
                                                    (SkiaControl)new SkiaButton("Seek(0)") { BackgroundColor = Color.Parse("#495057"), FontSize = 13 }.OnTapped(me => { _sprite.Stop(); _sprite.Seek(0); _playing = false; _playButton.Text = "Play"; }),
                                                })
                                                .ToList(),
                                            },
                                            new SkiaLabel("Frames are cut from the sheet by Columns × Rows, transparent borders trimmed per frame (SpriteFrameImage), nearest sampling; the animator runs 0..DurationMs and picks the frame by time.")
                                            {
                                                FontSize = 12, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill,
                                            },
                                        },
                                    },
                                },
                            }),

                        Card(Title(BoardTitle()).Assign(out _boardTitle),
                            new SkiaLayer
                            {
                                WidthRequest = Cols * Tile,
                                HeightRequest = Rows * Tile,
                                BackgroundColor = Color.Parse("#1B4332"),
                                HorizontalOptions = LayoutOptions.Center,
                                IsClippedToBounds = true,
                                Children = Enumerable.Range(0, Cols * Rows).Select(i => (SkiaControl)new SkiaShape
                                {
                                    Type = ShapeType.Rectangle,
                                    WidthRequest = Tile,
                                    HeightRequest = Tile,
                                    BackgroundColor = Color.Parse(((i % Cols) + i / Cols) % 2 == 0 ? "#2D6A4F" : "#40916C"),
                                    Margin = new Thickness((i % Cols) * Tile, (i / Cols) * Tile, 0, 0),
                                })
                                .Concat(new SkiaControl[]
                                {
                                    new SkiaSprite { Source = "anims/Trees/Tree1.png", Columns = 8, Rows = 1, FramesPerSecond = 6, Repeat = -1, WidthRequest = Tile, HeightRequest = Tile, Margin = new Thickness(5 * Tile, 0, 0, 0), UseCache = SkiaCacheType.Image },
                                    new SkiaSprite { Source = "anims/Trees/Tree2.png", Columns = 8, Rows = 1, FramesPerSecond = 5, Repeat = -1, WidthRequest = Tile, HeightRequest = Tile, Margin = new Thickness(7 * Tile, 2 * Tile, 0, 0), UseCache = SkiaCacheType.Image },
                                    new SkiaSprite { Source = "anims/RedWarrior/Warrior_Idle.png", Columns = 8, Rows = 1, FramesPerSecond = 15, Repeat = -1, WidthRequest = Tile, HeightRequest = Tile, Margin = new Thickness(6 * Tile, 3 * Tile, 0, 0), ScaleX = -1, UseCache = SkiaCacheType.Image, ZIndex = 9 },
                                    // the player: a code-behind SkiaSpriteSet subclass
                                    new WarriorSprite("Blue") { WidthRequest = Tile, HeightRequest = Tile, ZIndex = 10, TranslationX = 1 * Tile, TranslationY = 1 * Tile }.Assign(out _player),
                                })
                                .ToList(),
                            },
                            new SkiaWrap
                            {
                                Spacing = 8,
                                HorizontalOptions = LayoutOptions.Center,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("Left") { BackgroundColor = Color.Parse("#0F3460"), FontSize = 13 }.OnTapped(me => _ = Move(-1, 0)),
                                    new SkiaButton("Up") { BackgroundColor = Color.Parse("#0F3460"), FontSize = 13 }.OnTapped(me => _ = Move(0, -1)),
                                    new SkiaButton("Down") { BackgroundColor = Color.Parse("#0F3460"), FontSize = 13 }.OnTapped(me => _ = Move(0, 1)),
                                    new SkiaButton("Right") { BackgroundColor = Color.Parse("#0F3460"), FontSize = 13 }.OnTapped(me => _ = Move(1, 0)),
                                    new SkiaButton("Attack (Space)") { BackgroundColor = Color.Parse("#D63384"), FontSize = 13 }.OnTapped(me => Attack()),
                                },
                            },
                            new SkiaLabel("WarriorSprite extends SkiaSpriteSet: Define(0 idle, 1 run, 2 attack) with the FastRepro sheets; WState maps to State and mirrors CurrentSprite.ScaleX; the move is a TranslateToAsync to the target tile while the walk state plays. Arrows / WASD move, Space attacks (KeyboardManager).")
                            {
                                FontSize = 12, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill,
                            }),
                    },
                },
            }.Fill(),
        };

        SetFps(15);
        KeyboardManager.KeyDown += OnKeyDown;
    }

    /// <inheritdoc/>
    public override void OnDisposing()
    {
        KeyboardManager.KeyDown -= OnKeyDown;
        base.OnDisposing();
    }

    private void OnKeyDown(object sender, InputKey key)
    {
        switch (key)
        {
            case InputKey.ArrowLeft or InputKey.KeyA: _ = Move(-1, 0); break;
            case InputKey.ArrowRight or InputKey.KeyD: _ = Move(1, 0); break;
            case InputKey.ArrowUp or InputKey.KeyW: _ = Move(0, -1); break;
            case InputKey.ArrowDown or InputKey.KeyS: _ = Move(0, 1); break;
            case InputKey.Space: Attack(); break;
        }
    }

    private string SpriteTitle() => $"SkiaSprite — Source=\"anims/BlueWarrior/Warrior_Idle.png\" Columns=8 Rows=1 · {_info}";

    private string BoardTitle() => $"SkiaSpriteSet warrior on a tile board — arrows / WASD move, Space attacks · tile {_pos.Col},{_pos.Row} · {_player?.WState}";

    private void TogglePlay()
    {
        if (_sprite.IsPlaying)
        {
            _sprite.Stop();
            _playing = false;
        }
        else
        {
            _sprite.Start();
            _playing = true;
        }

        _playButton.Text = _playing ? "Pause" : "Play";
    }

    private void SetFps(double fps)
    {
        _fps = fps;
        _sprite.FramesPerSecond = fps;
        var values = new[] { 5.0, 15, 30 };
        for (var i = 0; i < _fpsButtons.Count; i++)
            _fpsButtons[i].BackgroundColor = Color.Parse(values[i] == fps ? "#533483" : "#495057");
    }

    private async Task Move(int dx, int dy)
    {
        if (_moving)
            return;

        var target = (Col: Math.Clamp(_pos.Col + dx, 0, Cols - 1), Row: Math.Clamp(_pos.Row + dy, 0, Rows - 1));
        if (dx != 0)
            _facing = dx > 0 ? "Right" : "Left";

        if (target == _pos)
        {
            SetState($"Idle{_facing}");
            return;
        }

        _moving = true;
        SetState($"Walk{_facing}");
        await _player.TranslateToAsync(target.Col * Tile, target.Row * Tile, 220, Easing.Linear);
        _pos = target;
        _moving = false;
        SetState($"Idle{_facing}");
    }

    private async void Attack()
    {
        if (_moving)
            return;

        SetState($"War{_facing}");
        await Task.Delay(500);
        SetState($"Idle{_facing}");
    }

    private void SetState(string state)
    {
        _player.WState = Enum.Parse<WarriorAnimState>(state);
        _boardTitle.Text = BoardTitle();
    }

    private static SkiaLabel Title(string text) => new(text)
    {
        FontSize = 12, TextColor = Color.Parse("#6EA8FE"), FontAttributes = FontAttributes.Bold, TextTransform = TextTransform.Uppercase, HorizontalOptions = LayoutOptions.Fill,
    };

    private static SkiaControl Card(SkiaLabel title, params SkiaControl[] content) => new SkiaShape
    {
        Type = ShapeType.Rectangle,
        CornerRadius = 8,
        BackgroundColor = Color.Parse("#2B3035"),
        HorizontalOptions = LayoutOptions.Fill,
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 10,
                Padding = new Thickness(16, 12),
                HorizontalOptions = LayoutOptions.Fill,
                Children = new SkiaControl[] { title }.Concat(content).ToList(),
            },
        },
    };
}
