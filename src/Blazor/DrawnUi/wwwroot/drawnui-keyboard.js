const globalStateKey = "__drawnUiKeyboardManager";
const assemblyName = "DrawnUi.Blazor";

function getState() {
    if (!window[globalStateKey]) {
        window[globalStateKey] = {};
    }

    return window[globalStateKey];
}

// Calls the .NET side. Through the object reference .NET passed to attachGlobalKeyboard: a page that runs
// several .NET runtimes (Blazor Server + WebAssembly) cannot resolve a static DotNet.invokeMethodAsync.
function call(methodName, ...args) {
    try {
        const dotnet = getState().dotnet;
        const pending = dotnet
            ? dotnet.invokeMethodAsync(methodName, ...args)
            : DotNet.invokeMethodAsync(assemblyName, methodName, ...args);
        return pending.catch(() => { /* teardown races during app shutdown/reload */ });
    } catch {
        return Promise.resolve();
    }
}

function invoke(methodName, code, forOtherElement) {
    return call(methodName, code, forOtherElement);
}

// A page element outside the canvas host (an input, a textarea, a button...) has the key: DrawnUI still sees it,
// but drawn controls must not act on it (KeyboardManager.IsKeyForOtherElement).
function isForOtherElement(event) {
    const t = event.target;
    return !!t && t.nodeType === 1 && t !== document.body && t !== document.documentElement && t !== getState().textInput
        && !(t.closest && t.closest(".xaml-canvas"));
}

