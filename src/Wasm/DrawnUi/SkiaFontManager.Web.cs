using System.Runtime.Versioning;
using SkiaSharp;

namespace DrawnUi.Draw
{
    /// <summary>
    /// Pure-WebAssembly completion of <see cref="SkiaFontManager"/>.
    /// SharedNet's synchronous <c>Initialize()</c> loads fonts via <see cref="SKTypeface.FromFile"/>
    /// from the Mono VFS, but <c>WasmFilesToBundle</c> is a no-op in the .NET WASM SDK, so no font
    /// ever reaches the VFS. This mirrors the Blazor path: fetch each registered font over HTTP
    /// (served as a normal static web asset) and parse it via <see cref="SKTypeface.FromData"/>.
    /// </summary>
    [SupportedOSPlatform("browser")]
    public sealed partial class SkiaFontManager
    {
        /// <summary>
        /// Downloads and registers all fonts added via <c>ConfigureFonts</c> using HttpClient,
        /// resolving relative sources against <paramref name="baseUrl"/> (the document base URL).
        /// Safe to call after the synchronous <see cref="Initialize"/>: already-loaded aliases are skipped.
        /// </summary>
        public async Task InitializeWebAsync(string? baseUrl, CancellationToken cancellationToken = default)
        {
            if (_fontSources.Count == 0)
            {
                Initialized = true;
                return;
            }

            await _loadSemaphore.WaitAsync(cancellationToken);
            try
            {
                Uri? baseUri = null;
                if (!string.IsNullOrWhiteSpace(baseUrl))
                    Uri.TryCreate(baseUrl, UriKind.Absolute, out baseUri);

                using var httpClient = baseUri != null
                    ? new HttpClient { BaseAddress = baseUri }
                    : new HttpClient();

                var pending = _fontSources.Where(source => !_fonts.ContainsKey(source.Key)).ToList();

                // Download in parallel (fetch-backed), parse sequentially (CPU-bound).
                var downloads = await Task.WhenAll(pending.Select(async source =>
                {
                    try
                    {
                        var uri = ResolveUri(baseUri, source.Value);
                        var bytes = await httpClient.GetByteArrayAsync(uri, cancellationToken);
                        return (source.Key, source.Value, Bytes: bytes);
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                        return (source.Key, source.Value, Bytes: (byte[]?)null);
                    }
                }));

                foreach (var download in downloads)
                {
                    if (download.Bytes == null)
                        continue;

                    using var data = SKData.CreateCopy(download.Bytes);
                    var typeface = SKTypeface.FromData(data);
                    if (typeface != null)
                        _fonts[download.Key] = typeface;
                    else
                        Super.Log($"[DRAWNUI] Web font preload failed to parse {download.Key} from {download.Item2}",
                            Microsoft.Extensions.Logging.LogLevel.Warning);
                }

                Initialized = true;
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }

        private static Uri ResolveUri(Uri? baseUri, string source)
        {
            if (Uri.TryCreate(source, UriKind.Absolute, out var absolute))
                return absolute;
            return baseUri != null ? new Uri(baseUri, source) : new Uri(source, UriKind.Relative);
        }
    }
}
