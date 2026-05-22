const lifecycleState = globalThis.__drawnUiNativeAppLifecycle ??= {
    attached: false,
    destroyed: false,
    dotNetRef: null,
    screenMetricsQueued: false,
    screenMetricsAttached: false,
    densityMediaQuery: null,
    densityMediaQueryHandler: null,
    safeAreaProbe: null,
    onScreenMetricsChanged: null,
    onVisualViewportResize: null,
    onOrientationChange: null
};

function invoke(methodName, ...args) {
    if (lifecycleState.dotNetRef) {
        return lifecycleState.dotNetRef.invokeMethodAsync(methodName, ...args);
    }
    return globalThis.DotNet?.invokeMethodAsync('DrawnUi.Blazor', methodName, ...args);
}

function onVisibilityChange() {
    if (globalThis.document.visibilityState === 'hidden') {
        invoke('HandleNativeAppHidden');
    } else {
        invoke('HandleNativeAppVisible');
    }
}

function onBeforeUnload() {
    signalDestroyed();
}

function onPageHide(event) {
    if (event?.persisted) {
        invoke('HandleNativeAppHidden');
        return;
    }

    signalDestroyed();
}

function onPageShow() {
    invoke('HandleNativeAppVisible');
    queueScreenMetrics();
}

function parsePixels(value) {
    const parsed = Number.parseFloat(value);
    return Number.isFinite(parsed) ? parsed : 0;
}

function ensureSafeAreaProbe() {
    if (lifecycleState.safeAreaProbe?.isConnected) {
        return lifecycleState.safeAreaProbe;
    }

    const probe = globalThis.document.createElement('div');
    probe.setAttribute('aria-hidden', 'true');
    probe.style.cssText = [
        'position:fixed',
        'inset:0',
        'visibility:hidden',
        'pointer-events:none',
        'padding-top:env(safe-area-inset-top)',
        'padding-right:env(safe-area-inset-right)',
        'padding-bottom:env(safe-area-inset-bottom)',
        'padding-left:env(safe-area-inset-left)'
    ].join(';');
    globalThis.document.documentElement.appendChild(probe);
    lifecycleState.safeAreaProbe = probe;
    return probe;
}

function getSafeAreaInsets() {
    const probe = ensureSafeAreaProbe();
    const style = globalThis.getComputedStyle(probe);
    return {
        top: parsePixels(style.paddingTop),
        right: parsePixels(style.paddingRight),
        bottom: parsePixels(style.paddingBottom),
        left: parsePixels(style.paddingLeft)
    };
}

function getViewportMetrics() {
    const viewport = globalThis.visualViewport;
    const root = globalThis.document.documentElement;
    const width = viewport?.width ?? root?.clientWidth ?? globalThis.window.innerWidth ?? 0;
    const height = viewport?.height ?? root?.clientHeight ?? globalThis.window.innerHeight ?? 0;
    const density = globalThis.window.devicePixelRatio || 1;
    const insets = getSafeAreaInsets();
    return {
        density,
        width,
        height,
        insets
    };
}

function notifyScreenMetrics() {
    const metrics = getViewportMetrics();
    invoke(
        'HandleScreenMetricsChanged',
        metrics.density,
        metrics.width,
        metrics.height,
        metrics.insets.top,
        metrics.insets.right,
        metrics.insets.bottom,
        metrics.insets.left
    );
}

function queueScreenMetrics() {
    if (!lifecycleState.attached || lifecycleState.screenMetricsQueued) {
        return;
    }

    lifecycleState.screenMetricsQueued = true;
    const flush = () => {
        lifecycleState.screenMetricsQueued = false;
        notifyScreenMetrics();
        attachDensityListener();
    };

    if (typeof globalThis.requestAnimationFrame === 'function') {
        globalThis.requestAnimationFrame(flush);
        return;
    }

    globalThis.setTimeout(flush, 0);
}

function detachDensityListener() {
    const query = lifecycleState.densityMediaQuery;
    const handler = lifecycleState.densityMediaQueryHandler;
    if (!query || !handler) {
        return;
    }

    if (typeof query.removeEventListener === 'function') {
        query.removeEventListener('change', handler);
    } else if (typeof query.removeListener === 'function') {
        query.removeListener(handler);
    }

    lifecycleState.densityMediaQuery = null;
    lifecycleState.densityMediaQueryHandler = null;
}

function attachDensityListener() {
    detachDensityListener();

    if (typeof globalThis.matchMedia !== 'function') {
        return;
    }

    const density = globalThis.window.devicePixelRatio || 1;
    const query = globalThis.matchMedia(`(resolution: ${density}dppx)`);
    const handler = () => {
        queueScreenMetrics();
    };

    if (typeof query.addEventListener === 'function') {
        query.addEventListener('change', handler);
    } else if (typeof query.addListener === 'function') {
        query.addListener(handler);
    }

    lifecycleState.densityMediaQuery = query;
    lifecycleState.densityMediaQueryHandler = handler;
}

function attachScreenMetricsWatcher() {
    if (lifecycleState.screenMetricsAttached) {
        return;
    }

    lifecycleState.screenMetricsAttached = true;
    lifecycleState.onScreenMetricsChanged = () => queueScreenMetrics();
    lifecycleState.onVisualViewportResize = () => queueScreenMetrics();
    lifecycleState.onOrientationChange = () => queueScreenMetrics();

    globalThis.window.addEventListener('resize', lifecycleState.onScreenMetricsChanged, true);
    globalThis.window.addEventListener('orientationchange', lifecycleState.onOrientationChange, true);
    globalThis.visualViewport?.addEventListener('resize', lifecycleState.onVisualViewportResize, true);

    attachDensityListener();
    queueScreenMetrics();
}

function signalDestroyed() {
    if (lifecycleState.destroyed) {
        return;
    }

    lifecycleState.destroyed = true;
    invoke('HandleNativeAppDestroyed');
}

export function attachNativeAppLifecycle(dotNetRef) {
    if (lifecycleState.attached) {
        return;
    }

    lifecycleState.dotNetRef = dotNetRef ?? null;
    lifecycleState.attached = true;
    lifecycleState.destroyed = false;

    invoke('HandleNativeAppCreated');

    if (globalThis.document.visibilityState === 'hidden') {
        invoke('HandleNativeAppHidden');
    }

    globalThis.document.addEventListener('visibilitychange', onVisibilityChange, true);
    globalThis.window.addEventListener('pageshow', onPageShow, true);
    globalThis.window.addEventListener('pagehide', onPageHide, true);
    globalThis.window.addEventListener('beforeunload', onBeforeUnload, true);

    attachScreenMetricsWatcher();
}