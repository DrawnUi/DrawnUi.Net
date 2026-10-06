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
        this.contextLost = false;  // WebGL context lost, no frames until the browser restores it
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

        if (this.renderLoopRequest !== 0 || this.contextLost)
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
export function initGL(elementId, callback, restoredCallback) {
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

    // A lost context (GPU reset, driver update, too many contexts) comes back only if the loss is
    // prevented. On restore the same WebGL object gets a new Emscripten handle (fresh extensions and
    // object tables), C# abandons its Skia context, and the next frame draws everything again.
    canvasEl.addEventListener('webglcontextlost', (e) => {
        e.preventDefault();
        view.contextLost = true;
        if (view.renderLoopRequest !== 0) {
            window.cancelAnimationFrame(view.renderLoopRequest);
            view.renderLoopRequest = 0;
        }
        console.warn('DrawnUI.Web: WebGL context lost');
    });
    canvasEl.addEventListener('webglcontextrestored', () => {
        const GL = getGL();
        const old = GL.getContext(view.glInfo.context);
        const gl = old.GLctx, attributes = old.attributes;
        GL.deleteContext(view.glInfo.context); // before registering: it clears canvas.GLctxObject
        const handle = GL.registerContext(gl, attributes);
        GL.makeContextCurrent(handle);
        view.glInfo.context = handle;
        view.contextLost = false;
        if (restoredCallback) restoredCallback();
        console.warn('DrawnUI.Web: WebGL context restored');
        view.requestAnimationFrame();
    });

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

// Absolute base URL (respects <base href>) so C# HttpClient can resolve relative
// font/asset paths. WasmFilesToBundle is a no-op in the .NET WASM SDK, so fonts are
// served as normal static web assets and fetched over HTTP, mirroring the Blazor path.
export function getBaseUrl() {
    return document.baseURI;
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
    // Pointer coords must be canvas-relative: clientX/Y are viewport-relative, so subtract the
    // canvas bounding rect. Required whenever the canvas is not at the viewport origin (e.g. an
    // aspect-locked / centered / letterboxed canvas); harmless when it is at 0,0.
    const relX = e => e.clientX - canvas.getBoundingClientRect().left;
    const relY = e => e.clientY - canvas.getBoundingClientRect().top;
    canvas.addEventListener('pointerdown', e => onPointerDown?.(e.pointerId, relX(e), relY(e), e.button, e.buttons, e.pointerType ?? 'mouse'));
    canvas.addEventListener('pointermove', e => onPointerMove?.(e.pointerId, relX(e), relY(e), e.buttons, e.pointerType ?? 'mouse'));
    canvas.addEventListener('pointerup', e => { onPointerUp?.(e.pointerId, relX(e), relY(e), e.button, e.buttons, e.pointerType ?? 'mouse'); if (e.pointerType !== 'mouse') _updateTouchAction?.(); });
    canvas.addEventListener('pointercancel', e => { onPointerCancel?.(e.pointerId); if (e.pointerType !== 'mouse') _updateTouchAction?.(); });
    // Gestures="Enabled" shares the wheel with the page: the default is prevented only when a control used it
    canvas.addEventListener('wheel', e => { const used = moduleOnWheel?.(e.deltaX, e.deltaY, e.deltaMode, relX(e), relY(e)); if (!_shareInput || used) e.preventDefault(); }, { passive: false });
    // right click / long press / Menu key: a control that handles ContextMenu suppresses the browser menu
    canvas.addEventListener('contextmenu', e => { if (onContextMenu?.(relX(e), relY(e), e.pointerType ?? 'mouse')) e.preventDefault(); });
    window.addEventListener('resize', reportCanvasSize);
}

// ============================================================================
// Gesture lock — mirrors Blazor Canvas GestureStyle (Lock/Enabled) at lib level.
// Web has no AppoMobi TouchEffect to preventDefault the touch stream, so for Lock
// we also kill iOS rubber-band / edge-swipe-away via a non-passive touchmove guard
// + overscroll-behavior on the page. CSS-only (Enabled) costs nothing per frame.
// ============================================================================

let _shareInput = false;          // Gestures="Enabled": wheel and touch pans are shared with the page
let _updateTouchAction = null;    // recomputes the canvas touch-action from what the page can scroll
let _touchObserver = null;        // html / body ResizeObserver feeding _updateTouchAction
let _gestureGuardEl = null;       // canvas the guard is bound to
let _gestureGuardHandler = null;  // touchmove listener (non-passive)

// Gestures="Enabled" shares touch pans with the page like MAUI's Enabled inside a native scroll view: along an axis the
// page (or a scrolling ancestor) can scroll, the browser takes the pan and cancels the pointer; taps and the other axis
// stay on the canvas. A page that cannot scroll keeps every touch on the canvas. touch-action is read when a touch
// starts, so it is kept current ahead of time (attach, window / html / body resize, after every touch).
function pageScrollAxes(element) {
    const scrolls = (v) => v === 'auto' || v === 'scroll' || v === 'overlay';
    const clips = (v) => v === 'hidden' || v === 'clip';
    let x = false, y = false;
    for (let n = element.parentElement; n && n !== document.body && n !== document.documentElement; n = n.parentElement) {
        const st = getComputedStyle(n);
        if (!y && scrolls(st.overflowY) && n.scrollHeight > n.clientHeight + 1) y = true;
        if (!x && scrolls(st.overflowX) && n.scrollWidth > n.clientWidth + 1) x = true;
    }
    const root = document.scrollingElement || document.documentElement;
    const hs = getComputedStyle(document.documentElement), bs = getComputedStyle(document.body);
    if (!y && !clips(hs.overflowY) && !clips(bs.overflowY) && root.scrollHeight > root.clientHeight + 1) y = true;
    if (!x && !clips(hs.overflowX) && !clips(bs.overflowX) && root.scrollWidth > root.clientWidth + 1) x = true;
    // a canvas filling a framed document (a widget in an iframe): the page that scrolls is the parent, which a
    // cross-origin frame cannot inspect, so assume it scrolls vertically; horizontal pans and taps stay on the canvas
    if (!y && window.self !== window.top) y = true;
    return x && y ? 'pan-x pan-y' : y ? 'pan-y' : x ? 'pan-x' : 'none';
}

function detachTouchSharing() {
    if (_updateTouchAction) window.removeEventListener('resize', _updateTouchAction);
    _touchObserver?.disconnect();
    _touchObserver = null;
    _updateTouchAction = null;
}

function detachGestureGuard() {
    if (_gestureGuardEl && _gestureGuardHandler) {
        _gestureGuardEl.removeEventListener('touchmove', _gestureGuardHandler);
    }
    _gestureGuardEl = null;
    _gestureGuardHandler = null;
}

/**
 * Apply gesture CSS to the canvas (and page) based on the DrawnUI Canvas.Gestures mode.
 * @param {string} elementId canvas element id
 * @param {boolean} lock true = GesturesMode.Lock, false = GesturesMode.Enabled
 */
export function applyGestureStyle(elementId, lock) {
    const el = document.getElementById(elementId);
    if (!el) {
        console.error(`applyGestureStyle: canvas "${elementId}" not found`);
        return;
    }

    _shareInput = !lock;
    detachTouchSharing();
    if (lock) {
        el.style.touchAction = 'none';
    } else {
        _updateTouchAction = () => { const pan = pageScrollAxes(el); if (el.style.touchAction !== pan) el.style.touchAction = pan; };
        _updateTouchAction();
        window.addEventListener('resize', _updateTouchAction);
        if (typeof ResizeObserver === 'function') {
            _touchObserver = new ResizeObserver(_updateTouchAction);
            _touchObserver.observe(document.documentElement);
            _touchObserver.observe(document.body);
        }
    }

    if (lock) {
        el.style.userSelect = 'none';
        el.style.webkitUserSelect = 'none';
        document.documentElement.style.overscrollBehavior = 'none';
        document.body.style.overscrollBehavior = 'none';

        detachGestureGuard();
        _gestureGuardEl = el;
        _gestureGuardHandler = e => e.preventDefault();
        el.addEventListener('touchmove', _gestureGuardHandler, { passive: false });
    } else {
        el.style.userSelect = '';
        el.style.webkitUserSelect = '';
        document.documentElement.style.overscrollBehavior = '';
        document.body.style.overscrollBehavior = '';
        detachGestureGuard();
    }
}

// ============================================================================
// Accessibility overlay — the Blazor Canvas ARIA overlay for pure WebAssembly: one invisible element per node of
// the accessibility snapshot over the canvas, in reading order. Screen readers read and activate them, Tab walks
// the interactive ones (one Tab stop per arrow-key group), Enter / Space activate, the arrow keys go to the node.
// Pointer input never lands on them (pointer-events: none): the canvas keeps every gesture. The canvas draws the
// keyboard focus ring (it follows scrolling every frame), so the elements have no outline.
// ============================================================================

let _a11y = null;

function ensureA11yStyles() {
    if (document.getElementById('drawnui-a11y-style')) return;
    const style = document.createElement('style');
    style.id = 'drawnui-a11y-style';
    // overflow: clip, not hidden: focusing a node outside the canvas must never scroll the overlay away from it
    style.textContent =
        '.drawnui-a11y-overlay{position:absolute;pointer-events:none;overflow:clip;}' +
        '.drawnui-a11y{position:absolute;background:transparent;color:transparent;font-size:0;' +
        'user-select:none;outline:none;}';
    document.head.appendChild(style);
}

/**
 * Creates the overlay after the canvas element (the next Tab stops of the page follow it) with the C# callbacks:
 * activate(id), key(id, key) -> used, focus(id), blur(id).
 */
export function a11yAttach(elementId, onActivate, onKey, onFocus, onBlur) {
    const canvasEl = document.getElementById(elementId);
    if (!canvasEl) return;
    a11yDetach();
    ensureA11yStyles();
    const overlay = document.createElement('div');
    overlay.className = 'drawnui-a11y-overlay';
    canvasEl.insertAdjacentElement('afterend', overlay);
    const place = () => {
        overlay.style.left = canvasEl.offsetLeft + 'px';
        overlay.style.top = canvasEl.offsetTop + 'px';
        overlay.style.width = canvasEl.clientWidth + 'px';
        overlay.style.height = canvasEl.clientHeight + 'px';
    };
    const observer = typeof ResizeObserver === 'function' ? new ResizeObserver(place) : null;
    observer?.observe(canvasEl);
    window.addEventListener('resize', place);
    place();
    _a11y = { overlay, map: new Map(), updating: false, place, observer, onActivate, onKey, onFocus, onBlur };
}

export function a11yDetach() {
    const a = _a11y;
    if (!a) return;
    _a11y = null;
    a.observer?.disconnect();
    window.removeEventListener('resize', a.place);
    a.overlay.remove();
}

function a11yCreate(id) {
    const el = document.createElement('div');
    el.className = 'drawnui-a11y';
    // a screen reader's click (its activation) on a node that takes input
    el.addEventListener('click', () => { if (el._interactive) _a11y?.onActivate(id); });
    el.addEventListener('keydown', e => {
        const a = _a11y;
        if (!a || !el._interactive) return;
        if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault(); // Space would scroll the page
            if (!e.repeat) a.onActivate(id);
        } else if (A11Y_KEYS.has(e.key) && a.onKey(id, e.key)) {
            e.preventDefault(); // a slider stepped, or focus moved inside the group
        }
    });
    el.addEventListener('focus', () => { if (_a11y && !_a11y.updating) _a11y.onFocus(id); });
    el.addEventListener('blur', () => { if (_a11y && !_a11y.updating) _a11y.onBlur(id); });
    return el;
}

