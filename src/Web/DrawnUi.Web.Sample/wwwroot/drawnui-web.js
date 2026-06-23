// DrawnUI.Web JavaScript module
// Provides JS interop helpers for DrawnUI on WASM

let canvas = null;
let ctx = null;
let gl = null;
let texture = null;
let moduleExports = null;

// Exported functions from C# will be set after module loads
let onBrowserFrame = null;
let onPointerDown = null;
let onPointerMove = null;
let onPointerUp = null;
let onPointerCancel = null;
let moduleOnWheel = null;
let onCanvasResize = null;

/**
 * Initialize the canvas and get its dimensions
 */
export function initCanvas(targetWidth, targetHeight) {
    canvas = document.getElementById('drawnui-canvas');
    if (!canvas) {
        console.error('Canvas element with id "drawnui-canvas" not found');
        return;
    }

    // Software rendering path: use 2D context for putImageData blits.
    // (GPU/WebGL path will be wired separately via GRDirectContext later.)
    ctx = canvas.getContext('2d', { alpha: false });

    // Set up input handlers
    setupInputHandlers();

    // Report initial size
    reportCanvasSize();
}

/**
 * Get the current canvas width in CSS pixels
 */
export function getCanvasWidth() {
    if (!canvas) return 0;
    return canvas.clientWidth;
}

/**
 * Get the current canvas height in CSS pixels
 */
export function getCanvasHeight() {
    if (!canvas) return 0;
    return canvas.clientHeight;
}

/**
 * Get device pixel ratio
 */
export function getDevicePixelRatio() {
    return window.devicePixelRatio || 1;
}

/**
 * Request a single animation frame callback
 */
export function requestAnimationFrame() {
    window.requestAnimationFrame(handleFrame);
}

/**
 * Handle animation frame - call into C#
 */
function handleFrame(timestamp) {
    if (onBrowserFrame) {
        onBrowserFrame(timestamp);
    }
}

/**
 * Report canvas size to C#
 */
function reportCanvasSize() {
    if (!canvas) return;

    const width = canvas.clientWidth;
    const height = canvas.clientHeight;
    const pixelRatio = getDevicePixelRatio();

    // Resize canvas buffer to match display size
    canvas.width = Math.floor(width * pixelRatio);
    canvas.height = Math.floor(height * pixelRatio);

    // Notify C# about resize
    if (onCanvasResize) {
        onCanvasResize(width, height, pixelRatio);
    }
}

/**
 * Set up input event handlers
 */
function setupInputHandlers() {
    if (!canvas) return;

    // Pointer events
    canvas.addEventListener('pointerdown', handlePointerDown);
    canvas.addEventListener('pointermove', handlePointerMove);
    canvas.addEventListener('pointerup', handlePointerUp);
    canvas.addEventListener('pointercancel', handlePointerCancel);
    canvas.addEventListener('wheel', handleWheel);

    // Window resize
    window.addEventListener('resize', reportCanvasSize);
}

function handlePointerDown(e) {
    if (onPointerDown) {
        onPointerDown(e.pointerId, e.clientX, e.clientY, e.button, e.buttons);
    }
}

function handlePointerMove(e) {
    if (onPointerMove) {
        onPointerMove(e.pointerId, e.clientX, e.clientY, e.buttons);
    }
}

function handlePointerUp(e) {
    if (onPointerUp) {
        onPointerUp(e.pointerId, e.clientX, e.clientY, e.button, e.buttons);
    }
}

function handlePointerCancel(e) {
    if (onPointerCancel) {
        onPointerCancel(e.pointerId);
    }
}

function handleWheel(e) {
    e.preventDefault();
    if (moduleOnWheel) {
        moduleOnWheel(e.deltaX, e.deltaY, e.deltaMode);
    }
}

/**
 * Update canvas with PNG image data (software rendering path)
 */
export function updateCanvasWithPng(pngBytes) {
    if (!ctx) {
        console.warn('2D context not available for PNG update');
        return;
    }

    const blob = new Blob([pngBytes], { type: 'image/png' });
    const url = URL.createObjectURL(blob);
    const img = new Image();
    
    img.onload = () => {
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
        URL.revokeObjectURL(url);
    };
    
    img.src = url;
}

/**
 * Blit pixel buffer to HTML canvas via putImageData.
 * Receives a byte array directly from C# (pure WASM runtime doesn't expose
 * HEAPU8 like Emscripten, so we pass the array instead of a pointer).
 */
export function putImageData(pixels, width, height) {
    if (!ctx) {
        console.warn('2D context not available for putImageData');
        return;
    }
    if (!pixels || width <= 0 || height <= 0)
        return;

    // make sure the canvas is scaled correctly for the drawing
    canvas.width = width;
    canvas.height = height;

    // pixels is a Uint8Array from the marshaller; wrap into ImageData
    const buffer = new Uint8ClampedArray(pixels.buffer || pixels, 0, width * height * 4);
    const imageData = new ImageData(buffer, width, height);
    ctx.putImageData(imageData, 0, 0);
}

/**
 * Get the Emscripten Module (SKHtmlCanvas.getModule pattern).
 * In pure .NET WASM the global may be named differently than in Blazor.
 * Try every known location until we find HEAPU8.
 */
function getModule() {
    // SkiaSharp may register its own module
    const candidates = [
        globalThis.SkiaSharpModule,
        (typeof Module !== 'undefined') ? Module : null,
        globalThis.Module,
        globalThis.__dotnet_module,
    ];
    for (const m of candidates) {
        if (m && m.HEAPU8) return m;
    }
    // Fall back: scan globalThis for any object with HEAPU8
    for (const k of Object.keys(globalThis)) {
        const v = globalThis[k];
        if (v && typeof v === 'object' && v.HEAPU8) {
            return v;
        }
    }
    return null;
}

/**
 * Get WebGL texture ID for GPU rendering
 * Returns -1 if WebGL not available
 */
export function getGlTextureId() {
    if (!gl) return -1;

    if (!texture) {
        texture = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    }

    // Return some identifier - in WebGL we can't get the actual numeric ID
    // We'll use 1 as a placeholder
    return texture ? 1 : -1;
}

/**
 * Get WebGL context handle
 * Returns 0 if WebGL not available
 */
export function getGlContext() {
    return gl ? 1 : 0;
}

/**
 * Store C# function references after module loads.
 * Called from main.js with the resolved assembly exports object.
 */
export function setModuleExports(exports) {
    onBrowserFrame = exports.onBrowserFrame;
    onPointerDown = exports.onPointerDown;
    onPointerMove = exports.onPointerMove;
    onPointerUp = exports.onPointerUp;
    onPointerCancel = exports.onPointerCancel;
    moduleOnWheel = exports.onWheel;
    onCanvasResize = exports.onCanvasResize;

    console.log('DrawnUI: Module exports set up successfully');
}
