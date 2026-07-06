using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace DrawnUi.Draw
{
    /// <summary>
    /// Blazor-only completion of <see cref="SkiaFontManager"/>.
    /// Adds HttpClient-based async font preloading. SharedNet base
    /// declares the partial with synchronous Initialize().
    /// </summary>
    public sealed partial class SkiaFontManager
    {
        private HttpClient? _httpClient;

        public async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        {
            if (_fontSources.Count == 0)
            {
                Initialized = true;
                return;
            }

            await _loadSemaphore.WaitAsync(cancellationToken);
            try
            {
                var httpClient = services?.GetService(typeof(HttpClient)) as HttpClient;
                if (httpClient == null)
                {
                    Super.Log("[DRAWNUI] Blazor font preload skipped: HttpClient service was not found.", Microsoft.Extensions.Logging.LogLevel.Warning);
                    return;
                }

                _httpClient = httpClient;

                var pending = _fontSources.Where(source => !_fonts.ContainsKey(source.Key)).ToList();

                // Download in parallel, then parse sequentially (parsing is CPU-bound).
                var downloads = await Task.WhenAll(pending.Select(async source =>
                {
                    try
                    {
                        var bytes = await httpClient.GetByteArrayAsync(source.Value, cancellationToken);
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
                    {
                        continue;
                    }

                    using var data = SKData.CreateCopy(download.Bytes);
                    var typeface = SKTypeface.FromData(data);
                    if (typeface != null)
                    {
                        _fonts[download.Key] = typeface;
                    }
                    else
                    {
                        Super.Log($"[DRAWNUI] Blazor font preload failed for {download.Key} from {download.Item2}", Microsoft.Extensions.Logging.LogLevel.Warning);
                    }
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
        /// </summary>
        public async Task<bool> LoadFontAsync(string alias, string sourceUrl,
            IProgress<float>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(sourceUrl))
            {
                return false;
            }

            var httpClient = _httpClient;
            if (httpClient == null)
            {
                Super.Log("[DRAWNUI] LoadFontAsync skipped: font manager was not initialized with an HttpClient.", Microsoft.Extensions.Logging.LogLevel.Warning);
                return false;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, sourceUrl);
                request.SetBrowserResponseStreamingEnabled(true);

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
                    {
                        progress?.Report((float)received / total);
                    }
                }

                using var data = SKData.CreateCopy(buffer.ToArray());
                var typeface = SKTypeface.FromData(data);
                if (typeface == null)
                {
                    Super.Log($"[DRAWNUI] LoadFontAsync failed to parse font {alias} from {sourceUrl}", Microsoft.Extensions.Logging.LogLevel.Warning);
                    return false;
                }

                _fontSources[alias] = sourceUrl;
                _fonts[alias] = typeface;
                progress?.Report(1f);
                return true;
            }
            catch (Exception e)
            {
                Super.Log(e);
                return false;
            }
        }
    }
}
