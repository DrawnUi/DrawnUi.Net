using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

            // Fonts/images are fetched over HTTP at startup; register a default
            // HttpClient unless the app already provided its own.
            builder.Services.TryAddScoped(sp =>
                new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            var host = builder.Build();

            Super.Services = host.Services;

            await SkiaFontManager.Instance.InitializeAsync(host.Services, cancellationToken);
            await SkiaImageManager.Instance.InitializeAsync(host.Services, cancellationToken);
            await SkiaSvg.InitializeAsync(cancellationToken);

            Super.Init();

            var jsRuntime = host.Services.GetService<IJSRuntime>();
            if (jsRuntime != null)
            {
                await Super.AttachNativeAppLifecycleAsync(jsRuntime);
            }

            if (settings?.UseDesktopKeyboard == true)
            {
                if (jsRuntime != null)
                {
                    await KeyboardManager.AttachToKeyboardAsync(jsRuntime);
                }
            }

            return host;
        }
    }
}
