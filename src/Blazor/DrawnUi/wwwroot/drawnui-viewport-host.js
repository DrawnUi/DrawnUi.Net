const observers = new WeakMap();

function reportOffset(element, dotNetRef) {
    if (!element || !dotNetRef) {
        return;
    }

    const rect = element.getBoundingClientRect();
    const offset = Math.max(0, rect.top);
    dotNetRef.invokeMethodAsync('OnViewportOffsetChanged', offset);
}

export function attachViewportOffsetObserver(element, dotNetRef) {
    detachViewportOffsetObserver(element);

    if (!element) {
        return;
    }

    const observedTarget = element.parentElement ?? element;
    let frameRequested = false;

    const scheduleReport = () => {
        if (frameRequested) {
            return;
        }

        frameRequested = true;
        requestAnimationFrame(() => {
            frameRequested = false;
            reportOffset(element, dotNetRef);
        });
    };

    const resizeObserver = new ResizeObserver(() => {
        scheduleReport();
    });

    resizeObserver.observe(observedTarget);
    window.addEventListener('resize', scheduleReport);
    window.addEventListener('scroll', scheduleReport, { passive: true });

    observers.set(element, {
        resizeObserver,
        scheduleReport
    });

    scheduleReport();
}

export function refreshViewportOffset(element) {
    const state = observers.get(element);
    if (!state) {
        return;
    }

    state.scheduleReport();
}

export function detachViewportOffsetObserver(element) {
    const state = observers.get(element);
    if (!state) {
        return;
    }

    state.resizeObserver.disconnect();
    window.removeEventListener('resize', state.scheduleReport);
    window.removeEventListener('scroll', state.scheduleReport);
    observers.delete(element);
}
