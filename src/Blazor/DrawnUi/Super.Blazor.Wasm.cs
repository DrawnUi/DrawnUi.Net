using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace DrawnUi.Draw
{
    public partial class Super
    {
        public static DrawnUiBlazorBuilder UseDrawnUi(WebAssemblyHostBuilder hostBuilder)
            => new(hostBuilder);
    }
}