const A11Y_STATES = ['aria-checked', 'aria-selected', 'aria-pressed'];
const A11Y_KEYS = new Set(['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End', 'PageUp', 'PageDown']);

function a11ySet(el, name, value) {
    if (value === null || value === undefined || value === '') {
        if (el.hasAttribute(name)) el.removeAttribute(name);
    } else if (el.getAttribute(name) !== value) {
        el.setAttribute(name, value);
    }
}

// flags: 1 takes input, 2 Tab stop, 4 has a pressed state, 8 pressed, 16 has a range value, 32 vertical,
// 64 a control role that takes no input (unavailable)
function a11yApply(el, flags, nums, o, strs, s) {
    const interactive = (flags & 1) !== 0;
    el._interactive = interactive;
    a11ySet(el, 'role', strs[s]);
    const label = strs[s + 1];
    a11ySet(el, 'aria-label', label);
    if (el.textContent !== label) el.textContent = label; // what a live region announces
    a11ySet(el, 'title', strs[s + 2]);
    a11ySet(el, 'aria-live', strs[s + 3]);
    // the pressed state on the attribute its role is read from (aria-checked for a switch, see Aria.PressedStateAttribute)
    const state = (flags & 4) ? ((flags & 8) ? 'true' : 'false') : null;
    for (const name of A11Y_STATES) a11ySet(el, name, name === strs[s + 5] ? state : null);
    a11ySet(el, 'aria-disabled', (flags & 64) ? 'true' : null);
    const value = (flags & 16) !== 0;
    a11ySet(el, 'aria-valuenow', value ? String(nums[o + 4]) : null);
    a11ySet(el, 'aria-valuemin', value ? String(nums[o + 5]) : null);
    a11ySet(el, 'aria-valuemax', value ? String(nums[o + 6]) : null);
    a11ySet(el, 'aria-valuetext', value ? strs[s + 4] : null);
    a11ySet(el, 'aria-orientation', value ? ((flags & 32) ? 'vertical' : 'horizontal') : null);
    // not a Tab stop: -1 still lets a screen reader's focus be moved here (refocus)
    a11ySet(el, 'tabindex', interactive && (flags & 2) ? '0' : '-1');
    el.style.left = nums[o] + 'px';
    el.style.top = nums[o + 1] + 'px';
    el.style.width = nums[o + 2] + 'px';
    el.style.height = nums[o + 3] + 'px';
}

