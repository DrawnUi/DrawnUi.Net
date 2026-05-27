using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Rect = Windows.Foundation.Rect;
using Visibility = Microsoft.UI.Xaml.Visibility;
using WinRT.Interop;

namespace DrawnUi.Views
{
    public partial class DrawnView
    {
        // bridgeHwnd is stored so OnCanvasViewChangedPlatform can retry wiring
        // after CanvasView's handler connects (which happens after OnHandlerChanged).
        private nint _bridgeHwnd;

        private void SetupWindowsAccessibility()
        {
            if (Handler?.MauiContext == null) return;
            try
            {
                var window     = Handler.MauiContext.GetPlatformWindow();
                var parentHwnd = (nint)WindowNative.GetWindowHandle(window);
                _bridgeHwnd    = parentHwnd != 0 ? FindDesktopChildSiteBridge(parentHwnd) : 0;
                if (_bridgeHwnd == 0) _bridgeHwnd = parentHwnd;

                // AutomationPeer approach: elements appear as children of the SwapChainPanel
                // peer (the 'custom' node) in the native WinUI3 UIA tree — correct position.
                // CanvasView may not have its handler yet at OnHandlerChanged time, so defer.
                TryWireA11yHost();
            }
            catch { }

        }

        // Called from OnCanvasViewChangedPlatform when CanvasView is assigned/replaced.
        partial void OnCanvasViewChangedPlatform()
        {
            if (_bridgeHwnd == 0) return; // SetupWindowsAccessibility not called yet
            TryWireA11yHost();
        }

        private void TryWireA11yHost()
        {
            var canvasView = CanvasView as View;
            if (canvasView == null)
                return;

            if (canvasView.Handler?.PlatformView is DrawnUi.Draw.IDrawnUiA11yHost host)
            {
                WireA11yHost(host, canvasView.Handler.PlatformView as Microsoft.UI.Xaml.FrameworkElement);
                return;
            }

            // Handler not connected yet — subscribe once and retry
            void OnCvHandlerChanged(object? s, EventArgs e)
            {
                canvasView.HandlerChanged -= OnCvHandlerChanged;
                if (canvasView.Handler?.PlatformView is DrawnUi.Draw.IDrawnUiA11yHost h)
                    WireA11yHost(h, canvasView.Handler.PlatformView as Microsoft.UI.Xaml.FrameworkElement);
            }
            canvasView.HandlerChanged += OnCvHandlerChanged;
        }

        private void WireA11yHost(DrawnUi.Draw.IDrawnUiA11yHost host, Microsoft.UI.Xaml.FrameworkElement? canvasElem)
        {
            Func<(double x, double y)> getOrigin = () =>
            {
                var bo = _bridgeHwnd != 0
                    ? MauiWindowsUiaProvider.GetClientScreenOrigin(_bridgeHwnd)
                    : (x: 0.0, y: 0.0);
                try
                {
                    if (canvasElem?.XamlRoot is { } xr)
                    {
                        var pt = canvasElem.TransformToVisual(null).TransformPoint(new Windows.Foundation.Point(0, 0));
                        return (bo.x + pt.X * xr.RasterizationScale, bo.y + pt.Y * xr.RasterizationScale);
                    }
                }
                catch { }
                return bo;
            };

            _a11yHost = host;
            host.A11yManager   = AccessibilityManager;
            host.A11yGetOrigin = getOrigin;
            host.A11yGetScale  = () => (float)RenderingScale;
            AccessibilityManager.Changed          += OnA11ySnapshotChanged;
            AccessibilityManager.FocusChanged     += OnA11yFocusChanged;
            AccessibilityManager.LiveRegionUpdated += OnA11yLiveRegionUpdated;

            // Eagerly create the automation peer so A11yPeer is set before any AT client
            // traverses the tree. Without this, focus events raised before the first
            // UIA traversal are lost (A11yPeer would be null).
            if (canvasElem != null && host.A11yPeer == null)
            {
                var created = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer
                    .CreatePeerForElement(canvasElem) as DrawnUi.Draw.DrawnUiAutomationPeer;
                if (created != null)
                    host.A11yPeer = created;
            }

            // Subscribe keyboard and focus handlers on the canvas element (SwapChainPanel).
            if (canvasElem != null)
            {
                canvasElem.KeyDown  -= OnCanvasKeyDown;
                canvasElem.KeyDown  += OnCanvasKeyDown;
                canvasElem.GotFocus -= OnCanvasGotFocus;
                canvasElem.GotFocus += OnCanvasGotFocus;
                canvasElem.LostFocus -= OnCanvasLostFocus;
                canvasElem.LostFocus += OnCanvasLostFocus;
            }

            // DrawnView extends ContentView — MAUI wraps it in a ContentPanel which may
            // itself receive Tab focus. Subscribe the same handlers to the outer wrapper
            // so keyboard/focus events reach us regardless of which element Tab lands on.
            _outerElem = Handler?.PlatformView as Microsoft.UI.Xaml.FrameworkElement;
            if (_outerElem != null && !ReferenceEquals(_outerElem, canvasElem))
            {
                _outerElem.KeyDown   -= OnCanvasKeyDown;
                _outerElem.KeyDown   += OnCanvasKeyDown;
                _outerElem.GotFocus  -= OnOuterGotFocus;
                _outerElem.GotFocus  += OnOuterGotFocus;
                _outerElem.LostFocus -= OnOuterLostFocus;
                _outerElem.LostFocus += OnOuterLostFocus;
            }

            // Snapshot may already have elements — force a rebuild on next frame
            // so GetChildrenCore gets the full tree after the UIA peer is created.
            AccessibilityManager.ForceRebuildOnNextFrame();
        }

