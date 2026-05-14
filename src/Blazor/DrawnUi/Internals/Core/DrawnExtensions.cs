using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

namespace DrawnUi.Draw
{
    public static partial class DrawnExtensions
    {
        /// <summary>
        /// Blazor WebAssembly host bootstrap: preload registered fonts/images
        /// and run <see cref="Super.Init"/> before app startup.
        /// </summary>
        public static async Task<WebAssemblyHost> UseDrawnUiAsync(this WebAssemblyHostBuilder builder,
            DrawnUiStartupSettings settings = null,
            CancellationToken cancellationToken = default)
        {
            StartupSettings = settings;

            var host = builder.Build();

            Super.Services = host.Services;

            await SkiaFontManager.Instance.InitializeAsync(host.Services, cancellationToken);
            await SkiaImageManager.Instance.InitializeAsync(host.Services, cancellationToken);

            Super.Init();

            if (settings?.UseDesktopKeyboard == true)
            {
                var jsRuntime = host.Services.GetService<IJSRuntime>();
                if (jsRuntime != null)
                {
                    await KeyboardManager.AttachToKeyboardAsync(jsRuntime);
                }
            }

            return host;
        }
    }
}
