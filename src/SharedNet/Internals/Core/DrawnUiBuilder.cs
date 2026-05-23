namespace DrawnUi.Draw;

public class DrawnUiBuilder
{
    public DrawnUiBuilder ConfigureFonts(Action<IFontCollection> configure)
    {
        configure(new FontCollection());
        return this;
    }

    public DrawnUiBuilder PreloadAssets(Action<IAssetCollection> configure)
    {
        configure(new AssetCollection());
        return this;
    }

    public void Build()
    {
        SkiaFontManager.Instance.Initialize();
    }

    public async Task BuildAsync(CancellationToken cancellationToken = default)
    {
        SkiaFontManager.Instance.Initialize();
#if !BROWSER
        await SkiaImageManager.Instance.InitializeAsync(cancellationToken);
        await SkiaSvg.InitializeAsync(cancellationToken);
#else
        await Task.CompletedTask;
#endif
    }
}