        // Set when the outer wrapper redirects Tab focus to the canvas — lets OnCanvasGotFocus
        // know it should auto-focus the first virtual child even though FocusState is Programmatic.
        private bool _focusingCanvasFromTab;

        // When Tab lands on the outer MAUI wrapper (ContentPanel) instead of the canvas,
        // redirect XAML focus to the canvas so our GotFocus/KeyDown handlers fire.
        private void OnOuterGotFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var canvasElem = GetCanvasPlatformElement();
            if (canvasElem == null) return;
            // Native input sinks (e.g. SkiaEditor's hidden TextBox) live as children of the
            // ContentPanel and legitimately own WinUI focus while the user types.
            // Do NOT redirect focus away from them — that would kill keyboard input.
            if (e.OriginalSource is Microsoft.UI.Xaml.Controls.TextBox)
            {
                _nativeChildHasFocus = true;
                return;
            }
            // If the original focus target is NOT the canvas, redirect.
            // If it IS the canvas, this event just bubbled up — already handled.
            if (!ReferenceEquals(e.OriginalSource, canvasElem))
            {
                _focusingCanvasFromTab = true;
                canvasElem.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }
        }

        // Called when a native input child (TextBox) loses WinUI focus.
        // If focus went somewhere OUTSIDE _outerElem, clear virtual peer state —
        // OnCanvasLostFocus already fired when the canvas lost focus to the TextBox, so
        // it won't fire again when the TextBox loses focus to an external element.
        private void OnOuterLostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (e.OriginalSource is not Microsoft.UI.Xaml.Controls.TextBox)
                return;

            _nativeChildHasFocus = false;

            // Check if focus stayed within _outerElem's subtree (e.g. back to canvas).
            // If so, OnCanvasGotFocus or another handler will manage UIA state.
            var fe     = sender as Microsoft.UI.Xaml.FrameworkElement;
            var newFocus = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(fe?.XamlRoot)
                           as Microsoft.UI.Xaml.DependencyObject;
            if (newFocus != null && IsDescendantOf(newFocus, _outerElem))
                return;

