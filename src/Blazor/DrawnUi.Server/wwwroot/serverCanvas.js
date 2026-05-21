const resizeObservers = new WeakMap();

export function getRelativePoint(element, clientX, clientY, width, height) {
    const rect = element.getBoundingClientRect();
    const safeWidth = rect.width || width || 1;
    const safeHeight = rect.height || height || 1;

    const x = (clientX - rect.left) * (width / safeWidth);
    const y = (clientY - rect.top) * (height / safeHeight);

    return {
        x: Math.max(0, Math.min(width, x)),
        y: Math.max(0, Math.min(height, y))
    };
}

export function getElementSize(element) {
    return {
        width: element?.clientWidth || 0,
        height: element?.clientHeight || 0
    };
}

function notifyHostResized(dotNetRef, width, height) {
    if (!dotNetRef) {
        return;
    }

    dotNetRef.invokeMethodAsync(
        'OnHostResized',
        Math.max(0, Math.round(width || 0)),
        Math.max(0, Math.round(height || 0))
    );
}

export function attachResizeObserver(element, dotNetRef) {
    detachResizeObserver(element);

    if (!element || !dotNetRef) {
        return;
    }

    let timer = null;
    let lastWidth = -1;
    let lastHeight = -1;
    const visualViewport = window.visualViewport;

    const flush = (width, height) => {
        const nextWidth = Math.max(0, Math.round(width || 0));
        const nextHeight = Math.max(0, Math.round(height || 0));

        if (nextWidth === lastWidth && nextHeight === lastHeight) {
            return;
        }

        lastWidth = nextWidth;
        lastHeight = nextHeight;
        notifyHostResized(dotNetRef, nextWidth, nextHeight);
    };

    const scheduleMeasure = () => {
        clearTimeout(timer);
        timer = setTimeout(() => {
            timer = null;
            const size = getElementSize(element);
            flush(size.width, size.height);
        }, 80);
    };

    const observer = typeof ResizeObserver === 'function'
        ? new ResizeObserver(() => {
            scheduleMeasure();
        })
        : null;

    const onWindowResize = () => {
        scheduleMeasure();
    };

    if (observer) {
        observer.observe(element);
    }

    window.addEventListener('resize', onWindowResize);
    window.addEventListener('orientationchange', onWindowResize);
    visualViewport?.addEventListener('resize', onWindowResize);

    const size = getElementSize(element);
    lastWidth = Math.max(0, Math.round(size.width || 0));
    lastHeight = Math.max(0, Math.round(size.height || 0));

    resizeObservers.set(element, {
        observer,
        onWindowResize,
        visualViewport,
        clearTimer: () => {
            clearTimeout(timer);
            timer = null;
        }
    });
}

export function detachResizeObserver(element) {
    const state = resizeObservers.get(element);
    if (!state) {
        return;
    }

    state.clearTimer();
    state.observer?.disconnect();
    window.removeEventListener('resize', state.onWindowResize);
    window.removeEventListener('orientationchange', state.onWindowResize);
    state.visualViewport?.removeEventListener('resize', state.onWindowResize);
    resizeObservers.delete(element);
}

function revokeFrameUrl(imageElement) {
    const url = imageElement?.__drawnUiFrameUrl;
    if (!url) {
        delete imageElement?.__drawnUiFrameVersion;
        return;
    }

    delete imageElement.__drawnUiFrameUrl;
    delete imageElement.__drawnUiFrameVersion;
    delete imageElement.__drawnUiContentType;

    try {
        URL.revokeObjectURL(url);
    } catch {
    }
}

