const globalStateKey = "__drawnUiKeyboardManager";
const assemblyName = "DrawnUi.Blazor";

function getState() {
    if (!window[globalStateKey]) {
        window[globalStateKey] = {};
    }

    return window[globalStateKey];
}

function invoke(methodName, code) {
    return DotNet.invokeMethodAsync(assemblyName, methodName, code).catch(() => {
        // Ignore teardown races during app shutdown/reload.
    });
}

export function attachGlobalKeyboard() {
    const state = getState();
    if (state.keyDownHandler && state.keyUpHandler) {
        return;
    }

    // When this app runs inside a cross-frame <iframe>, the embedding page keeps real
    // browser/DOM focus by default: a bare <canvas> has no tabindex, so a tap/click on it
    // never causes the browser to hand keyboard focus to this window, and physical
    // keydown/keyup here never fire. window.focus() is an explicit script call (unlike a
    // browser default action), so it still works even if the gesture pipeline preventDefault()s
    // the same pointerdown.
    if (!state.focusStealHandler) {
        state.focusStealHandler = () => window.focus();
        window.addEventListener("pointerdown", state.focusStealHandler, true);
    }

    state.keyDownHandler = event => {
        invoke("HandleGlobalKeyDown", event.code || null);
        if (event.key && event.key.length === 1 && !event.ctrlKey && !event.altKey && !event.metaKey) {
            invoke("HandleGlobalKeyChar", event.key);
        }
    };

    state.keyUpHandler = event => {
        invoke("HandleGlobalKeyUp", event.code || null);
    };

    window.addEventListener("keydown", state.keyDownHandler, true);
    window.addEventListener("keyup", state.keyUpHandler, true);
}