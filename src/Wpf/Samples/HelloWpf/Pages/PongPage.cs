using System.Windows;
using DrawnUi.Draw;
using Pong.Game;
using DrawnUi.Views;
using Color = DrawnUi.Color;
using Thickness = DrawnUi.Views.Thickness;

namespace HelloWpf.Pages;

/// <summary>
/// Pong, ported from the React demo's PongPage.tsx: the shared Pong.Shared game (the same code the MAUI,
/// WPF, OpenTK, Blazor and pure-WASM Pong samples host), a DrawnGame with a game loop, sprites moved by
/// Left / Top, an AI paddle, keyboard and touch input. The 360x640 field is fitted to the page by a
/// <see cref="RescalingLayout"/> (rendering scale, not a transform).
/// </summary>
public class PongPage : SkiaLayer
{
    private PongGame _game;
    private Window _window;

    /// <summary>Builds the page and starts the game.</summary>
    public PongPage()
    {
        BackgroundColor = Color.Parse("#0A0F1E");
        VerticalOptions = LayoutOptions.Fill;

        Children = new List<SkiaControl>
        {
            new SkiaLabel("← → or drag to move, tap / Space to serve · first to 7")
            {
                FontFamilyFallback = "FontSymbols,FontSymbols2",
                FontSize = 13,
                TextColor = Color.Parse("#ADB5BD"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(12, 8, 12, 0),
            },
            new RescalingLayout
            {
                LogicalWidth = PongGame.WIDTH,
                LogicalHeight = PongGame.HEIGHT,
                Margin = new Thickness(0, 36, 0, 0),
                Children = new List<SkiaControl>
                {
                    new PongGame
                    {
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                    }.Assign(out _game),
                },
            },
        };

        // A minimized window gets no frames: pause, and Resume() restarts the frame clock, else the first
        // frame back carries the whole hidden time as its delta (the React page does it on visibilitychange).
        _window = Application.Current?.MainWindow;
        if (_window != null)
            _window.StateChanged += OnWindowStateChanged;
    }

    private void OnWindowStateChanged(object sender, EventArgs e)
    {
        if (_window.WindowState == WindowState.Minimized)
            _game.Pause();
        else if (_game.IsPaused)
            _game.Resume();
    }

    /// <inheritdoc/>
    public override void OnDisposing()
    {
        if (_window != null)
            _window.StateChanged -= OnWindowStateChanged;
        _window = null;

        base.OnDisposing();
    }
}
