using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace DrawnUi.Draw;

public class DrawnUiBlazorBuilder
{
    private readonly WebAssemblyHostBuilder _hostBuilder;
    private DrawnUiStartupSettings? _settings;
    private string? _baseUrl;

    public DrawnUiBlazorBuilder(WebAssemblyHostBuilder hostBuilder)
    {
        _hostBuilder = hostBuilder;
    }

    public DrawnUiBlazorBuilder WithBaseUrl(string baseUrl)
    {
        _baseUrl = baseUrl;
        return this;
    }

    public DrawnUiBlazorBuilder WithOptions(Action<DrawnUiStartupSettings> configure)
    {
        _settings ??= new DrawnUiStartupSettings();
        configure(_settings);
        return this;
    }

    public DrawnUiBlazorBuilder ConfigureFonts(Action<IFontCollection> configure)
    {
        configure(new FontCollection(_baseUrl));
        return this;
    }

    public DrawnUiBlazorBuilder ConfigureStyles(Action<IStylesCollection> configure)
    {
        configure(new StylesCollection());

        return this;
    }

    public DrawnUiBlazorBuilder PreloadAssets(Action<IAssetCollection> configure)
    {
        configure(new AssetCollection(_baseUrl));
        return this;
    }

    public async Task BuildAndRunAsync(CancellationToken cancellationToken = default)
    {
        var host = await _hostBuilder.UseDrawnUiAsync(_settings, cancellationToken);
        await host.RunAsync();
    }
}