/**
 * The snapshot, in reading order: ints = (id, flags) per node, nums = (left, top, width, height, now, min, max)
 * per node in CSS pixels, strs = (role, label, hint, live, value text, pressed attribute) per node. Elements are kept by id and moved
 * only when the order changed, so the focused one keeps its focus.
 */
export function a11yUpdate(ints, nums, strs) {
    const a = _a11y;
    if (!a) return;
    const active = document.activeElement;
    const focused = active && a.overlay.contains(active) ? active : null;
    a.updating = true;
    try {
        const count = ints.length / 2;
        const keep = new Set();
        let ref = a.overlay.firstChild;
        for (let i = 0; i < count; i++) {
            const id = ints[i * 2];
            let el = a.map.get(id);
            if (!el) {
                el = a11yCreate(id);
                a.map.set(id, el);
            }
            keep.add(id);
            a11yApply(el, ints[i * 2 + 1], nums, i * 7, strs, i * 6);
            if (el !== ref) a.overlay.insertBefore(el, ref);
            else ref = ref.nextSibling;
        }
        for (const [id, el] of a.map) {
            if (!keep.has(id)) {
                el.remove();
                a.map.delete(id);
            }
        }
        // a moved element lost focus: give it back without telling C# (nothing changed for it)
        if (focused && focused.isConnected && document.activeElement !== focused) focused.focus({ preventScroll: true });
    } finally {
        a.updating = false;
    }
}

