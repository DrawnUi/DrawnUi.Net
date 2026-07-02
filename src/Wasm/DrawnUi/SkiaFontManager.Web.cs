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
        private Uri? _webBaseUri;

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

                _webBaseUri = baseUri;

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

        /// <summary>
        /// Downloads and registers a font at runtime, after startup initialization.
        /// Replaces any typeface already registered under the same alias, so an app can
        /// boot with a small subset font and stream the full font in the background.
        /// Reports download progress as 0..1 when the server provides Content-Length.
        /// Resolves relative sources against the base URL captured by <see cref="InitializeWebAsync"/>.
        /// </summary>
        public async Task<bool> LoadFontAsync(string alias, string sourceUrl,
            IProgress<float>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(sourceUrl))
                return false;

            try
            {
                var baseUri = _webBaseUri;
                using var httpClient = baseUri != null
                    ? new HttpClient { BaseAddress = baseUri }
                    : new HttpClient();

                var uri = ResolveUri(baseUri, sourceUrl);

                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                // Opt into fetch response streaming without taking a Blazor HTTP package dependency.
                request.Options.Set(new HttpRequestOptionsKey<bool>("WebAssemblyEnableStreamingResponse"), true);

                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? -1L;
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var buffer = new MemoryStream(total > 0 ? (int)total : 1024 * 1024);

                var chunk = new byte[64 * 1024];
                long received = 0;
                int read;
                while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
                {
                    buffer.Write(chunk, 0, read);
                    received += read;
                    if (total > 0)
                        progress?.Report((float)received / total);
                }

                using var data = SKData.CreateCopy(buffer.ToArray());
                var typeface = SKTypeface.FromData(data);
                if (typeface == null)
                {
                    Super.Log($"[DRAWNUI] LoadFontAsync failed to parse font {alias} from {sourceUrl}", Microsoft.Extensions.Logging.LogLevel.Warning);
                    return false;
                }

                await _loadSemaphore.WaitAsync(cancellationToken);
                try
                {
                    _fontSources[alias] = sourceUrl;
                    _fonts[alias] = typeface;
                }
                finally
                {
                    _loadSemaphore.Release();
                }

                progress?.Report(1f);
                return true;
            }
            catch (Exception e)
            {
                Super.Log(e);
                return false;
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
