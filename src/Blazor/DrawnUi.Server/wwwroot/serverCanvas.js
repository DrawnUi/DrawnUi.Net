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