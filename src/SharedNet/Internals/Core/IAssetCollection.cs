namespace DrawnUi.Draw;

public interface IAssetCollection
{
    IAssetCollection AddImage(string source);
    IAssetCollection AddImage(string alias, string source);
    IAssetCollection AddSvg(string source);
}

public class AssetCollection : IAssetCollection
{
    private readonly Uri? _baseUri;

    public AssetCollection(string? baseUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
            _baseUri = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + '/');
    }

    private string Resolve(string source)
    {
        if (_baseUri == null || source.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return source;
        return new Uri(_baseUri, source).ToString();
    }

    public IAssetCollection AddImage(string source)
    {
        SkiaImageManager.Instance.RegisterImage(Resolve(source));
        return this;
    }

    public IAssetCollection AddImage(string alias, string source)
    {
        SkiaImageManager.Instance.RegisterImage(alias, Resolve(source));
        return this;
    }

    public IAssetCollection AddSvg(string source)
    {
        SkiaSvg.RegisterSource(Resolve(source));
        return this;
    }
}