            // Focus left the DrawnUI area entirely — clean up virtual peer.
            var peer = _a11yHost?.A11yPeer as DrawnUi.Draw.DrawnUiAutomationPeer;
            if (peer == null) return;
            peer.FocusedPeer?.Source?.OnAccessibilityFocused(false);
            peer.ClearVirtualFocus();
        }

        private static bool IsDescendantOf(Microsoft.UI.Xaml.DependencyObject? child,
                                            Microsoft.UI.Xaml.DependencyObject? ancestor)
        {
            if (ancestor == null) return false;
            var current = child;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor)) return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private void OnCanvasKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // Keys from native input children (SkiaEditor's hidden TextBox) bubble up here.
            // Don't intercept them — let the TextBox process its own input.
            if (e.OriginalSource is Microsoft.UI.Xaml.Controls.TextBox)
                return;

            var peer = _a11yHost?.A11yPeer as DrawnUi.Draw.DrawnUiAutomationPeer;
            System.Diagnostics.Debug.WriteLine($"[A11y-KEY] OnCanvasKeyDown key={e.Key} sender={sender?.GetType().Name} peer={(peer == null ? "NULL" : "ok")} focusedPeer={(peer?.FocusedPeer == null ? "NULL" : peer.FocusedPeer.Role)}");
            if (peer == null) return;

            var key = e.Key;
            if (key == Windows.System.VirtualKey.Tab)
            {
                bool shift = Microsoft.UI.Input.InputKeyboardSource
                    .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                    .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

                bool moved = peer.MoveFocusToNext(!shift);
                System.Diagnostics.Debug.WriteLine($"[A11y-KEY] Tab moved={moved} newFocusedPeer={(peer.FocusedPeer == null ? "NULL" : peer.FocusedPeer.Role)}");
                if (moved)
                {
                    e.Handled = true; // consume Tab; don't let XAML move focus away
                }
                // if not moved (past end/beginning) let XAML Tab continue to next element
            }
            else if (key == Windows.System.VirtualKey.Enter || key == Windows.System.VirtualKey.Space)
            {
                System.Diagnostics.Debug.WriteLine($"[A11y-KEY] Enter/Space — activating focusedPeer={(peer.FocusedPeer == null ? "NULL" : peer.FocusedPeer.Role)}");
                peer.ActivateFocused();
                e.Handled = true;
            }
        }

        // True while canvas holds XAML keyboard focus (between GotFocus / LostFocus).
        private bool _canvasHasXamlFocus;

        // True while a native child of _outerElem (e.g. SkiaEditor's hidden TextBox) holds
        // XAML focus. Distinct from _canvasHasXamlFocus so callers can test either/both.
        private bool _nativeChildHasFocus;

        /// <summary>
        /// True when this DrawnView's canvas OR any native child (e.g. a hidden input sink)
        /// currently holds WinUI XAML keyboard focus. Use this in gesture handlers to decide
        /// whether to call canvasElem.Focus() — skip the call when a child already owns it.
        /// </summary>
        internal bool CanvasOrChildHasXamlFocus => _canvasHasXamlFocus || _nativeChildHasFocus;

        private void OnCanvasGotFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _canvasHasXamlFocus = true;
            _nativeChildHasFocus = false;
            var peer = _a11yHost?.A11yPeer as DrawnUi.Draw.DrawnUiAutomationPeer;
            System.Diagnostics.Debug.WriteLine($"[A11y-FOCUS] OnCanvasGotFocus peer={(peer == null ? "NULL" : "ok")} FocusedPeer={(peer?.FocusedPeer == null ? "NULL" : peer.FocusedPeer.Role)}");
            if (peer == null) return;

            // Auto-focus first child only for keyboard (Tab directly) or when we redirected
            // Tab from the outer ContentPanel. For pointer clicks, virtual focus is set by
            // NotifyAccessibilityFocused from the element that was clicked.
            var fe = sender as Microsoft.UI.Xaml.FrameworkElement;
            bool isTabEntry = fe?.FocusState == Microsoft.UI.Xaml.FocusState.Keyboard || _focusingCanvasFromTab;
            _focusingCanvasFromTab = false;

            if (isTabEntry && peer.FocusedPeer == null)
                peer.MoveFocusToNext(forward: true);
            else if (peer.FocusedPeer != null)
                peer.FocusedPeer.RaiseAutomationEvent(
                    Microsoft.UI.Xaml.Automation.Peers.AutomationEvents.AutomationFocusChanged);
        }

        private void OnCanvasLostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _canvasHasXamlFocus = false;
            var peer = _a11yHost?.A11yPeer as DrawnUi.Draw.DrawnUiAutomationPeer;
            if (peer == null) return;

            // FocusManager already reflects the new focus target synchronously during LostFocus,
            // before GotFocus fires on the new element. Use IsDescendantOf to match exactly our
            // native child controls (e.g. SkiaEditor's hidden TextBox) without false-positives
            // from TextBox controls elsewhere on the page.
            var fe = sender as Microsoft.UI.Xaml.FrameworkElement;
            var newFocus = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(fe?.XamlRoot)
                           as Microsoft.UI.Xaml.DependencyObject;
            if (newFocus != null && IsDescendantOf(newFocus, _outerElem))
                return;

            // Focus truly left the DrawnUI area — deactivate any active input control.
            peer.FocusedPeer?.Source?.OnAccessibilityFocused(false);
            peer.ClearVirtualFocus();
        }

        // Called by SkiaEditor when Tab is pressed inside the hidden TextBox.
        // Deactivates the editor, returns WinUI focus to the canvas, and advances virtual UIA focus.
        internal bool HandleEditorA11yTabOut(bool forward)
        {
            var peer = _a11yHost?.A11yPeer as DrawnUi.Draw.DrawnUiAutomationPeer;
            var canvasElem = GetCanvasPlatformElement();

            // Return WinUI focus to canvas so MoveFocusToNext UIA events fire on the right element.
            if (canvasElem != null)
            {
                _focusingCanvasFromTab = true;
                canvasElem.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }

            if (peer == null) return false;

            bool moved = peer.MoveFocusToNext(forward);
            if (!moved)
                peer.ClearVirtualFocus();

            return moved;
        }


        private DrawnUi.Draw.IDrawnUiA11yHost? _a11yHost;
        private Microsoft.UI.Xaml.FrameworkElement? _outerElem;

        private MauiWindowsUiaProvider? _uiaProvider;

        private Microsoft.UI.Xaml.FrameworkElement? GetCanvasPlatformElement()
            => (CanvasView as View)?.Handler?.PlatformView as Microsoft.UI.Xaml.FrameworkElement;

        private void OnA11yFocusChanged(DrawnUi.Draw.ISkiaAccessibilityNode? focused)
        {
            var host = _a11yHost;
            if (host == null) return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var canvasElem = GetCanvasPlatformElement();

                // Request XAML focus only when canvas doesn't already have it.
                // If canvas already has focus, Focus(Programmatic) fires GotFocus which
                // would override FocusedPeer to the first child, conflicting with the
                // button-tap path that wants to focus the tapped element.
                // Skip when a native child (e.g. SkiaEditor's hidden TextBox) holds focus —
                // stealing it would kill keyboard input.
                if (!_canvasHasXamlFocus && !_nativeChildHasFocus)
                    canvasElem?.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);

                var peer = (host.A11yPeer as DrawnUi.Draw.DrawnUiAutomationPeer)
                    ?? Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(
                           canvasElem) as DrawnUi.Draw.DrawnUiAutomationPeer;

                // Guard: a null focused notification arrives async from the ReportFocus chain
                // when an editor's FocusedChild changes during Tab navigation. If MoveFocusToNext
                // already set a new FocusedPeer, the null would wrongly clear it — skip.
                if (focused == null && peer?.FocusedPeer != null)
                    return;

                peer?.NotifyFocusChanged(focused);
            });
        }

        private void OnA11ySnapshotChanged()
        {
            var host = _a11yHost;
            if (host == null) return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var peer = (host.A11yPeer as DrawnUi.Draw.DrawnUiAutomationPeer)
                    ?? Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(
                           GetCanvasPlatformElement()) as DrawnUi.Draw.DrawnUiAutomationPeer;
                peer?.NotifyStructureChanged();
            });
        }

        private static nint FindDesktopChildSiteBridge(nint parentHwnd)
        {
            nint found = 0;
            EnumChildWindows(parentHwnd, (hwnd, _) =>
            {
                var sb = new System.Text.StringBuilder(256);
                GetClassName(hwnd, sb, sb.Capacity);
                if (sb.ToString() == "Microsoft.UI.Content.DesktopChildSiteBridge")
                {
                    found = hwnd;
                    return false;
                }
                return true;
            }, 0);
            return found;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool EnumChildWindows(nint hWndParent, EnumChildProc lpEnumFunc, nint lParam);
        private delegate bool EnumChildProc(nint hwnd, nint lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int GetClassName(nint hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        private void OnA11yLiveRegionUpdated(DrawnUi.Draw.ISkiaAccessibilityNode node)
        {
            var host = _a11yHost;
            if (host == null) return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var peer = (host.A11yPeer as DrawnUi.Draw.DrawnUiAutomationPeer);
                if (peer == null) return;
                // Ensure children are populated
                if (peer.CachedChildren.Count == 0)
                    peer.EnsureChildrenCached();
                var virtualPeer = peer.CachedChildren.FirstOrDefault(p => ReferenceEquals(p.Source, node));
                virtualPeer?.RaiseLiveRegionChanged();
            });
        }

        private void TeardownWindowsAccessibility()
        {
            AccessibilityManager.Changed          -= OnA11ySnapshotChanged;
            AccessibilityManager.FocusChanged     -= OnA11yFocusChanged;
            AccessibilityManager.LiveRegionUpdated -= OnA11yLiveRegionUpdated;
            var canvasElem = GetCanvasPlatformElement();
            if (canvasElem != null)
            {
                canvasElem.KeyDown   -= OnCanvasKeyDown;
                canvasElem.GotFocus  -= OnCanvasGotFocus;
                canvasElem.LostFocus -= OnCanvasLostFocus;
            }
            if (_outerElem != null)
            {
                _outerElem.KeyDown   -= OnCanvasKeyDown;
                _outerElem.GotFocus  -= OnOuterGotFocus;
                _outerElem.LostFocus -= OnOuterLostFocus;
                _outerElem = null;
            }
            _uiaProvider?.Dispose();
            _uiaProvider = null;
        }

        private int _frameSkipCounter = 0;
        private DateTime _viewportChangedTime;
        private readonly TimeSpan _visibilityCheckDelay = TimeSpan.FromSeconds(0.1);
        private bool _wasVisible = true;

        /// <summary>
        /// Check if element is visible within all parent bounds
        /// </summary>
        public void CheckElementVisibility(VisualElement element)
        {
            NeedCheckParentVisibility = false;

            if (Handler?.PlatformView is not FrameworkElement platformElement)
            {
                IsHiddenInViewTree = true;
                return;
            }

            IsHiddenInViewTree = !IsElementVisibleInParentChain(platformElement);
        }

        /// <summary>
        /// Check if element is visible through entire parent chain
        /// </summary>
        private bool IsElementVisibleInParentChain(FrameworkElement element)
        {
            // Quick checks first
            if (element.Visibility == Visibility.Collapsed ||
                element.ActualWidth <= 0 ||
                element.ActualHeight <= 0)
            {
                return false;
            }

            // Start with element bounds
            var elementBounds = new Rect(0, 0, element.ActualWidth, element.ActualHeight);

            DependencyObject current = element;
            DependencyObject parent = VisualTreeHelper.GetParent(current);

            // Walk up parent chain
            while (parent != null)
            {
                if (parent is FrameworkElement parentElement)
                {
                    // Check parent visibility
                    if (parentElement.Visibility == Visibility.Collapsed ||
                        parentElement.ActualWidth <= 0 ||
                        parentElement.ActualHeight <= 0)
                    {
                        return false;
                    }

                    try
                    {
                        // Transform to parent space
                        if (current is UIElement currentUI)
                        {
                            var transform = currentUI.TransformToVisual(parentElement);
                            elementBounds = transform.TransformBounds(elementBounds);
                        }

                        // Check if within parent bounds
                        var parentBounds = new Rect(0, 0, parentElement.ActualWidth, parentElement.ActualHeight);

                        if (!AreRectanglesIntersecting(elementBounds, parentBounds))
                        {
                            return false;
                        }

                        // Special handling for ScrollViewer
                        if (parent is Microsoft.UI.Xaml.Controls.ScrollViewer scrollViewer)
                        {
                            var viewportBounds = new Rect(0, 0, scrollViewer.ViewportWidth, scrollViewer.ViewportHeight);
                            if (!AreRectanglesIntersecting(elementBounds, viewportBounds))
                            {
                                return false;
                            }
                        }
                    }
                    catch
                    {
                        // Elements not in same visual tree
                        return false;
                    }
                }

                current = parent;
                parent = VisualTreeHelper.GetParent(current);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool AreRectanglesIntersecting(Rect rect1, Rect rect2)
        {
            return rect1.Left < rect2.Right &&
                   rect1.Right > rect2.Left &&
                   rect1.Top < rect2.Bottom &&
                   rect1.Bottom > rect2.Top;
        }

        protected virtual void InitFrameworkPlatform(bool subscribe)
        {
            if (subscribe)
            {
                if (Handler?.PlatformView is FrameworkElement element)
                {
                    element.EffectiveViewportChanged += ElementOnEffectiveViewportChanged;
                    element.LayoutUpdated += ElementOnLayoutUpdated;
                }
                CompositionTarget.Rendering += OnRendering;
                SetupWindowsAccessibility();
            }
            else
            {
                TeardownWindowsAccessibility();
                CompositionTarget.Rendering -= OnRendering;
                if (Handler?.PlatformView is FrameworkElement element)
                {
                    element.EffectiveViewportChanged -= ElementOnEffectiveViewportChanged;
                    element.LayoutUpdated -= ElementOnLayoutUpdated;
                }
            }
        }

        private bool _checkVisibility;

        private void ElementOnEffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            _checkVisibility = true;
            _viewportChangedTime = DateTime.UtcNow;
            //Debug.WriteLine($"[DrawnView] CHANGED {Tag}");
        }

        private void ElementOnLayoutUpdated(object sender, object e)
        {
            // Similar to Android's OnGlobalLayout - fires when visual tree layout changes
            if (Handler?.PlatformView != null)
            {
                NeedCheckParentVisibility = true;
            }
        }

        private void OnRendering(object sender, object e)
        {
            if (!_checkVisibility)
                return;

            var delay = DateTime.UtcNow - _viewportChangedTime;
            if (delay < _visibilityCheckDelay)
                return;

            if (Handler?.PlatformView is FrameworkElement element)
            {
                _checkVisibility = false;

                var hide = ! IsElementVisibleInParentChain(element);
                if (hide != IsHiddenInViewTree)
                {
                    IsHiddenInViewTree = hide;
                }
            }
        }


        protected virtual void OnSizeChanged()
        {
            if (Handler?.PlatformView is ContentPanel layout)
            {
                layout.Clip = new RectangleGeometry
                {
                    Rect = new Rect(0, 0, Width, Height)
                };
            }
            Update();
        }

        public virtual void SetupRenderingLoop()
        {
#if !LEGACY
            Super.OnFrame -= OnFrame;
            Super.OnFrame += OnFrame;
#endif
        }

        protected virtual void PlatformHardwareAccelerationChanged()
        {
        }

#if LEGACY
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckCanDraw()
        {
            if (UpdateLocked && StopDrawingWhenUpdateIsLocked)
                return false;

            return CanvasView != null
                   && !IsRendering
                   && IsDirty
                   && IsVisible
                   && !IsHiddenInViewTree; // Added check
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void UpdatePlatform()
        {
            IsDirty = true;
            if (!OrderedDraw && CheckCanDraw())
            {
                OrderedDraw = true;
                InvalidateCanvas();
            }
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void UpdatePlatform()
        {
            IsDirty = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckCanDraw()
        {
            return CanvasView != null
                   && this.Handler != null
                   && this.Handler.PlatformView != null
                   //&& !CanvasView.IsDrawing
                   && IsDirty
                   && !(UpdateLocks > 0 && StopDrawingWhenUpdateIsLocked)
                   && IsVisible
                   && Super.EnableRendering;
        }
#endif

        protected virtual void DisposePlatform()
        {
            InitFrameworkPlatform(false); // Unsubscribe from rendering
            Super.OnFrame -= OnFrame;
        }
    }
}
