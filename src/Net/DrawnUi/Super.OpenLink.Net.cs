using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DrawnUi.Draw
{
    public partial class Super
    {
        /// <summary>
        /// Opens <paramref name="link"/> in the system's default browser (or the app registered for its scheme), as
        /// Super.OpenLink does on the MAUI and web heads. An empty link does nothing.
        /// </summary>
        public static void OpenLink(string link)
        {
            if (string.IsNullOrEmpty(link))
                return;
            try
            {
                Process.Start(new ProcessStartInfo(link) { UseShellExecute = true })?.Dispose();
            }
            catch (Exception e)
            {
                Log(e.Message, LogLevel.Error);
            }
        }
    }
}
