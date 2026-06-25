// Workaround for https://github.com/dotnet/runtime/issues/76077
// Special thanks to the Avalonia UI team
//
// Emscripten JS library linked via `--js-library` (see DrawnUi.Web.props).
// InterceptBrowserObjects runs INSIDE the module closure where `GL` and `Module`
// are in scope, and stashes them on globalThis. This is the only way to reach the
// Emscripten GL object from JS in the pure .NET WASM runtime, because `GL` is not
// in EXPORTED_RUNTIME_METHODS (touching Module.GL from outside aborts the runtime).
// Called from C# via [DllImport("libSkiaSharp")] InterceptBrowserObjects().

var SkiaSharpInterop = {
	$SkiaSharpLibrary: {
		internal_func: function () {
		}
	},
    InterceptBrowserObjects: function () {
		globalThis.SkiaSharpGL = GL
        globalThis.SkiaSharpModule = Module
    }
}

autoAddDeps(SkiaSharpInterop, '$SkiaSharpLibrary')
mergeInto(LibraryManager.library, SkiaSharpInterop)
