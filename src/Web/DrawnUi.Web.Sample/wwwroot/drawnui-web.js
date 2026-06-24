// DrawnUI.Web — SkiaSharp canvas bridge for pure WebAssembly (no Blazor).
// Ported from SkiaSharp.Views.Blazor SKHtmlCanvas.ts + SKHtmlCanvasInterop.cs.
//
// Two rendering paths:
//   GL (GPU)    — Emscripten GL.createContext → GRContext → SKSurface on framebuffer. Zero copy.
//   Raster (CPU)— 2D context + putImageData from pinned byte[] buffer.
//
// Export-friendly: designed so this could be contributed back as SkiaSharp.Views.Web.

// --- Emscripten aliases (resolved at runtime, not import-time) ---
function getGL() {
    // The Emscripten GL object is exposed on globalThis.SkiaSharpGL by the native
    // InterceptBrowserObjects() call (C#), linked via --js-library SkiaSharpInterop.js.
    // Do NOT probe Module.GL / getDotnetRuntime().Module.GL: GL is not in
    // EXPORTED_RUNTIME_METHODS, and accessing it ABORTS the .NET WASM runtime.
    return globalThis.SkiaSharpGL || null;
}
function getModule() {
    return globalThis.SkiaSharpModule || null;
}
function getGLctx() {
    const GL = getGL();
    if (!GL) return null;
    return (GL.currentContext && GL.currentContext.GLctx) || (typeof GLctx !== 'undefined' ? GLctx : null);
}

// --- Per-canvas view state (mirrors SKHtmlCanvas class) ---
const views = new Map(); // elementId → SKHtmlCanvasView

class SKHtmlCanvasView {
    constructor(htmlCanvas, renderFrameCallback) {
        this.htmlCanvas = htmlCanvas;
        this.renderFrameCallback = renderFrameCallback; // C# [JSExport] function
        this.glInfo = null;        // { context, fboId, stencil, sample, depth } or null for raster
        this.renderLoopEnabled = false;
        this.renderLoopRequest = 0;
    }

    deinit() {
        this.setEnableRenderLoop(false);
    }

    requestAnimationFrame(renderLoop, width, height) {
        if (renderLoop !== undefined && this.renderLoopEnabled !== renderLoop)
            this.setEnableRenderLoop(renderLoop);

        if (width && height) {
            this.htmlCanvas.width = width;
            this.htmlCanvas.height = height;
        }

        if (this.renderLoopRequest !== 0)
            return;

        this.renderLoopRequest = window.requestAnimationFrame(() => {
            if (this.glInfo) {
                const GL = getGL();
                if (GL) GL.makeContextCurrent(this.glInfo.context);
            }

            if (this.renderFrameCallback) {
                this.renderFrameCallback();
            }
            this.renderLoopRequest = 0;

            if (this.renderLoopEnabled)
                this.requestAnimationFrame();
        });
    }

    setEnableRenderLoop(enable) {
        this.renderLoopEnabled = enable;
        if (enable) {
            this.requestAnimationFrame();
        } else if (this.renderLoopRequest !== 0) {
            window.cancelAnimationFrame(this.renderLoopRequest);
            this.renderLoopRequest = 0;
        }
    }

    // Raster path: blit byte[] to 2D context (pure WASM passes array, not pointer)
    putImageData(pixels, width, height) {
        if (this.glInfo || !pixels || width <= 0 || height <= 0)
            return false;

        const ctx2d = this.htmlCanvas.getContext('2d');
        if (!ctx2d) {
            console.error('Failed to obtain 2D canvas context.');
            return false;
        }

        this.htmlCanvas.width = width;
        this.htmlCanvas.height = height;

        const buffer = new Uint8ClampedArray(pixels.buffer || pixels, 0, width * height * 4);
        const imageData = new ImageData(buffer, width, height);
        ctx2d.putImageData(imageData, 0, 0);
        return true;
    }
}

// --- WebGL context creation (mirrors SKHtmlCanvas.createWebGLContext) ---
function createWebGLContext(htmlCanvas) {
    const contextAttributes = {
        alpha: 1,
        depth: 1,
        stencil: 8,
        antialias: 1,
        premultipliedAlpha: 1,
        preserveDrawingBuffer: 0,
        preferLowPowerToHighPerformance: 0,
        failIfMajorPerformanceCaveat: 0,
        majorVersion: 2,
        minorVersion: 0,
        enableExtensionsByDefault: 1,
        explicitSwapControl: 0,
        renderViaOffscreenBackBuffer: 0,
    };

    const GL = getGL();
    if (!GL) {
        console.error('Emscripten GL module not found. GPU rendering unavailable.');
        return null;
    }

    let ctx = GL.createContext(htmlCanvas, contextAttributes);
    if (!ctx && contextAttributes.majorVersion > 1) {
        console.warn('Falling back to WebGL 1.0');
        contextAttributes.majorVersion = 1;
        contextAttributes.minorVersion = 0;
        ctx = GL.createContext(htmlCanvas, contextAttributes);
    }
    return ctx;
}

// ============================================================================
// Exported functions — called from C# via [JSImport]
// ============================================================================

/**
 * Initialize a GPU (WebGL) canvas view. Returns GL info or null on failure.
 * Mirrors SKHtmlCanvas.initGL.
 */
