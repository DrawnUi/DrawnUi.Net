const observers = new WeakMap();

// SkiaSharp.Views.Blazor resizes the canvas bitmap the moment it learns a new size
// (SKHtmlCanvas.requestAnimationFrame(renderLoop, width, height) writes canvas.width/height) and
// draws on the NEXT animation frame. A new size arrives from a ResizeObserver, which the browser runs
// after this frame's animation callbacks and before painting, so that frame was painted with the
// freshly cleared bitmap: a one-frame blank after every resize (seen when a window resize settles).
// When a call really changes the bitmap size, draw right away in a microtask, still before the
// browser paints. Calls with an unchanged size (every normal frame) are untouched.
let skiaCanvasPatch = null;

function patchSkiaCanvasResize() {
    skiaCanvasPatch ??= import(new URL('./_content/SkiaSharp.Views.Blazor/SKHtmlCanvas.js', document.baseURI).href)
        .then(({ SKHtmlCanvas }) => {
            const proto = SKHtmlCanvas?.prototype;
            if (!proto || proto.requestAnimationFrame.drawnUiResizePatch) return;
            const original = proto.requestAnimationFrame;
            const patched = function (renderLoop, width, height) {
                const canvas = this.htmlCanvas;
                const resized = !!(width && height && canvas && (canvas.width !== width || canvas.height !== height));
                original.call(this, renderLoop, width, height);
                if (!resized) return;
                queueMicrotask(() => {
                    if (!this.htmlCanvas || !this.renderFrameCallback) return;   // disposed meanwhile
                    if (this.glInfo) SKHtmlCanvas.getGL().makeContextCurrent(this.glInfo.context);
                    if (typeof this.renderFrameCallback === 'function') this.renderFrameCallback();
                    else this.renderFrameCallback.invokeMethod('Invoke');
                });
            };
            patched.drawnUiResizePatch = true;
            proto.requestAnimationFrame = patched;
        })
        .catch(() => { /* SkiaSharp not present or its layout changed: keep its own behavior */ });
    return skiaCanvasPatch;
}

function isElementFullscreen(element, simulatedFullscreen) {
    return simulatedFullscreen === true || document.fullscreenElement === element || document.webkitFullscreenElement === element;
}

function isMobileBrowserCore() {
    const navigatorRef = globalThis.navigator;
    if (!navigatorRef) {
        return false;
    }

    if (typeof navigatorRef.userAgentData?.mobile === 'boolean') {
        return navigatorRef.userAgentData.mobile;
    }

    const userAgent = navigatorRef.userAgent || navigatorRef.vendor || '';
    if (/android|webos|iphone|ipad|ipod|blackberry|iemobile|opera mini|mobile/i.test(userAgent)) {
        return true;
    }

    const coarsePointer = globalThis.matchMedia?.('(pointer: coarse)')?.matches === true;
    const smallViewport = globalThis.matchMedia?.('(max-width: 900px)')?.matches === true;
    const hasTouch = (navigatorRef.maxTouchPoints || 0) > 1;

    return coarsePointer && smallViewport && hasTouch;
}

function notifyFullscreen(element, dotNetRef, simulatedFullscreen) {
    dotNetRef.invokeMethodAsync('OnFullscreenChanged', isElementFullscreen(element, simulatedFullscreen));
}

function notifySize(element, dotNetRef, width, height) {
    const nextWidth = Math.max(1, width);
    const nextHeight = Math.max(1, height);
    dotNetRef.invokeMethodAsync('OnHostResized', nextWidth, nextHeight);
}

export function getHostSize(element) {
    const rect = element.getBoundingClientRect();
    return {
        width: Math.max(1, rect.width),
        height: Math.max(1, rect.height)
    };
}

export function isCanvasFullscreen(element) {
    if (!element) {
        return false;
    }

    const state = observers.get(element);
    return isElementFullscreen(element, state?.simulatedFullscreen === true);
}

export function isMobileBrowser() {
    return isMobileBrowserCore();
}