/** Keyboard or screen-reader focus moves to the node (arrow keys in a group, refocus after its page closed). */
export function a11yFocus(id) {
    _a11y?.map.get(id)?.focus({ preventScroll: true });
}

/** A live region changed: its new text at once, the snapshot follows within a second. */
export function a11yLive(id, text) {
    const el = _a11y?.map.get(id);
    if (!el) return;
    el.setAttribute('aria-label', text);
    el.textContent = text;
}

// ============================================================================
// Loading spinner — self-contained, no per-app HTML/CSS needed.
// ============================================================================

let loaderEl = null;

function ensureLoaderStyles() {
    if (document.getElementById('drawnui-loader-style')) return;
    const style = document.createElement('style');
    style.id = 'drawnui-loader-style';
    style.textContent =
        '.drawnui-loader{position:absolute;top:50%;left:50%;width:42px;height:42px;' +
        'margin:-21px 0 0 -21px;border:4px solid rgba(255,255,255,.15);' +
        'border-top-color:#4CC9F0;border-radius:50%;' +
        'animation:drawnui-spin .8s linear infinite;z-index:2147483647;}' +
        '@keyframes drawnui-spin{to{transform:rotate(360deg)}}' +
        '.drawnui-loader.drawnui-error{width:auto;height:auto;margin:0;' +
        'transform:translate(-50%,-50%);border:0;animation:none;' +
        'font:16px sans-serif;color:#FF5555;white-space:nowrap;}';
    document.head.appendChild(style);
}

