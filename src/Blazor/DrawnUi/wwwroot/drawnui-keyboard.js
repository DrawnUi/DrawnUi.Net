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