// allowSnapshot is kept for the .NET caller's signature: the resize snapshot overlay it enabled is gone,
// the canvas now follows its host live instead of freezing at the old size until resizing stops.
export function attachCanvasHost(element, dotNetRef, allowSnapshot) {
    detachCanvasHost(element);
    patchSkiaCanvasResize();

    const state = {
        resizeObserver: null,
        onFullscreenChange: null,
        onSimulatedFullscreen: null,
        simulatedFullscreen: false,
        mutationObserver: null,
    };

    // The browser delivers ResizeObserver callbacks at most once per frame, so passing every size on
    // keeps the canvas at the real size during a window drag (as the desktop heads do) without flooding.
    const resizeObserver = new ResizeObserver((entries) => {
        for (const entry of entries) {
            notifySize(entry.target, dotNetRef, entry.contentRect.width, entry.contentRect.height);
        }
    });

    const onFullscreenChange = () => {
        const rect = element.getBoundingClientRect();
        notifySize(element, dotNetRef, rect.width, rect.height);
        notifyFullscreen(element, dotNetRef, state.simulatedFullscreen);
    };

    // Handles CSS-simulated fullscreen for browsers/WebViews that don't support the Fullscreen API
    // (e.g. Telegram in-app browser, WKWebView on iOS). The app dispatches 'drawnui-simulated-fullscreen'
    // when it detects that requestFullscreen() is unavailable or fails.
    const onSimulatedFullscreen = (e) => {
        state.simulatedFullscreen = e.detail?.enabled === true;
        notifyFullscreen(element, dotNetRef, state.simulatedFullscreen);
    };

    state.resizeObserver = resizeObserver;
    state.onFullscreenChange = onFullscreenChange;
    state.onSimulatedFullscreen = onSimulatedFullscreen;
    state.mutationObserver = new MutationObserver(() => {});
    observers.set(element, state);

    resizeObserver.observe(element);
    state.mutationObserver.observe(element, { childList: true, subtree: true, attributes: true, attributeFilter: ['width', 'height', 'style'] });
    document.addEventListener('fullscreenchange', onFullscreenChange);
    document.addEventListener('webkitfullscreenchange', onFullscreenChange);
    document.addEventListener('drawnui-simulated-fullscreen', onSimulatedFullscreen);

    const rect = element.getBoundingClientRect();
    notifySize(element, dotNetRef, rect.width, rect.height);
    notifyFullscreen(element, dotNetRef, state.simulatedFullscreen);
}

export function detachCanvasHost(element) {
    const state = observers.get(element);
    if (!state) {
        return;
    }

    state.resizeObserver.disconnect();
    state.mutationObserver?.disconnect();
    document.removeEventListener('fullscreenchange', state.onFullscreenChange);
    document.removeEventListener('webkitfullscreenchange', state.onFullscreenChange);
    document.removeEventListener('drawnui-simulated-fullscreen', state.onSimulatedFullscreen);
    observers.delete(element);
}

export async function setCanvasFullscreen(element, enabled) {
    if (!element) {
        return false;
    }

    const state = observers.get(element);

    // If the Fullscreen API is not available (WebView, Telegram in-app browser, etc.),
    // honour the simulated state that was set via the 'drawnui-simulated-fullscreen' event.
    if (!document.fullscreenEnabled) {
        if (state) state.simulatedFullscreen = enabled;
        return enabled;
    }

    try {
        if (enabled) {
            if (isElementFullscreen(element, state?.simulatedFullscreen)) {
                return true;
            }

            if (document.fullscreenElement && document.fullscreenElement !== element) {
                await document.exitFullscreen();
            }

            if (element.requestFullscreen) {
                await element.requestFullscreen();
            } else if (element.webkitRequestFullscreen) {
                await element.webkitRequestFullscreen();
            }

            return isElementFullscreen(element, state?.simulatedFullscreen);
        }

        if (isElementFullscreen(element, state?.simulatedFullscreen)) {
            if (document.exitFullscreen) {
                await document.exitFullscreen();
            } else if (document.webkitExitFullscreen) {
                await document.webkitExitFullscreen();
            }
        }
    } catch {
    }

    return isElementFullscreen(element, state?.simulatedFullscreen);
}
