using Microsoft.Extensions.Logging;

namespace DrawnUi.Draw;

public class DrawnUiStartupSettings
{
    /// <summary>
    /// Will be used by Super.Log calls
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// For desktop: if set will affect the app window at startup.
    /// </summary>
    public WindowParameters? DesktopWindow { get; set; }

    /// <summary>
    /// Avoid safe insets and remove system ui like status bar etc if supported by platform
    /// </summary>
    public bool? MobileIsFullscreen { get; set; }

    /// <summary>
    /// Listen to keyboard keys with KeyboardManager. Available on supported MAUI platforms and Blazor.
    /// </summary>
    public bool UseDesktopKeyboard { get; set; }

    /// <summary>
    /// Android: draw hardware-accelerated canvases with Vulkan where the device supports it (Android 7+, Vulkan 1.1),
    /// with OpenGL ES where it does not or where Vulkan fails to start. Set false to always use OpenGL ES.
    /// </summary>
    public bool UseVulkan { get; set; } = true;

    /// <summary>
    /// Will be executed after DrawnUI is initialized and MAUI App is created. 
    /// </summary>
    public Action<IServiceProvider> Startup { get; set; }

}