export function initGL(elementId, callback) {
    const canvasEl = document.getElementById(elementId);
    if (!canvasEl) {
        console.error(`Canvas element "${elementId}" not found`);
        return null;
    }

    const view = new SKHtmlCanvasView(canvasEl, callback);
    views.set(elementId, view);

    const ctx = createWebGLContext(canvasEl);
    if (!ctx) {
        console.error('Failed to create WebGL context');
        return null;
    }

    const GL = getGL();
    GL.makeContextCurrent(ctx);

    const GLctx = getGLctx();
    if (!GLctx) {
        console.error('Failed to get current WebGL context');
        return null;
    }

    const fbo = GLctx.getParameter(GLctx.FRAMEBUFFER_BINDING);
    view.glInfo = {
        context: ctx,
        fboId: fbo ? fbo.id : 0,
        stencil: GLctx.getParameter(GLctx.STENCIL_BITS),
        sample: 0,
        depth: GLctx.getParameter(GLctx.DEPTH_BITS),
    };

    console.log(`DrawnUI.Web GL init: fbo=${view.glInfo.fboId} stencil=${view.glInfo.stencil} depth=${view.glInfo.depth}`);
    return view.glInfo;
}

/**
 * Initialize a raster (CPU) canvas view. Returns true on success.
 */
export function initRaster(elementId, callback) {
    const canvasEl = document.getElementById(elementId);
    if (!canvasEl) {
        console.error(`Canvas element "${elementId}" not found`);
        return false;
    }

    const view = new SKHtmlCanvasView(canvasEl, callback);
    views.set(elementId, view);
    return true;
}

/** Deinitialize a canvas view. */
export function deinit(elementId) {
    const view = views.get(elementId);
    if (!view) return;
    view.deinit();
    views.delete(elementId);
}

/** Request a frame render. Optionally set render loop + resize. */
export function requestAnimationFrame(elementId, renderLoop, width, height) {
    const view = views.get(elementId);
    if (!view) return;
    view.requestAnimationFrame(renderLoop, width, height);
}

/** Enable/disable continuous render loop. */
export function setEnableRenderLoop(elementId, enable) {
    const view = views.get(elementId);
    if (!view) return;
    view.setEnableRenderLoop(enable);
}

/** Raster path: blit pixel buffer to canvas via putImageData. */
export function putImageData(elementId, pixels, width, height) {
    const view = views.get(elementId);
    if (!view) return;
    view.putImageData(pixels, width, height);
}

// ============================================================================
// Legacy compat — old initCanvas/getCanvasWidth etc. (used during bring-up)
// ============================================================================

let canvas = null;
let ctx = null;

export function initCanvas(targetWidth, targetHeight) {
    canvas = document.getElementById('drawnui-canvas');
    if (!canvas) {
        console.error('Canvas element with id "drawnui-canvas" not found');
        return;
    }
    // Do NOT acquire a 2D context here: a canvas can only ever hold ONE context
    // type. Grabbing '2d' permanently blocks the WebGL (GPU) path. The raster
    // fallback lazily acquires '2d' in putImageData only if GPU init fails.
    setupInputHandlers();
    reportCanvasSize();
}

export function getCanvasWidth() {
    return canvas ? canvas.clientWidth : 0;
}

export function getCanvasHeight() {
    return canvas ? canvas.clientHeight : 0;
}

export function getDevicePixelRatio() {
    return window.devicePixelRatio || 1;
}

export function requestAnimationFrameLegacy() {
    window.requestAnimationFrame(handleFrame);
}

function handleFrame(timestamp) {
    if (onBrowserFrame) onBrowserFrame(timestamp);
}

function reportCanvasSize() {
    if (!canvas) return;
    const width = canvas.clientWidth;
    const height = canvas.clientHeight;
    const pixelRatio = getDevicePixelRatio();
    canvas.width = Math.floor(width * pixelRatio);
    canvas.height = Math.floor(height * pixelRatio);
    if (onCanvasResize) onCanvasResize(width, height, pixelRatio);
}

function setupInputHandlers() {
    if (!canvas) return;
    canvas.addEventListener('pointerdown', e => onPointerDown?.(e.pointerId, e.clientX, e.clientY, e.button, e.buttons));
    canvas.addEventListener('pointermove', e => onPointerMove?.(e.pointerId, e.clientX, e.clientY, e.buttons));
    canvas.addEventListener('pointerup', e => onPointerUp?.(e.pointerId, e.clientX, e.clientY, e.button, e.buttons));
    canvas.addEventListener('pointercancel', e => onPointerCancel?.(e.pointerId));
    canvas.addEventListener('wheel', e => { e.preventDefault(); moduleOnWheel?.(e.deltaX, e.deltaY, e.deltaMode, e.clientX, e.clientY); }, { passive: false });
    window.addEventListener('resize', reportCanvasSize);
}

/**
 * Wire C# [JSExport] callbacks (called from main.js, JS→JS, no marshaling).
 */
export function setModuleExports(exports) {
    onBrowserFrame = exports.onBrowserFrame;
    onPointerDown = exports.onPointerDown;
    onPointerMove = exports.onPointerMove;
    onPointerUp = exports.onPointerUp;
    onPointerCancel = exports.onPointerCancel;
    moduleOnWheel = exports.onWheel;
    onCanvasResize = exports.onCanvasResize;
    console.log('DrawnUI.Web: Module exports set up');
}

let onBrowserFrame = null;
let onPointerDown = null;
let onPointerMove = null;
let onPointerUp = null;
let onPointerCancel = null;
let moduleOnWheel = null;
let onCanvasResize = null;
