namespace DrawnUi.Draw;

public interface IStylesCollection
{
    IStylesCollection AddStyle(Style style);
}

public class StylesCollection : IStylesCollection
{
    public IStylesCollection AddStyle(Style style)
    {
        Styles.Add(style);
        return this;
    }

    public static List<Style> Styles { get; } = new();
}

public interface IFontCollection
{
    IFontCollection AddFont(string source, string alias);
    IFontCollection AddFont(string source, string alias, FontWeight weight);
}

public class FontCollection : IFontCollection
{
    private readonly Uri? _baseUri;

    public FontCollection(string? baseUrl = null)
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

    public IFontCollection AddFont(string source, string alias)
    {
        SkiaFontManager.Instance.RegisterFont(alias, Resolve(source));
        return this;
    }

    public IFontCollection AddFont(string source, string alias, FontWeight weight)
    {
        SkiaFontManager.Instance.RegisterFont(alias, weight, Resolve(source));
        return this;
    }
}
