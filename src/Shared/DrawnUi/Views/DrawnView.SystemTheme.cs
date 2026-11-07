using System.Runtime.InteropServices;

namespace DrawnUi.Views
{
    /// <summary>The system's light or dark appearance.</summary>
    public enum SystemTheme
    {
        Light,
        Dark
    }

    public partial class DrawnView
    {
        #region System theme

        /// <summary>
        /// The system's light or dark appearance, null until the head learns it. Same as DrawnUI for Rust's
        /// <c>Cx::system_theme</c> and DrawnUI for React's <c>Canvas.SystemTheme</c>.
        /// </summary>
        public SystemTheme? SystemTheme { get; private set; }

        /// <summary>
        /// Raised once when the head learns the system theme, then once per change; a repeated value is not raised.
        /// It asks for no frame: a handler that changes colors does that itself.
        /// </summary>
        public event EventHandler<SystemTheme> SystemThemeChanged;

        /// <summary>For heads: the system theme they learned or that changed.</summary>
        public void SetSystemTheme(SystemTheme theme)
        {
            if (SystemTheme == theme)
                return;
            SystemTheme = theme;
            SystemThemeChanged?.Invoke(this, theme);
        }

        /// <summary>
        /// Windows: the apps' theme from <c>AppsUseLightTheme</c> (Settings, Personalization, Colors); null elsewhere or
        /// when it cannot be read.
        /// </summary>
        public static SystemTheme? ReadWindowsSystemTheme()
        {
            if (!OperatingSystem.IsWindows())
                return null;
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int light ? light == 0 ? global::DrawnUi.Views.SystemTheme.Dark : global::DrawnUi.Views.SystemTheme.Light : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Windows: the window message that says the apps' theme may have changed (WM_SETTINGCHANGE "ImmersiveColorSet").</summary>
        public static bool IsWindowsThemeMessage(int msg, IntPtr lParam) =>
            msg == 0x001A && lParam != IntPtr.Zero && Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet";

        #endregion
    }
}
