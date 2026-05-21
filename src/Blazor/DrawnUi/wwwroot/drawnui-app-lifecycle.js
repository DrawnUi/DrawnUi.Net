const lifecycleState = globalThis.__drawnUiNativeAppLifecycle ??= {
    attached: false,
    destroyed: false,
    dotNetRef: null
};

function invoke(methodName) {
    if (lifecycleState.dotNetRef) {
        return lifecycleState.dotNetRef.invokeMethodAsync(methodName);
    }
    return globalThis.DotNet?.invokeMethodAsync('DrawnUi.Blazor', methodName);
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
}