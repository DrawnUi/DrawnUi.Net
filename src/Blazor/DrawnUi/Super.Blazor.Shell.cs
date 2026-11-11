using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(DrawnUi.Draw.Super.BlazorHotReload))]

namespace DrawnUi.Draw
{
    public partial class Super
    {
        // Listeners held weakly by their owner: a dropped shell or page is still collected.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, Action<Type[]>> HotReloadListeners = new();

        /// <summary>
        /// Calls <paramref name="handler"/> on every C# hot reload while <paramref name="owner"/> is alive, without keeping
        /// it alive: an owner dropped without unsubscribing is still collected. One handler per owner. Same API as the
        /// desktop and pure WebAssembly .NET heads.
        /// </summary>
        public static void AddHotReloadListener(object owner, Action<Type[]> handler) =>
            HotReloadListeners.AddOrUpdate(owner, handler);

        /// <summary>Stops calling the handler <paramref name="owner"/> registered with <see cref="AddHotReloadListener"/>.</summary>
        public static void RemoveHotReloadListener(object owner) => HotReloadListeners.Remove(owner);

        /// <summary>
        /// Opens <paramref name="link"/> in a new browser tab (window.open), as Super.OpenLink opens it in the system
        /// browser on the MAUI heads.
        /// </summary>
        public static void OpenLink(string link)
        {
            if (string.IsNullOrEmpty(link) || Services?.GetService(typeof(IJSRuntime)) is not IJSRuntime js)
                return;
            _ = js.InvokeVoidAsync("open", link, "_blank", "noopener").AsTask().ContinueWith(
                t => Log(t.Exception?.GetBaseException().Message, LogLevel.Error), TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// C# Hot Reload: the runtime calls <see cref="UpdateApplication"/> for every metadata delta (only while
        /// developing, never in a published app); one edit's burst reaches the listeners once.
        /// </summary>
        public static class BlazorHotReload
        {
            private static readonly RestartingTimer Debounce = new(1000, () =>
            {
                foreach (var pair in HotReloadListeners)
                    pair.Value(null);
            });

            public static void ClearCache(Type[] types) { }

            public static void UpdateApplication(Type[] types) => Debounce.Kick();
        }
    }
}