export function setImageFrame(imageElement, version, bytes, contentType) {
    if (!imageElement || !bytes) {
        return;
    }

    const nextVersion = Number.isFinite(version) ? Number(version) : 0;
    const appliedVersion = Number(imageElement.__drawnUiFrameVersion || 0);
    if (nextVersion !== 0 && nextVersion < appliedVersion) {
        return;
    }

    const payload = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
    if (payload.byteLength === 0) {
        return;
    }

    const nextUrl = URL.createObjectURL(new Blob([payload], {
        type: contentType || 'image/png'
    }));
    const previousUrl = imageElement.__drawnUiFrameUrl;

    imageElement.src = nextUrl;
    imageElement.__drawnUiFrameUrl = nextUrl;
    imageElement.__drawnUiFrameVersion = nextVersion;
    imageElement.__drawnUiContentType = contentType || 'image/png';

    if (previousUrl && previousUrl !== nextUrl) {
        setTimeout(() => {
            try {
                URL.revokeObjectURL(previousUrl);
            } catch {
            }
        }, 0);
    }
}

export function disposeImageFrame(imageElement) {
    revokeFrameUrl(imageElement);
}

export function configureTapEffects(imageElement, container, width, height, effectBounds) {
    if (!imageElement || !container) {
        return;
    }

    imageElement.__drawnUiTapEffectConfig = {
        container,
        width,
        height,
        effectBounds: effectBounds || []
    };

    if (imageElement.__drawnUiTapEffectHandler) {
        return;
    }

    const handler = (event) => {
        const config = imageElement.__drawnUiTapEffectConfig;
        if (!config || !Array.isArray(config.effectBounds) || config.effectBounds.length === 0) {
            return;
        }

        const point = getRelativePoint(imageElement, event.clientX, event.clientY, config.width, config.height);
        const bounds = config.effectBounds.find(candidate =>
            point.x >= candidate.left &&
            point.x <= candidate.left + candidate.width &&
            point.y >= candidate.top &&
            point.y <= candidate.top + candidate.height);

        if (!bounds) {
            return;
        }

        showTapEffect(config.container, event.clientX, event.clientY, config.width, config.height, bounds);
    };

    imageElement.__drawnUiTapEffectHandler = handler;
    imageElement.addEventListener('pointerdown', handler);
}

export function showTapEffect(container, clientX, clientY, width, height, effectBounds) {
    if (!container || !effectBounds) {
        return;
    }

    const rect = container.getBoundingClientRect();
    const scaleX = rect.width / (width || 1);
    const scaleY = rect.height / (height || 1);

    const overlay = document.createElement('span');
    overlay.style.position = 'absolute';
    overlay.style.left = `${effectBounds.left * scaleX}px`;
    overlay.style.top = `${effectBounds.top * scaleY}px`;
    overlay.style.width = `${effectBounds.width * scaleX}px`;
    overlay.style.height = `${effectBounds.height * scaleY}px`;
    overlay.style.borderRadius = `${effectBounds.radius * scaleX}px`;
    overlay.style.overflow = 'hidden';
    overlay.style.pointerEvents = 'none';
    overlay.style.zIndex = '2';

    const ripple = document.createElement('span');
    ripple.style.position = 'absolute';
    ripple.style.left = `${clientX - rect.left - effectBounds.left * scaleX}px`;
    ripple.style.top = `${clientY - rect.top - effectBounds.top * scaleY}px`;
    ripple.style.width = '18px';
    ripple.style.height = '18px';
    ripple.style.borderRadius = '999px';
    ripple.style.background = 'rgba(255, 255, 255, 0.42)';
    ripple.style.border = '1px solid rgba(17, 17, 17, 0.08)';
    ripple.style.boxShadow = '0 0 0 2px rgba(255, 255, 255, 0.12)';
    ripple.style.pointerEvents = 'none';
    ripple.style.opacity = '0.95';
    ripple.style.transform = 'translate(-50%, -50%) scale(0.2)';
    ripple.style.transition = 'transform 420ms ease-out, opacity 420ms ease-out';

    overlay.appendChild(ripple);
    container.appendChild(overlay);

    requestAnimationFrame(() => {
        ripple.style.transform = 'translate(-50%, -50%) scale(9)';
        ripple.style.opacity = '0';
    });

    setTimeout(() => {
        overlay.remove();
    }, 450);
}