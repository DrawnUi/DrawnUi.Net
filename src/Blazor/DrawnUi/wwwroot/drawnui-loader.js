(function () {
    const loaderThemeClasses = ['boot-shell-light', 'boot-shell-dark'];
    const defaultLoaderOptions = {
        initialProgress: 8,
        initialLabel: 'Loading',
        initializingLabel: 'Initializing...',
        failureLabel: 'Failed',
        theme: 'dark',
        shellSelector: '.boot-shell',
        logoSelector: '.boot-logo',
        lightLogoSrc: null,
        darkLogoSrc: null
    };

    function getLoaderShell(selector) {
        return globalThis.document.querySelector(selector || '.boot-shell');
    }

    function normalizeTheme(theme) {
        const resolved = String(theme || 'dark').toLowerCase();
        if (resolved === 'light' || resolved === 'dark') {
            return resolved;
        }

        if (resolved === 'auto') {
            return globalThis.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
        }

        return 'dark';
    }

    function applyLoaderTheme(loaderOptions) {
        const resolvedLoaderOptions = Object.assign({}, defaultLoaderOptions, loaderOptions);
        const shell = getLoaderShell(resolvedLoaderOptions.shellSelector);
        if (!shell) {
            return null;
        }

        const theme = normalizeTheme(resolvedLoaderOptions.theme || shell.dataset.loaderTheme);
        shell.classList.remove(...loaderThemeClasses);
        shell.classList.add(theme === 'light' ? 'boot-shell-light' : 'boot-shell-dark');
        shell.dataset.loaderThemeApplied = theme;

        const logo = shell.querySelector(resolvedLoaderOptions.logoSelector);
        if (logo) {
            const lightLogoSrc = resolvedLoaderOptions.lightLogoSrc || logo.getAttribute('data-logo-light') || shell.dataset.loaderLogoLight;
            const darkLogoSrc = resolvedLoaderOptions.darkLogoSrc || logo.getAttribute('data-logo-dark') || shell.dataset.loaderLogoDark;
            const nextLogoSrc = theme === 'light' ? lightLogoSrc : darkLogoSrc;
            if (nextLogoSrc) {
                logo.setAttribute('src', nextLogoSrc);
            }
        }

        return theme;
    }

    function setLoaderProgress(state, progress, label) {
        const clampedProgress = Math.max(0, Math.min(100, progress));
        const progressText = label || `${Math.floor(clampedProgress)}%`;

        state.lastProgress = clampedProgress;
        globalThis.document.documentElement.style.setProperty('--drawnui-load-percentage', `${clampedProgress}%`);
        globalThis.document.documentElement.style.setProperty('--drawnui-load-percentage-text', `"${progressText}"`);
    }

    function updateLoaderWidth(shell) {
        const resolvedShell = shell || getLoaderShell();
        const title = resolvedShell?.querySelector('.boot-copy');
        if (!title || !title.firstChild) {
            return;
        }

        const range = globalThis.document.createRange();
        range.selectNodeContents(title);

        const titleWidth = Array.from(range.getClientRects()).reduce(function (maxWidth, rect) {
            return Math.max(maxWidth, rect.width || 0);
        }, 0);

        if (titleWidth > 0) {
            globalThis.document.documentElement.style.setProperty('--boot-progress-width', `${titleWidth * 0.85}px`);
        }
    }

    function resolvePreview(state, loaderOptions) {
        const searchParams = new URLSearchParams(globalThis.location.search);
        if (!searchParams.has('loader-preview')) {
            return false;
        }

        const previewTheme = searchParams.get('theme');
        if (previewTheme) {
            applyLoaderTheme(Object.assign({}, loaderOptions, { theme: previewTheme }));
        }

        const loaderProgressRaw = Number(searchParams.get('progress'));
        const loaderProgress = Number.isFinite(loaderProgressRaw)
            ? Math.max(0, Math.min(100, loaderProgressRaw))
            : state.lastProgress;
        const loaderLabel = searchParams.get('label');

        setLoaderProgress(state, loaderProgress, loaderLabel || `${Math.floor(loaderProgress)}%`);
        globalThis.document.body.classList.add('loader-preview');
        globalThis.document.getElementById('blazor-error-ui')?.style.setProperty('display', 'none');
        return true;
    }

    function startBlazorWithLoader(startOptions, loaderOptions) {
        const resolvedStartOptions = startOptions || {};
        const resolvedLoaderOptions = Object.assign({}, defaultLoaderOptions, loaderOptions);
        const state = {
            lastProgress: resolvedLoaderOptions.initialProgress,
            downloadsCompleted: false
        };

        const appliedTheme = applyLoaderTheme(resolvedLoaderOptions);
        const shell = getLoaderShell(resolvedLoaderOptions.shellSelector);
        updateLoaderWidth(shell);

        globalThis.requestAnimationFrame(function () {
            updateLoaderWidth(shell);
        });

        globalThis.document.fonts?.ready?.then(function () {
            updateLoaderWidth(shell);
        });

        if (shell && !shell.dataset.loaderWidthBound) {
            const updateWidth = function () {
                updateLoaderWidth(shell);
            };
            const title = shell.querySelector('.boot-copy');

            globalThis.addEventListener('resize', updateWidth);

            if (title && globalThis.ResizeObserver) {
                const resizeObserver = new globalThis.ResizeObserver(updateWidth);
                resizeObserver.observe(title);
            }

            shell.dataset.loaderWidthBound = 'true';
        }

        if (resolvePreview(state, resolvedLoaderOptions)) {
            return Promise.resolve();
        }

        setLoaderProgress(state, resolvedLoaderOptions.initialProgress, resolvedLoaderOptions.initialLabel);

        const originalProgressHandler = resolvedStartOptions.onDownloadResourceProgress;
        const mergedStartOptions = Object.assign({}, resolvedStartOptions, {
            onDownloadResourceProgress: function (resourcesLoaded, totalResources) {
                if (typeof originalProgressHandler === 'function') {
                    originalProgressHandler(resourcesLoaded, totalResources);
                }

                if (!Number.isFinite(totalResources) || totalResources <= 0) {
                    return;
                }

                const clampedRatio = Math.max(0, Math.min(1, resourcesLoaded / totalResources));
                const isDownloadComplete = clampedRatio >= 1;
                const resolvedProgress = clampedRatio * 100;

                state.downloadsCompleted = isDownloadComplete;
                setLoaderProgress(
                    state,
                    resolvedProgress,
                    isDownloadComplete ? resolvedLoaderOptions.initializingLabel : undefined);
            }
        });

        return Blazor.start(mergedStartOptions)
            .then(function (result) {
                setLoaderProgress(state, 100, resolvedLoaderOptions.initializingLabel);
                return result;
            })
            .catch(function (error) {
                setLoaderProgress(
                    state,
                    state.downloadsCompleted ? 100 : state.lastProgress,
                    resolvedLoaderOptions.failureLabel);
                throw error;
            });
    }

    globalThis.DrawnUiLoader = globalThis.DrawnUiLoader || {};
    globalThis.DrawnUiLoader.applyTheme = applyLoaderTheme;
    globalThis.DrawnUiLoader.startBlazorWithLoader = startBlazorWithLoader;
    globalThis.DrawnUiBrowser = globalThis.DrawnUiBrowser || {};
    globalThis.DrawnUiBrowser.isMobileBrowser = function () {
        const navigatorRef = globalThis.navigator;
        if (!navigatorRef) {
            return false;
        }

        if (typeof navigatorRef.userAgentData?.mobile === 'boolean') {
            return navigatorRef.userAgentData.mobile;
        }

        const userAgent = navigatorRef.userAgent || navigatorRef.vendor || '';
        if (/android|webos|iphone|ipad|ipod|blackberry|iemobile|opera mini|mobile/i.test(userAgent)) {
            return true;
        }

        const coarsePointer = globalThis.matchMedia?.('(pointer: coarse)')?.matches === true;
        const smallViewport = globalThis.matchMedia?.('(max-width: 900px)')?.matches === true;
        const hasTouch = (navigatorRef.maxTouchPoints || 0) > 1;

        return coarsePointer && smallViewport && hasTouch;
    };
})();