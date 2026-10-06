const globalStateKey = "__drawnUiKeyboardManager";
const assemblyName = "DrawnUi.Blazor";

function getState() {
    if (!window[globalStateKey]) {
        window[globalStateKey] = {};
    }

    return window[globalStateKey];
}

function invoke(methodName, code, forOtherElement) {
    return DotNet.invokeMethodAsync(assemblyName, methodName, code, forOtherElement).catch(() => {
        // Ignore teardown races during app shutdown/reload.
    });
}

// A page element outside the canvas host (an input, a textarea, a button...) has the key: DrawnUI still sees it,
// but drawn controls must not act on it (KeyboardManager.IsKeyForOtherElement).
function isForOtherElement(event) {
    const t = event.target;
    return !!t && t.nodeType === 1 && t !== document.body && t !== document.documentElement
        && !(t.closest && t.closest(".xaml-canvas"));
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
        const other = isForOtherElement(event);
        invoke("HandleGlobalKeyDown", event.code || null, other);
        if (event.key && event.key.length === 1 && !event.ctrlKey && !event.altKey && !event.metaKey) {
            invoke("HandleGlobalKeyChar", event.key, other);
        }
    };

    state.keyUpHandler = event => {
        invoke("HandleGlobalKeyUp", event.code || null, isForOtherElement(event));
    };

    window.addEventListener("keydown", state.keyDownHandler, true);
    window.addEventListener("keyup", state.keyUpHandler, true);
}

// Called when a drawn text editor (SkiaEditor) gains focus. A drawn editor has no DOM
// element, so DOM focus stays on whatever page text input (e.g. a Monaco code editor)
// last held it — the browser keeps delivering physical keys there. Blur that external
// input so keys reach only the window-level listener that drives the drawn editor.
// Scoped to text-entry elements OUTSIDE the canvas host; leaves everything else alone.
export function blurExternalTextInput() {
    const a = document.activeElement;
    if (a
        && (a.tagName === "INPUT" || a.tagName === "TEXTAREA" || a.isContentEditable)
        && !(a.closest && a.closest(".xaml-canvas"))) {
        a.blur();
    }
}