/** Show a centered rotating spinner (injects element + styles on first call). */
export function showLoader() {
    ensureLoaderStyles();
    if (!loaderEl) {
        loaderEl = document.createElement('div');
        loaderEl.className = 'drawnui-loader';
        document.body.appendChild(loaderEl);
    }
    loaderEl.style.display = '';
    loaderEl.classList.remove('drawnui-error');
    loaderEl.textContent = '';
}

/** Hide the spinner (call when the app is ready). */
export function hideLoader() {
    if (loaderEl) loaderEl.style.display = 'none';
}

/** Replace the spinner with an error message. */
export function showError(message) {
    showLoader();
    loaderEl.classList.add('drawnui-error');
    loaderEl.textContent = message;
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
    onContextMenu = exports.onContextMenu;
    onCanvasResize = exports.onCanvasResize;
    onKeyDown = exports.onKeyDown;
    onKeyUp = exports.onKeyUp;
    setupKeyboardHandlers();
    console.log('DrawnUI.Web: Module exports set up');
}

// DOM keys that would scroll the page — suppress default while DrawnUI handles them.
const PREVENT_DEFAULT_KEYS = new Set([
    'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Space',
]);

// DrawnUI only observes keys: a page element that has focus (an input, a textarea, a button...) keeps its
// keys, so Space types and the arrows move its caret. The page only stops scrolling when the key belongs
// to nobody else: its target is the page itself, a canvas or a node of the accessibility overlay.
function keyIsForDrawnUi(e) {
    const t = e.target;
    return !t || t === document.body || t === document.documentElement || t === window || t.tagName === 'CANVAS'
        || !!(t.closest && t.closest('.drawnui-a11y-overlay'));
}

let keyboardAttached = false;
function setupKeyboardHandlers() {
    if (keyboardAttached) return;
    keyboardAttached = true;
    // Window-level: the WebGL canvas can't hold focus for key events.
    window.addEventListener('keydown', e => {
        if (e.repeat) return;
        const forDrawnUi = keyIsForDrawnUi(e);
        if (PREVENT_DEFAULT_KEYS.has(e.code) && forDrawnUi) e.preventDefault();
        onKeyDown?.(e.code, !forDrawnUi);
    });
    window.addEventListener('keyup', e => {
        const forDrawnUi = keyIsForDrawnUi(e);
        if (PREVENT_DEFAULT_KEYS.has(e.code) && forDrawnUi) e.preventDefault();
        onKeyUp?.(e.code, !forDrawnUi);
    });
}

let onBrowserFrame = null;
let onPointerDown = null;
let onPointerMove = null;
let onPointerUp = null;
let onPointerCancel = null;
let moduleOnWheel = null;
let onContextMenu = null;
let onCanvasResize = null;
let onKeyDown = null;
let onKeyUp = null;
