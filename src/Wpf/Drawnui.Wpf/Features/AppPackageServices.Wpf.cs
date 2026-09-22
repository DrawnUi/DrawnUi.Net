using System.IO;
using System.Net;
using System.Net.Http;

namespace DrawnUi.Wpf;

/// <summary>
/// Default <see cref="Super.Services"/> for a WPF app that did not supply its own container. The
/// shared engine resolves an <see cref="HttpClient"/> from it to read "package" resources by
/// relative path (the browser heads fetch them; MAUI opens raw assets). Here a relative path means
/// a file next to the executable, so the client serves those from disk and passes every absolute
/// http(s) request through untouched.
/// </summary>
public sealed class AppPackageServices : IServiceProvider
{
    private static readonly Uri PackageBase = new("http://drawnui-app-package.local/");
    private readonly HttpClient _client;

    /// <summary>Creates the provider with package files rooted at <paramref name="root"/>.</summary>
    public AppPackageServices(string root)
    {
        _client = new HttpClient(new PackageFileHandler(root)) { BaseAddress = PackageBase };
    }

    /// <summary>Installs the provider when the app has not set <see cref="Super.Services"/> itself.</summary>
    public static void EnsureInstalled()
    {
        if (Super.Services == null)
            Super.Services = new AppPackageServices(AppContext.BaseDirectory);
    }

    /// <inheritdoc/>
    public object GetService(Type serviceType) => serviceType == typeof(HttpClient) ? _client : null;

    private sealed class PackageFileHandler : DelegatingHandler
    {
        private readonly string _root;

        public PackageFileHandler(string root) : base(new HttpClientHandler())
        {
            _root = Path.GetFullPath(root);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri == null || request.RequestUri.Host != PackageBase.Host)
                return await base.SendAsync(request, cancellationToken);

            var relative = Uri.UnescapeDataString(request.RequestUri.AbsolutePath).TrimStart('/');
            var full = Path.GetFullPath(Path.Combine(_root, relative));

            // stay inside the package root
            if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request };

            var bytes = await File.ReadAllBytesAsync(full, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes), RequestMessage = request };
        }
    }
}
