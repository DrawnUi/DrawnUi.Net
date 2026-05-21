namespace DrawnUi.Draw
{
    public sealed partial class SkiaFontManager
    {
        public static SkiaFontManager Instance { get; } = new();

        public static SKTypeface DefaultTypeface => SKTypeface.CreateDefault();

        private readonly Dictionary<string, SKTypeface> _fonts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _fontSources = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<int>> _registeredWeights = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _loadSemaphore = new(1, 1);
        private static SKFontManager _manager;

        public bool Initialized { get; private set; }

        public static SKFontManager Manager => _manager ??= SKFontManager.CreateDefault();

        public void RegisterFont(string alias, string sourceUrl)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                throw new ArgumentException("Font alias cannot be empty.", nameof(alias));
            }

            if (string.IsNullOrWhiteSpace(sourceUrl))
            {
                throw new ArgumentException("Font source URL cannot be empty.", nameof(sourceUrl));
            }

            _fontSources[alias] = sourceUrl;
        }

        public void RegisterFont(string family, FontWeight weight, string sourceUrl)
        {
            RegisterWeight(family, weight);
            RegisterFont(GetAlias(family, weight), sourceUrl);
        }

        public void Initialize()
        {
            Initialized = true;
        }

        public SKTypeface GetFont(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return DefaultTypeface;
            }

            if (_fonts.TryGetValue(alias, out var registeredFont))
            {
                return registeredFont;
            }

            var systemFont = SKTypeface.FromFamilyName(alias);
            if (systemFont != null)
            {
                return systemFont;
            }

            return DefaultTypeface;
        }

        public SKTypeface GetFont(string family, int weight)
        {
            if (string.IsNullOrWhiteSpace(family))
            {
                return DefaultTypeface;
            }

            var weightedAlias = GetRegisteredAlias(family, weight);

            var font = GetFont(weightedAlias);
            if (font != SKTypeface.Default)
            {
                return font;
            }

            if (!string.Equals(weightedAlias, family, StringComparison.OrdinalIgnoreCase))
            {
                font = GetFont(family);
                if (font != SKTypeface.Default)
                {
                    return font;
                }
            }

            return DefaultTypeface;
        }

        public static SKTypeface MatchCharacter(int symbol)
        {
            var text = char.ConvertFromUtf32(symbol);
            foreach (var typeface in Instance._fonts.Values)
            {
                var glyphs = typeface?.GetGlyphs(text);
                if (glyphs != null && glyphs.Any(glyph => glyph != 0))
                {
                    return typeface;
                }
            }

            return Manager.MatchCharacter(symbol) ?? DefaultTypeface;
        }

        public static void RegisterWeight(string alias, FontWeight weight)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return;
            }

            if (!Instance._registeredWeights.TryGetValue(alias, out var list))
            {
                list = new List<int>();
                Instance._registeredWeights[alias] = list;
            }

            var value = (int)weight;
            if (!list.Contains(value))
            {
                list.Add(value);
            }
        }

        public static string GetRegisteredAlias(string alias, int weight)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return alias;
            }

            if (Instance._registeredWeights.TryGetValue(alias, out var registeredWeights) && registeredWeights.Count > 0)
            {
                var closestRegisteredWeight = registeredWeights.OrderBy(value => Math.Abs(value - weight)).First();
                return GetAlias(alias, GetWeightEnum(closestRegisteredWeight));
            }

            return alias;
        }

        public static FontWeight GetWeightEnum(int weight)
        {
            var fontWeights = (FontWeight[])Enum.GetValues(typeof(FontWeight));
            return fontWeights
                .Select(value => new { Value = value, Difference = Math.Abs((int)value - weight) })
                .OrderBy(item => item.Difference)
                .First()
                .Value;
        }

        public static string GetAlias(string alias, FontWeight weight)
        {
            return string.IsNullOrEmpty(alias) ? alias : $"{alias}{weight}";
        }
    }
}
