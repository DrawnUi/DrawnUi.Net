using System.Windows;
using System.Windows.Input;
using DrawnUi.Draw;

namespace DrawnUi.Wpf;

/// <summary>
/// Startup settings for the WPF head, the same <see cref="DrawnUiStartupSettings"/> a MAUI app passes to
/// <c>UseDrawnUi</c>:
/// <code>
/// Super.UseDrawnUi()
///     .ConfigureFonts(...)
///     .WithSettings(new DrawnUiStartupSettings
///     {
///         DesktopWindow = new WindowParameters { Width = 500, Height = 800, IsFixedSize = true },
///         UseDesktopKeyboard = true,
///         Logger = logger,
///         Startup = services => { ... },
///     })
///     .Build();
/// </code>
/// </summary>
public static class DrawnUiBuilderExtensions
{
    /// <summary>
    /// Applies startup settings to the WPF head. <c>DesktopWindow</c> sizes the window that hosts the
    /// first <see cref="DrawnUiElement"/> (device-independent pixels, <c>IsFixedSize</c> = no resizing);
    /// <c>UseDesktopKeyboard</c> feeds every key pressed in that window to <see cref="KeyboardManager"/>,
    /// whatever has focus, as on the MAUI desktop heads; <c>Logger</c> receives <c>Super.Log</c>;
    /// <c>Startup</c> runs once, with <c>Super.Services</c>, when the first element initializes DrawnUI.
    /// <c>MobileIsFullscreen</c> has no meaning here and is ignored.
    /// </summary>
    public static DrawnUiBuilder WithSettings(this DrawnUiBuilder builder, DrawnUiStartupSettings settings)
    {
        DrawnExtensions.StartupSettings = settings;
        WpfStartup.Reset();
        return builder;
    }
}

/// <summary>Applies <see cref="DrawnExtensions.StartupSettings"/> at the moments the WPF head reaches them.</summary>
internal static class WpfStartup
{
    private static bool _startupRan;
    private static Window? _window;

    /// <summary>Forgets what was applied, so new settings apply again.</summary>
    public static void Reset()
    {
        _startupRan = false;
        _window = null;
    }

    /// <summary>Runs the <c>Startup</c> action once, after DrawnUI is initialized.</summary>
    public static void RunStartup()
    {
        if (_startupRan)
            return;

        _startupRan = true;
        DrawnExtensions.StartupSettings?.Startup?.Invoke(Super.Services);
    }

    /// <summary>
    /// Sizes the window and attaches the window-level keyboard, once, for the window hosting the
    /// first element.
    /// </summary>
    public static void ApplyToWindow(Window window)
    {
        var settings = DrawnExtensions.StartupSettings;
        if (settings == null || _window != null)
            return;

        _window = window;

        if (settings.DesktopWindow is { } size)
        {
            window.SizeToContent = SizeToContent.Manual;
            window.Width = size.Width;
            window.Height = size.Height;
            if (size.IsFixedSize)
                window.ResizeMode = ResizeMode.NoResize;
        }

        if (settings.UseDesktopKeyboard)
        {
            // A focused DrawnUiElement feeds the manager itself; everything else in the window
            // goes through here. Events are not marked handled, WPF controls keep their keys.
            window.PreviewKeyDown += (_, e) =>
            {
                if (e.OriginalSource is DrawnUiElement || e.IsRepeat)
                    return;
                KeyboardManager.KeyboardPressed(KeyboardManager.MapKey(e.Key == Key.System ? e.SystemKey : e.Key));
            };
            window.PreviewKeyUp += (_, e) =>
            {
                if (e.OriginalSource is DrawnUiElement)
                    return;
                KeyboardManager.KeyboardReleased(KeyboardManager.MapKey(e.Key == Key.System ? e.SystemKey : e.Key));
            };
            window.PreviewTextInput += (_, e) =>
            {
                if (e.OriginalSource is DrawnUiElement)
                    return;
                var text = e.Text;
                if (string.IsNullOrEmpty(text) || text == "\r" || text == "\n" || text == "\t")
                    return;
                if (text.Length == 1 && char.IsControl(text[0]))
                    return;
                KeyboardManager.KeyboardChar(text);
            };
        }
    }
}
