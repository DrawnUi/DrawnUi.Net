const observers = new WeakMap();

function isElementFullscreen(element) {
    return document.fullscreenElement === element || document.webkitFullscreenElement === element;
}

function notifyFullscreen(element, dotNetRef) {
    dotNetRef.invokeMethodAsync('OnFullscreenChanged', isElementFullscreen(element));
}

function notifySize(element, dotNetRef, width, height) {
    const nextWidth = Math.max(1, Math.round(width));
    const nextHeight = Math.max(1, Math.round(height));
    dotNetRef.invokeMethodAsync('OnHostResized', nextWidth, nextHeight);
}

export function getHostSize(element) {
    const rect = element.getBoundingClientRect();
    return {
        width: Math.max(1, Math.round(rect.width)),
        height: Math.max(1, Math.round(rect.height))
    };
}

function showSnapshot(element, state) {
    if (state.snapshotImg) return;
    const canvas = element.querySelector('canvas');
    if (!canvas || !state.allowSnapshot) return;
    let dataUrl;
    try {
        dataUrl = canvas.toDataURL('image/png');
    } catch {
        return;
    }
    const img = document.createElement('img');
    img.src = dataUrl;
    img.style.cssText = 'position:absolute;inset:0;width:100%;height:100%;z-index:10;opacity:1;transition:opacity 0.2s ease;pointer-events:none;';
    element.style.position = element.style.position || 'relative';
    element.appendChild(img);
    state.snapshotImg = img;
}

function fadeSnapshot(state) {
    const img = state.snapshotImg;
    if (!img) return;
    state.snapshotImg = null;
    img.style.opacity = '0';
    setTimeout(() => img.parentNode && img.parentNode.removeChild(img), 220);
}

export function attachCanvasHost(element, dotNetRef, allowSnapshot) {
    detachCanvasHost(element);

    let resizeTimer = null;
    let resizePending = false;
    let hasFirstSize = false;
    let hasFirstPaint = false;

    const state = {
        resizeObserver: null,
        onFullscreenChange: null,
        snapshotImg: null,
        allowSnapshot: allowSnapshot === true,
        get resizeTimer() { return resizeTimer; },
        clearTimer() { clearTimeout(resizeTimer); resizeTimer = null; }
    };

    const resizeObserver = new ResizeObserver((entries) => {
        for (const entry of entries) {
            const box = entry.contentRect;
            const w = box.width;
            const h = box.height;

            if (hasFirstSize && hasFirstPaint && !resizePending) {
                resizePending = true;
                showSnapshot(element, state);
            }

            clearTimeout(resizeTimer);
            resizeTimer = setTimeout(() => {
                resizePending = false;
                fadeSnapshot(state);
                notifySize(entry.target, dotNetRef, w, h);
                hasFirstPaint = true;
            }, 80);
        }
    });

    const onFullscreenChange = () => {
        const rect = element.getBoundingClientRect();
        notifySize(element, dotNetRef, rect.width, rect.height);
        notifyFullscreen(element, dotNetRef);
    };

    state.resizeObserver = resizeObserver;
    state.onFullscreenChange = onFullscreenChange;
    observers.set(element, state);

    resizeObserver.observe(element);
    document.addEventListener('fullscreenchange', onFullscreenChange);
    document.addEventListener('webkitfullscreenchange', onFullscreenChange);

    const rect = element.getBoundingClientRect();
    notifySize(element, dotNetRef, rect.width, rect.height);
    notifyFullscreen(element, dotNetRef);
    hasFirstSize = true;
}

export function detachCanvasHost(element) {
    const state = observers.get(element);
    if (!state) {
        return;
    }

    state.clearTimer();
    fadeSnapshot(state);
    state.resizeObserver.disconnect();
    document.removeEventListener('fullscreenchange', state.onFullscreenChange);
    document.removeEventListener('webkitfullscreenchange', state.onFullscreenChange);
    observers.delete(element);
}

export async function setCanvasFullscreen(element, enabled) {
    if (!element) {
        return false;
    }

    try {
        if (enabled) {
            if (isElementFullscreen(element)) {
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

            return isElementFullscreen(element);
        }

        if (isElementFullscreen(element)) {
            if (document.exitFullscreen) {
                await document.exitFullscreen();
            } else if (document.webkitExitFullscreen) {
                await document.webkitExitFullscreen();
            }
        }
    } catch {
    }

    return isElementFullscreen(element);
}
