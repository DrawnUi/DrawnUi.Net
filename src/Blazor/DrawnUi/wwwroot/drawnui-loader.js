(function () {
    const defaultLoaderOptions = {
        initialProgress: 8,
        initialLabel: 'Loading',
        initializingLabel: 'Initializing...',
        failureLabel: 'Failed'
    };

    function setLoaderProgress(state, progress, label) {
        const clampedProgress = Math.max(0, Math.min(100, progress));
        const progressText = label || `${Math.floor(clampedProgress)}%`;

        state.lastProgress = clampedProgress;
        globalThis.document.documentElement.style.setProperty('--drawnui-load-percentage', `${clampedProgress}%`);
        globalThis.document.documentElement.style.setProperty('--drawnui-load-percentage-text', `"${progressText}"`);
    }

    function resolvePreview(state) {
        const searchParams = new URLSearchParams(globalThis.location.search);
        if (!searchParams.has('loader-preview')) {
            return false;
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

        if (resolvePreview(state)) {
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
    globalThis.DrawnUiLoader.startBlazorWithLoader = startBlazorWithLoader;
})();