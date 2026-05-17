const overlayId = 'drawnui-picker-overlay';

function getPickerTheme(styleName) {
    switch ((styleName || '').toLowerCase()) {
        case 'cupertino':
            return {
                overlayBackground: 'rgba(17, 17, 19, 0.36)',
                cardBackground: '#ffffff',
                cardColor: '#1c1c1e',
                cardRadius: '26px',
                cardBorder: '1px solid rgba(60, 60, 67, 0.18)',
                cardShadow: '0 26px 72px rgba(0, 0, 0, 0.18)',
                titleFont: '600 19px -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
                subtitleFont: '400 13px -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
                subtitleColor: '#6c6c70',
                buttonRadius: '18px',
                buttonBorder: '1px solid rgba(60, 60, 67, 0.16)',
                buttonBackground: '#fbfbfd',
                buttonColor: '#1c1c1e',
                buttonHoverBackground: '#f1f2f6',
                buttonHoverBorder: 'rgba(60, 60, 67, 0.24)',
                selectedBackground: '#e9f2ff',
                selectedBorder: '#7aa8ff',
                selectedColor: '#123a78',
                cancelBackground: '#f4f4f8'
            };
        case 'material':
            return {
                overlayBackground: 'rgba(32, 33, 36, 0.46)',
                cardBackground: '#f8f9fa',
                cardColor: '#202124',
                cardRadius: '12px',
                cardBorder: '1px solid rgba(95, 99, 104, 0.22)',
                cardShadow: '0 20px 48px rgba(60, 64, 67, 0.3)',
                titleFont: '600 20px Roboto, "Segoe UI", sans-serif',
                subtitleFont: '400 13px Roboto, "Segoe UI", sans-serif',
                subtitleColor: '#5f6368',
                buttonRadius: '12px',
                buttonBorder: '1px solid #dadce0',
                buttonBackground: '#ffffff',
                buttonColor: '#202124',
                buttonHoverBackground: '#f1f3f4',
                buttonHoverBorder: '#1a73e8',
                selectedBackground: '#e8f0fe',
                selectedBorder: '#1a73e8',
                selectedColor: '#174ea6',
                cancelBackground: '#eef2f7'
            };
        case 'windows':
        default:
            return {
                overlayBackground: 'rgba(15, 15, 15, 0.32)',
                cardBackground: '#f3f3f3',
                cardColor: '#111111',
                cardRadius: '8px',
                cardBorder: '1px solid rgba(117, 117, 117, 0.32)',
                cardShadow: '0 18px 42px rgba(0, 0, 0, 0.18)',
                titleFont: '600 20px "Segoe UI Variable Text", "Segoe UI", sans-serif',
                subtitleFont: '400 13px "Segoe UI Variable Text", "Segoe UI", sans-serif',
                subtitleColor: '#616161',
                buttonRadius: '8px',
                buttonBorder: '1px solid #c7c7c7',
                buttonBackground: '#ffffff',
                buttonColor: '#111111',
                buttonHoverBackground: '#f3f2f1',
                buttonHoverBorder: '#8a8a8a',
                selectedBackground: '#e5f1fb',
                selectedBorder: '#005fb8',
                selectedColor: '#0f3d75',
                cancelBackground: '#ececec'
            };
    }
}

function removeExistingOverlay() {
    document.getElementById(overlayId)?.remove();
}

function createButton(label, onClick, theme, styles = {}) {
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = label;
    Object.assign(button.style, {
        width: '100%',
        border: theme.buttonBorder,
        borderRadius: theme.buttonRadius,
        background: theme.buttonBackground,
        color: theme.buttonColor,
        padding: '12px 14px',
        textAlign: 'left',
        font: `500 15px ${theme.titleFont.split(' ').slice(2).join(' ')}`,
        cursor: 'pointer',
        transition: 'background 120ms ease, border-color 120ms ease',
        boxSizing: 'border-box'
    }, styles);

    button.addEventListener('mouseenter', () => {
        button.style.background = styles.background ?? theme.buttonHoverBackground;
        button.style.borderColor = styles.borderColor ?? theme.buttonHoverBorder;
    });

    button.addEventListener('mouseleave', () => {
        button.style.background = styles.background ?? theme.buttonBackground;
        button.style.borderColor = styles.borderColor ?? theme.buttonBorder.replace('1px solid ', '');
    });

    button.addEventListener('click', onClick);
    return button;
}

export function showPickerPrompt(title, cancelText, options, selectedIndex, styleName) {
    if (!Array.isArray(options) || options.length === 0) {
        return Promise.resolve(-1);
    }

    removeExistingOverlay();

    const header = title && title.trim().length > 0 ? title.trim() : 'Select an item';
    const dismissLabel = cancelText && cancelText.trim().length > 0 ? cancelText.trim() : 'Cancel';
    const theme = getPickerTheme(styleName);

    return new Promise(resolve => {
        const overlay = document.createElement('div');
        overlay.id = overlayId;
        Object.assign(overlay.style, {
            position: 'fixed',
            inset: '0',
            background: theme.overlayBackground,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            padding: '20px',
            zIndex: '2147483647',
            boxSizing: 'border-box'
        });

        const card = document.createElement('div');
        Object.assign(card.style, {
            width: 'min(420px, 100%)',
            maxHeight: 'min(80vh, 560px)',
            overflow: 'auto',
            background: theme.cardBackground,
            color: theme.cardColor,
            borderRadius: theme.cardRadius,
            boxShadow: theme.cardShadow,
            border: theme.cardBorder,
            padding: '18px',
            boxSizing: 'border-box'
        });

        const titleElement = document.createElement('div');
        titleElement.textContent = header;
        Object.assign(titleElement.style, {
            font: theme.titleFont,
            marginBottom: '6px'
        });

        const subtitleElement = document.createElement('div');
        subtitleElement.textContent = 'Choose one option';
        Object.assign(subtitleElement.style, {
            font: theme.subtitleFont,
            color: theme.subtitleColor,
            marginBottom: '14px'
        });

        const list = document.createElement('div');
        Object.assign(list.style, {
            display: 'flex',
            flexDirection: 'column',
            gap: '8px'
        });

        const cleanup = result => {
            document.removeEventListener('keydown', onKeyDown, true);
            overlay.remove();
            resolve(result);
        };

        const onKeyDown = event => {
            if (event.key === 'Escape') {
                event.preventDefault();
                cleanup(-1);
            }
        };

        options.forEach((option, index) => {
            const isSelected = index === selectedIndex;
            const label = isSelected ? `${option}  (current)` : option;
            const optionButton = createButton(label, () => cleanup(index), theme, isSelected
                ? {
                    background: theme.selectedBackground,
                    borderColor: theme.selectedBorder,
                    color: theme.selectedColor
                }
                : undefined);
            list.append(optionButton);
        });

        const footer = document.createElement('div');
        Object.assign(footer.style, {
            display: 'flex',
            justifyContent: 'flex-end',
            marginTop: '14px'
        });

        const cancelButton = createButton(dismissLabel, () => cleanup(-1), theme, {
            width: 'auto',
            minWidth: '110px',
            textAlign: 'center',
            background: theme.cancelBackground
        });

        footer.append(cancelButton);
        card.append(titleElement, subtitleElement, list, footer);
        overlay.append(card);

        overlay.addEventListener('click', event => {
            if (event.target === overlay) {
                cleanup(-1);
            }
        });

        document.addEventListener('keydown', onKeyDown, true);
        document.body.append(overlay);
    });
}
