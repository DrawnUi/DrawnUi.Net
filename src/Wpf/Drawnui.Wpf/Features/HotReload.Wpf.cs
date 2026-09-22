using DrawnUi.Draw;

[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(DrawnUi.HotReloadService))]

namespace DrawnUi
{
    /// <summary>
    /// C# Hot Reload handler for the WPF head, the same pattern as the MAUI and WebAssembly heads.
    /// The runtime calls <see cref="UpdateApplication"/> for every metadata delta (Visual Studio /
    /// Rider Hot Reload, <c>dotnet watch</c>); the burst one edit produces is debounced into a single
    /// <see cref="Super.HotReload"/>. The runtime only applies deltas during development, so this
    /// never fires in a published app.
    /// </summary>
    public static class HotReloadService
    {
        // One edit arrives as several UpdateApplication calls: reload once, after they settle.
        private const uint DelayMs = 1000;

        private static readonly RestartingTimer Debounce = new(DelayMs, () =>
        {
            Super.Log("[HOTRELOAD] Updating Application =>");
            Super.RaiseHotReload(null);
        });

        /// <summary>Called by the runtime; nothing is cached per type here.</summary>
        public static void ClearCache(Type[] types)
        {
        }

        /// <summary>Called by the runtime after a metadata update was applied.</summary>
        public static void UpdateApplication(Type[] types) => Debounce.Kick();
    }
}

namespace DrawnUi.Draw
{
    public partial class Super
    {
        /// <summary>
        /// Raised (debounced, on a timer thread) after C# Hot Reload applied an edit. Rebuild
        /// code-behind UI from here: <c>DrawnUiElement.ContentBuilder</c> and <c>SkiaShell</c>
        /// already do. Marshal to the UI thread yourself when handling it directly.
        /// </summary>
        public static event Action<Type[]> HotReload;

        internal static void RaiseHotReload(Type[] types) => HotReload?.Invoke(types);
    }
}
