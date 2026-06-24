// Main entry point for DrawnUI.Web.Sample
// Loads the WASM module and initializes the app

import { dotnet } from './_framework/dotnet.js';
import { setModuleExports } from './_content/DrawnUi.Web/drawnui-web.js';

// Recursively find the [JSExport] Main in an exports tree (no per-app namespace hardcode).
function findExport(obj, name) {
    for (const key in obj) {
        const val = obj[key];
        if (key === name && typeof val === 'function') return val;
        if (val && typeof val === 'object') {
            const found = findExport(val, name);
            if (found) return found;
        }
    }
    return null;
}

console.log('DrawnUI.Web.Sample: Starting...');

// Expose dotnet runtime globally so drawnui-web.js can find Emscripten GL module
globalThis.dotnet = dotnet;

// Show loading message
const loadingDiv = document.getElementById('loading');

try {
    // Load the .NET runtime
    loadingDiv.textContent = 'Loading .NET runtime...';
    
    const { getAssemblyExports, getConfig } = await dotnet
        .withDiagnosticTracing(false)
        .withApplicationArgumentsFromQuery()
        .create();

    const config = getConfig();

    loadingDiv.textContent = 'Initializing DrawnUI...';

    // Input / frame / resize callbacks live in the DrawnUi.Web library assembly.
    const lib = await getAssemblyExports('DrawnUi.Web');
    const Input = lib.DrawnUi.Draw.WebInput;
    const Super = lib.DrawnUi.Draw.Super;
    const Host = lib.DrawnUi.Draw.BrowserHost;

    setModuleExports({
        onBrowserFrame: Super.OnBrowserFrame,
        onCanvasResize: Host.OnCanvasResize,
        onPointerDown: Input.OnPointerDown,
        onPointerMove: Input.OnPointerMove,
        onPointerUp: Input.OnPointerUp,
        onPointerCancel: Input.OnPointerCancel,
        onWheel: Input.OnWheel,
    });

    // App entry point: builds DrawnUI and runs the Canvas (RunAsync does all glue).
    const app = await getAssemblyExports(config.mainAssemblyName);
    const main = findExport(app, 'Main');
    if (!main) throw new Error(`No [JSExport] Main found in ${config.mainAssemblyName}`);
    main();
    
    // Hide loading message
    loadingDiv.style.display = 'none';
    
    console.log('DrawnUI.Web.Sample: Ready!');
    
} catch (error) {
    console.error('DrawnUI.Web.Sample: Failed to start:', error);
    loadingDiv.textContent = 'Failed to load: ' + error.message;
    loadingDiv.style.color = 'red';
}
