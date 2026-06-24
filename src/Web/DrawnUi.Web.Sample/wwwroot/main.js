// Main entry point for DrawnUI.Web.Sample
// Loads the WASM module and initializes the app

import { dotnet } from './_framework/dotnet.js';
import { setModuleExports } from './drawnui-web.js';

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
    
    // Get exports from our assembly
    loadingDiv.textContent = 'Initializing DrawnUI...';
    const exports = await getAssemblyExports(config.mainAssemblyName);
    
    // Exports are keyed by namespace path. Stash for debugging.
    window.__drawnUiExports = exports;
    console.log('DrawnUI exports:', exports);
    
    // All [JSExport] methods live on Program in the main assembly.
    // (WebInput is in the library assembly, which doesn't generate JS exports,
    //  so Program re-exports them as pass-throughs.)
    const programExports = exports?.DrawnUi?.Web?.Sample?.Program
                        ?? exports?.Program;
    
    if (!programExports) {
        console.error('Program exports missing. Full exports:', exports);
        throw new Error('Could not locate Program exports. Inspect window.__drawnUiExports.');
    }
    
    // Wire C# [JSExport] callbacks into the drawnui-web.js module (JS->JS, no marshaling)
    setModuleExports({
        onBrowserFrame: programExports.OnBrowserFrame,
        onCanvasResize: programExports.OnCanvasResize,
        onPointerDown: programExports.OnPointerDown,
        onPointerMove: programExports.OnPointerMove,
        onPointerUp: programExports.OnPointerUp,
        onPointerCancel: programExports.OnPointerCancel,
        onWheel: programExports.OnWheel,
    });

    // Call Main to initialize the app (initCanvas is called from C# via JSImport)
    programExports.Main();
    
    // Hide loading message
    loadingDiv.style.display = 'none';
    
    console.log('DrawnUI.Web.Sample: Ready!');
    
} catch (error) {
    console.error('DrawnUI.Web.Sample: Failed to start:', error);
    loadingDiv.textContent = 'Failed to load: ' + error.message;
    loadingDiv.style.color = 'red';
}
