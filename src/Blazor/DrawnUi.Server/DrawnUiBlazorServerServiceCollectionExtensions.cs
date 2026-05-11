using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DrawnUi.Blazor.Server;

public static class DrawnUiBlazorServerServiceCollectionExtensions
{
    public static IServiceCollection AddDrawnUiBlazorServer(
        this IServiceCollection services,
        Action<DrawnUiBlazorServerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<DrawnUiBlazorServerOptions>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<IDrawnUiServerFrameEncoder, PngDrawnUiServerFrameEncoder>();
        services.TryAddSingleton<IDrawnUiServerRenderer, DrawnUiServerRenderer>();

        return services;
    }
}