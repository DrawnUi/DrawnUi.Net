namespace DrawnUi.Draw
{
    /// <summary>
    /// Blazor-only completion of <see cref="SkiaFontManager"/>.
    /// Adds HttpClient-based async font preloading. SharedNet base
    /// declares the partial with synchronous Initialize().
    /// </summary>
    public sealed partial class SkiaFontManager
    {
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

                foreach (var source in _fontSources)
                {
                    if (_fonts.ContainsKey(source.Key))
                    {
                        continue;
                    }

                    try
                    {
                        var bytes = await httpClient.GetByteArrayAsync(source.Value, cancellationToken);
                        using var data = SKData.CreateCopy(bytes);
                        var typeface = SKTypeface.FromData(data);
                        if (typeface != null)
                        {
                            _fonts[source.Key] = typeface;
                        }
                        else
                        {
                            Super.Log($"[DRAWNUI] Blazor font preload failed for {source.Key} from {source.Value}", Microsoft.Extensions.Logging.LogLevel.Warning);
                        }
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                    }
                }

                Initialized = true;
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }
    }
}
