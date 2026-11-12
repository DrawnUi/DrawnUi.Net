using System.Globalization;
using System.Reflection;
using System.Runtime.Versioning;
using DrawnUi.OpenTk;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using Add3dGallery;

// Prices and numbers read the same on every machine ("$1,190", "0.5").
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

Super.UseDrawnUi()
    .ConfigureFonts(fonts => fonts
        .AddFont("fonts/OpenSans-Regular.ttf", "FontText")
        .AddFont("fonts/OpenSans-Semibold.ttf", "FontTextTitle"))
    // Labels without a FontFamily draw in FontText.
    .ConfigureStyles(styles => styles.AddStyle(new Style
    {
        ApplyToDerivedTypes = true,
        TargetType = typeof(SkiaLabel),
        Setters = { new Setter { Property = SkiaLabel.FontFamilyProperty, Value = "FontText" } },
    }))
    .Build();

var root = new GalleryRoot("The Add3d add-on for OpenTK");
// ADD3D_PERF=mobs (globe, globechip...) measures the gallery; results go to the console.
var perf = Environment.GetEnvironmentVariable("ADD3D_PERF");
if (!string.IsNullOrEmpty(perf))
    PerfProbe.Start(root.Gallery, "perf=" + perf);

var canvas = new Canvas
{
    BackgroundColor = Theme.Page,
    RenderingMode = RenderingModeType.Accelerated,
    // an app: draws while something moves (3D models ask for frames while they turn or play) and sleeps when still
    UpdateMode = UpdateModeType.Dynamic,
    Gestures = GesturesMode.Enabled,
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    Content = root,
};

var nativeSettings = new NativeWindowSettings
{
    ClientSize = new Vector2i(980, 820),
    Title = "DrawnUI 3D for OpenTK",
    API = ContextAPI.OpenGL,
    Profile = ContextProfile.Core,
    // Windows has GL 4.6; Mesa (Linux) gives 3.3 and macOS 4.1, which also needs a forward-compatible core context
    APIVersion = OperatingSystem.IsWindows() ? new Version(4, 6) : new Version(3, 3),
    Flags = OperatingSystem.IsMacOS() ? ContextFlags.ForwardCompatible : ContextFlags.Default,
    WindowState = WindowState.Normal,
    Icon = GalleryWindow.LoadIcon(),
};

using var window = new GalleryWindow(new GameWindowSettings { UpdateFrequency = 0 }, nativeSettings, canvas);
window.Run();

namespace Add3dGallery
{
    /// <summary>The window: title bar in the page's color, keys to the KeyboardManager (the boss fight, the form).</summary>
    public class GalleryWindow(GameWindowSettings gameSettings, NativeWindowSettings nativeSettings, Canvas canvas)
        : DrawnUiWindow(gameSettings, nativeSettings, canvas)
    {
        /// <inheritdoc/>
        [SupportedOSPlatform("windows")]
        protected override void ConfigureWindowChrome(IntPtr hwnd)
        {
            var c = Theme.Header;
            WindowChrome.SetCaptionColor(hwnd, (byte)(c.Red * 255), (byte)(c.Green * 255), (byte)(c.Blue * 255));
            WindowChrome.SetBorderColor(hwnd, (byte)(c.Red * 255), (byte)(c.Green * 255), (byte)(c.Blue * 255));
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!e.IsRepeat && OpenTkKeyMapper.Map(e.Key) is { } key)
                KeyboardManager.KeyboardPressed(key);
        }

        /// <inheritdoc/>
        protected override void OnKeyUp(KeyboardKeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (OpenTkKeyMapper.Map(e.Key) is { } key)
                KeyboardManager.KeyboardReleased(key);
        }

        /// <summary>The DrawnUI icon for the title bar, from the embedded icon.ico (32 x 32, RGBA).</summary>
        public static WindowIcon LoadIcon()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Add3dGallery.icon.ico");
            using var bitmap = stream == null ? null : SKBitmap.Decode(stream);
            if (bitmap == null)
                return null;
            using var resized = bitmap.Resize(new SKImageInfo(32, 32), new SKSamplingOptions(SKCubicResampler.Mitchell));
            var pixels = resized.Bytes;
            // SKBitmap is BGRA, OpenTK wants RGBA
            for (var i = 0; i < pixels.Length; i += 4)
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
            return new WindowIcon(new OpenTK.Windowing.Common.Input.Image(32, 32, pixels));
        }
    }
}
