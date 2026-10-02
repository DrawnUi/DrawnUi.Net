namespace DrawnUi.Draw
{
    public sealed partial class SkiaFontManager
    {
        public static SkiaFontManager Instance { get; } = new();

        public static SKTypeface DefaultTypeface => SKTypeface.CreateDefault();

        // Concurrent: fonts can be registered (or loaded on Initialize) while another thread renders and looks them up.
        // Reads stay lock-free; a plain Dictionary read during a write could return nothing or throw.
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SKTypeface> _fonts = new(StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _fontSources = new(StringComparer.OrdinalIgnoreCase);
        // Weights per family: replaced as a whole on change (copy on write), so a reader never sees a list being edited.
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int[]> _registeredWeights = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _registerLock = new();
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

            if (Initialized)
            {
                TryLoadFont(alias, sourceUrl);
            }
        }

        public void RegisterFont(string family, FontWeight weight, string sourceUrl)
        {
            RegisterWeight(family, weight);
            RegisterFont(GetAlias(family, weight), sourceUrl);
        }

        public void Initialize()
        {
            foreach (var source in _fontSources)
            {
                TryLoadFont(source.Key, source.Value);
            }

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
            if (systemFont != null && string.Equals(systemFont.FamilyName, alias, StringComparison.OrdinalIgnoreCase))
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
            var managerMatch = Manager.MatchCharacter(symbol);
            if (managerMatch != null && managerMatch != DefaultTypeface)
            {
                return managerMatch;
            }

            var text = char.ConvertFromUtf32(symbol);
            foreach (var typeface in Instance._fonts.Values) // a snapshot: safe while fonts are added
            {
                var glyphs = typeface?.GetGlyphs(text);
                if (glyphs != null && glyphs.Any(glyph => glyph != 0))
                {
                    return typeface;
                }
            }

            return managerMatch ?? DefaultTypeface;
        }

        public static void RegisterWeight(string alias, FontWeight weight)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return;
            }

            var value = (int)weight;
            lock (Instance._registerLock)
            {
                var list = Instance._registeredWeights.TryGetValue(alias, out var existing) ? existing : Array.Empty<int>();
                if (Array.IndexOf(list, value) < 0)
                    Instance._registeredWeights[alias] = [..list, value];
            }
        }

        public static string GetRegisteredAlias(string alias, int weight)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return alias;
            }

            if (Instance._registeredWeights.TryGetValue(alias, out var registeredWeights) && registeredWeights.Length > 0)
            {
                var closestRegisteredWeight = registeredWeights[0]; // the first of equally close weights, as OrderBy gave
                for (var i = 1; i < registeredWeights.Length; i++)
                {
                    if (Math.Abs(registeredWeights[i] - weight) < Math.Abs(closestRegisteredWeight - weight))
                        closestRegisteredWeight = registeredWeights[i];
                }
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

        private void TryLoadFont(string alias, string sourceUrl)
        {
            lock (_registerLock) // one load per alias when two threads register or initialize at once
                TryLoadFontLocked(alias, sourceUrl);
        }

        private void TryLoadFontLocked(string alias, string sourceUrl)
        {
            if (_fonts.ContainsKey(alias))
                return;

            var path = sourceUrl;
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return;

            if (!Path.IsPathRooted(path))
                path = Path.Combine(AppContext.BaseDirectory, path);

            if (!File.Exists(path))
                return;

            var typeface = SKTypeface.FromFile(path);
            if (typeface != null)
                _fonts[alias] = typeface;
        }
    }
}
