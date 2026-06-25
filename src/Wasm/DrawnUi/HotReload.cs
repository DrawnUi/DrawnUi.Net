using System;
using DrawnUi.Draw;
using DrawnUi.Models;

[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(DrawnUi.HotReloadService))]

namespace DrawnUi
{
    /// <summary>
    /// Pure-WebAssembly C# Hot Reload handler. Mirrors the MAUI <c>DrawnUi.HotReloadService</c>.
    /// The runtime calls <see cref="UpdateApplication"/> on every metadata delta; we debounce the
    /// burst and raise <see cref="Super.HotReload"/> once. The runtime only applies metadata deltas
    /// during development (<c>dotnet watch</c> / debug), so this never fires in a published app —
    /// zero production cost — and works under bare <c>dotnet watch</c> with no debugger attached.
    /// <c>BrowserHost</c> subscribes to rebuild the drawn scene.
    /// </summary>
    public static class HotReloadService
    {
        /// <summary>Raised (debounced) after a hot-reload metadata update. <c>Super</c> forwards it to <c>Super.HotReload</c>.</summary>
        public static event Action<Type[]> UpdateApplicationEvent;

        // Coalesce the burst of UpdateApplication calls one edit produces into a single reload.
        private const uint DelayMs = 1000;

        private static readonly RestartingTimer _timer = new(DelayMs, () =>
        {
            Super.Log("[HOTRELOAD] Updating Application =>");
            UpdateApplicationEvent?.Invoke(null);
        });

        public static void ClearCache(Type[] types) { }

        public static void UpdateApplication(Type[] types)
        {
            _timer.Kick();
        }
    }
}
