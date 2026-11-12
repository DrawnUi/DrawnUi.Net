using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Add3dGallery;

/// <summary>The WPF host of the shared app (src/Shared/Samples/Add3dGallery.Shared): one canvas filling the window.</summary>
public partial class MainWindow : Window
{
    /// <summary>Creates the window and mounts the gallery. ADD3D_PERF=mobs (scroll, globe...) measures it, results in the console.</summary>
    public MainWindow()
    {
        InitializeComponent();
        var root = new GalleryRoot("Add3D addon to DrawnUI for WPF");
        Drawn.Canvas.BackgroundColor = Theme.Page;
        Drawn.Canvas.Content = root;
        var perf = Environment.GetEnvironmentVariable("ADD3D_PERF");
        if (!string.IsNullOrEmpty(perf))
            PerfProbe.Start(root.Gallery, "perf=" + perf);
        SourceInitialized += (_, _) => DarkTitleBar();
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>A dark title bar in the header's color (Windows 11; Windows 10 gets the dark mode only).</summary>
    void DarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
        var c = Theme.Header;
        var color = (int)(c.Red * 255) | (int)(c.Green * 255) << 8 | (int)(c.Blue * 255) << 16; // COLORREF 0x00BBGGRR
        DwmSetWindowAttribute(hwnd, 35, ref color, sizeof(int)); // DWMWA_CAPTION_COLOR
        DwmSetWindowAttribute(hwnd, 34, ref color, sizeof(int)); // DWMWA_BORDER_COLOR
    }
}
