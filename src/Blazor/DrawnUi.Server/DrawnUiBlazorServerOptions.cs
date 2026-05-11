namespace DrawnUi.Blazor.Server;

public sealed class DrawnUiBlazorServerOptions
{
    public ServerRenderMode RenderMode { get; set; } = ServerRenderMode.PosterFrameThenInteractive;

    public bool UseBase64DataUrls { get; set; }

    public int MaxFramesPerSecond { get; set; } = 30;

    public string DefaultImageContentType { get; set; } = "image/png";
}