const FIELD_KEYS = new Set(["Space", "ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "Home", "End", "PageUp", "PageDown"]);

function isTextFieldElement(t) {
    return !!t && t.nodeType === 1 && !!(t.closest && t.closest(".xaml-a11y-overlay"))
        && (t.getAttribute("role") === "textbox" || t.getAttribute("role") === "searchbox");
}

export function attachGlobalKeyboard(dotnet) {
    const state = getState();
    if (dotnet) state.dotnet = dotnet;
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
        if (state.textInput && event.target === state.textInput) {
            textInputKeyDown(event, state);
            return;
        }
        const other = isForOtherElement(event);
        // a drawn text field whose overlay element has the page focus: the keys that would scroll the page are the
        // field's (a space, the caret keys)
        if (isTextFieldElement(event.target) && FIELD_KEYS.has(event.code)) {
            event.preventDefault();
        }
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
    attachTouchTextInput(state);
    watchKeyboardTop(state);
}

// The on-screen keyboard's top edge in page points (the browser shrinks the visual viewport under it), -1 when none,
// to .NET whenever it changes (Super.KeyboardTop). Pinch zoom shrinks the viewport too, so a zoomed page reports none.
function watchKeyboardTop(state) {
    const vv = window.visualViewport;
    if (!vv || state.keyboardTopHandler) return;
    let last;
    state.keyboardTopHandler = () => {
        const bottom = vv.offsetTop + vv.height;
        const covered = window.innerHeight - bottom;
        const top = vv.scale > 1.05 || covered < 80 ? -1 : bottom; // under 80: the browser's own bars moving
        if (top === last) return;
        last = top;
        call("HandleKeyboardTop", top);
    };
    vv.addEventListener("resize", state.keyboardTopHandler);
    vv.addEventListener("scroll", state.keyboardTopHandler);
    state.keyboardTopHandler();
}

/** An element's bottom edge in page points (the coordinates of the keyboard top), NaN without one. */
export function elementBottom(element) {
    return element && element.getBoundingClientRect ? element.getBoundingClientRect().bottom : NaN;
}

// --- On-screen keyboard for drawn editors (phones) ---
// iOS opens its keyboard only for a text field focused synchronously inside the tap's touchend, and the mouse events
// it sends after a touch take the page focus back to the page. The pointer events reach .NET asynchronously, too late
// for that, so the tap is recognized here: a tap that ends on a text field's overlay element gives a hidden textarea
// the page focus at once (the drawn editor takes its own focus from the same tap), and the touchend's default is
// prevented while that textarea has the focus. What the keyboard types reaches the editor as text (HandleTextInput) or
// as an editing key (HandleTextKey); the textarea keeps two spaces with its caret at the end, so a Backspace always has
// something to delete and is reported. A field focused by the mouse or the keyboard (Tab) uses the same textarea, placed
// at the field, so an input method (Chinese, Japanese, Korean) composes into it and shows its candidates there.
const TEXT_INPUT_REST = "  ";
const EDITOR_KEYS = new Set(["Backspace", "Delete", "Enter", "NumpadEnter",
    "ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "Home", "End", "PageUp", "PageDown"]);
const TAP_SLOP = 10, GIVE_BACK_MS = 1500, TOUCH_RECENT_MS = 1000;

function graphemes(s) {
    if (typeof Intl !== "undefined" && Intl.Segmenter)
        return [...new Intl.Segmenter().segment(s)].map(x => x.segment);
    return [...s];
}

function sendText(text) {
    call("HandleTextInput", text);
}

function sendKey(code) {
    call("HandleTextKey", code);
}

function restTextInput(el) {
    el.value = TEXT_INPUT_REST;
    el.setSelectionRange(TEXT_INPUT_REST.length, TEXT_INPUT_REST.length);
}

// the composition shown in the editor follows the input method: what changed since its last text is deleted and typed again
function applyComposition(state, next) {
    const before = graphemes(state.composing), after = graphemes(next);
    let same = 0;
    while (same < before.length && same < after.length && before[same] === after[same]) same++;
    for (let i = before.length - same; i > 0; i--) sendKey("Backspace");
    if (after.length > same) sendText(after.slice(same).join(""));
    state.composing = next;
}

function ensureTextInput(state) {
    if (state.textInput) return state.textInput;
    const el = document.createElement("textarea");
    for (const [name, value] of [["autocomplete", "off"], ["autocorrect", "off"], ["autocapitalize", "off"],
        ["spellcheck", "false"], ["aria-hidden", "true"], ["tabindex", "-1"]])
        el.setAttribute(name, value);
    // 16px: iOS zooms the page into a focused field with a smaller font
    el.style.cssText = "position:fixed;left:0;top:0;width:1px;height:1px;opacity:0;border:0;padding:0;margin:0;"
        + "resize:none;overflow:hidden;font-size:16px;color:transparent;background:transparent;caret-color:transparent;"
        + "pointer-events:none;z-index:-1";
    state.composing = "";
    el.addEventListener("beforeinput", e => {
        switch (e.inputType) {
            case "insertText":
                if (e.data) sendText(e.data);
                e.preventDefault();
                break;
            case "insertLineBreak":
            case "insertParagraph":
                sendKey("Enter");
                e.preventDefault();
                break;
            case "deleteContentBackward":
                if (!state.composing) {
                    sendKey("Backspace");
                    e.preventDefault();
                }
                break;
            case "deleteContentForward":
                sendKey("Delete");
                e.preventDefault();
                break;
            case "insertFromPaste":
            case "insertFromDrop":
                e.preventDefault(); // the paste event typed it
                break;
        }
    });
    el.addEventListener("paste", e => {
        const text = e.clipboardData && e.clipboardData.getData("text/plain");
        e.preventDefault();
        if (text) sendText(text);
    });
    el.addEventListener("compositionstart", () => { state.composing = ""; });
    el.addEventListener("compositionupdate", e => applyComposition(state, e.data || ""));
    el.addEventListener("compositionend", e => {
        applyComposition(state, e.data || "");
        state.composing = "";
        restTextInput(el);
    });
    document.body.appendChild(el);
    state.textInput = el;
    restTextInput(el);
    return el;
}

function textInputKeyDown(e, state) {
    if (e.isComposing || e.keyCode === 229) return; // the input method has the key
    if (e.code === "Tab") {
        // Tab moves on from the editor's own overlay element, in the overlay's order
        state.textOwner?.focus({ preventScroll: true });
        return;
    }
    const shortcut = e.ctrlKey || e.metaKey;
    if (EDITOR_KEYS.has(e.code) || (shortcut && e.code === "KeyA")) e.preventDefault();
    // Ctrl / Cmd + V: the textarea's paste event types the text (no clipboard permission needed)
    if (shortcut && e.code === "KeyV") return;
    // its text comes from the textarea's input; editing keys and shortcuts (Ctrl+C, Ctrl+X, Ctrl+A...) go as keys
    if (e.code) invoke("HandleGlobalKeyDown", e.code, false);
}

// the text field's overlay element under a point of the page, the topmost one
function textFieldAt(host, x, y) {
    const fields = host.querySelectorAll('.xaml-a11y-overlay [role="textbox"], .xaml-a11y-overlay [role="searchbox"]');
    for (let i = fields.length - 1; i >= 0; i--) {
        const f = fields[i];
        if (f.getAttribute("aria-disabled") === "true") continue;
        const r = f.getBoundingClientRect();
        if (r.width > 0 && r.height > 0 && x >= r.left && x <= r.right && y >= r.top && y <= r.bottom) return f;
    }
    return null;
}

function attachTouchTextInput(state) {
    if (state.touchStartHandler) return;
    state.touchStartHandler = e => {
        state.touching = true;
        state.lastTouchAt = performance.now();
        const host = e.target && e.target.closest && e.target.closest(".xaml-canvas");
        const t = e.touches.length === 1 ? e.touches[0] : null;
        state.tap = host && t ? { host, x: t.clientX, y: t.clientY, at: performance.now() } : null;
    };
    state.touchEndHandler = e => {
        state.touching = e.touches.length > 0;
        state.lastTouchAt = performance.now();
        const host = e.target && e.target.closest && e.target.closest(".xaml-canvas");
        if (!host) return;
        const tap = state.tap;
        state.tap = null;
        const t = e.changedTouches[0];
        // a finger that lifts where it went down: a tap, or a long press (which focuses the field too)
        if (tap && t && e.touches.length === 0 && tap.host === host
            && Math.abs(t.clientX - tap.x) < TAP_SLOP && Math.abs(t.clientY - tap.y) < TAP_SLOP) {
            const field = textFieldAt(host, t.clientX, t.clientY);
            if (field) {
                const el = ensureTextInput(state);
                placeTextInput(el, null);
                state.textOwner = field;
                state.textOwnerId = field.getAttribute("data-a11y-id");
                // a tap on the focused field again: focus anew, so a keyboard the user closed opens again
                if (document.activeElement === el) el.blur();
                state.composing = "";
                restTextInput(el);
                // the overlay element losing the focus to it is told synchronously, while the page has no focused
                // element yet: textInputHasFocus answers for the textarea during that
                state.textInputFocusing = true;
                try { el.focus({ preventScroll: true }); } finally { state.textInputFocusing = false; }
                call("HandleSoftKeyboard", true);
                giveBackIfNotTaken(state);
            }
        }
        // while the textarea has the page focus, no mouse events follow the touch: they would take the focus back to
        // the page and close the keyboard. The drawn controls decide whether the editor keeps its focus.
        if (state.textInput && document.activeElement === state.textInput) e.preventDefault();
    };
    document.addEventListener("touchstart", state.touchStartHandler, { capture: true, passive: true });
    document.addEventListener("touchend", state.touchEndHandler, { capture: true, passive: false });
    // iOS Safari takes the second tap of a double tap for itself: no pointer or touch event for it, only click, click
    // and dblclick. A dblclick on a canvas that follows ONE touch release (the other browsers deliver both taps first)
    // brings that tap back as a touch tap at its place, so a double tap reaches the controls (a 3D viewer's reset), as
    // in DrawnUi.React. For a mouse, or when both taps came, it adds nothing.
    const touchUps = [];
    let replaying = false;
    document.addEventListener("pointerup", e => {
        if (replaying || e.pointerType === "mouse" || !(e.target && e.target.closest && e.target.closest(".xaml-canvas"))) return;
        touchUps.push(performance.now());
        if (touchUps.length > 2) touchUps.shift();
    }, true);
    document.addEventListener("dblclick", e => {
        const target = e.target;
        if (!(target && target.closest && target.closest(".xaml-canvas"))) return;
        const now = performance.now(), recent = touchUps.filter(t => now - t < 700);
        touchUps.length = 0;
        if (recent.length !== 1 || typeof PointerEvent !== "function") return;
        const at = { pointerId: 2000000 + Math.floor(now), pointerType: "touch", isPrimary: true, clientX: e.clientX,
            clientY: e.clientY, screenX: e.screenX, screenY: e.screenY, button: 0, bubbles: true, cancelable: true, composed: true };
        replaying = true;
        try {
            target.dispatchEvent(new PointerEvent("pointerdown", { ...at, buttons: 1 }));
            target.dispatchEvent(new PointerEvent("pointerup", { ...at, buttons: 0 }));
        } finally {
            replaying = false;
        }
    }, true);
    // a mouse press on the canvas leaves the textarea its focus: the drawn controls decide whether the editor keeps it
    document.addEventListener("mousedown", e => {
        if (state.textInput && document.activeElement === state.textInput && e.target && e.target.closest
            && e.target.closest(".xaml-canvas")) e.preventDefault();
    }, true);
}

// Focused by a touch: the textarea stays in the page's corner (over a field low on the screen iOS would pan the whole
// page to reveal it). Focused by the mouse or the keyboard: it sits at the field's bottom-left, where an input method
// shows its candidates, and follows the field while the page scrolls (the overlay element moves with the drawn field).
function placeTextInput(el, owner) {
    const state = getState();
    if (state.textFollow) {
        state.textFollow.disconnect();
        state.textFollow = null;
    }
    const place = () => {
        const r = owner ? owner.getBoundingClientRect() : null;
        const placed = r && r.width > 0;
        el.style.left = placed ? r.left + "px" : "0px";
        el.style.top = placed ? Math.max(0, r.bottom - 1) + "px" : "0px";
        return placed;
    };
    if (place() && typeof MutationObserver === "function") {
        state.textFollow = new MutationObserver(place);
        state.textFollow.observe(owner, { attributes: true, attributeFilter: ["style"] });
    }
}

// a tap the editor does not take as focus (a read-only field) gives the page focus back
function giveBackIfNotTaken(state) {
    clearTimeout(state.giveBackTimer);
    const started = state.editorFocusCount || 0;
    state.giveBackTimer = setTimeout(() => {
        if (!state.editorFocused && (state.editorFocusCount || 0) === started && document.activeElement === state.textInput)
            state.textInput.blur();
    }, GIVE_BACK_MS);
}

// A drawn editor (ownerId: its overlay node) took (true) or gave back (false) its focus. Giving it back closes the
// on-screen keyboard, unless a tap already gave the keyboard to another field (Name, then Email).
export function textInputFocus(focus, ownerId) {
    const state = getState();
    const id = String(ownerId);
    if (focus) {
        state.editorFocused = true;
        state.editorFocusCount = (state.editorFocusCount || 0) + 1;
        state.textOwnerId = id;
        const el = ensureTextInput(state);
        if (document.activeElement !== el) {
            // focused by the mouse, the keyboard or code: the textarea takes the page focus at the field. A finger that
            // focused it some other way than a tap (a long press, a drag from it): the on-screen keyboard's, as a tap.
            const owner = document.querySelector('.xaml-a11y-overlay [data-a11y-id="' + id + '"]');
            const byTouch = state.touching || performance.now() - (state.lastTouchAt || -1e9) < TOUCH_RECENT_MS;
            state.textOwner = owner;
            state.composing = "";
            restTextInput(el);
            placeTextInput(el, byTouch ? null : owner);
            state.textInputFocusing = true;
            try { el.focus({ preventScroll: true }); } finally { state.textInputFocusing = false; }
            if (byTouch) call("HandleSoftKeyboard", true);
        }
        return;
    }
    if (state.textOwnerId != null && state.textOwnerId !== id) return;
    // the field that had the on-screen keyboard gave it back (not the one a tap just moved from)
    call("HandleSoftKeyboard", false);
    state.editorFocused = false;
    state.textOwnerId = null;
    state.textOwner = null;
    if (state.textInput) placeTextInput(state.textInput, null);
    if (state.textInput && document.activeElement === state.textInput) state.textInput.blur();
}

// the hidden textarea has the page focus: the editor's overlay element must not take it (that closes the keyboard)
export function textInputHasFocus() {
    const state = getState();
    return !!state.textInput && (state.textInputFocusing || document.activeElement === state.textInput);
}

// Called when a drawn text editor (SkiaEditor) gains focus. A drawn editor has no DOM
// element, so DOM focus stays on whatever page text input (e.g. a Monaco code editor)
// last held it — the browser keeps delivering physical keys there. Blur that external
// input so keys reach only the window-level listener that drives the drawn editor.
// Scoped to text-entry elements OUTSIDE the canvas host; leaves everything else alone.
export function blurExternalTextInput() {
    const a = document.activeElement;
    if (a && a !== getState().textInput
        && (a.tagName === "INPUT" || a.tagName === "TEXTAREA" || a.isContentEditable)
        && !(a.closest && a.closest(".xaml-canvas"))) {
        a.blur();
    }
}