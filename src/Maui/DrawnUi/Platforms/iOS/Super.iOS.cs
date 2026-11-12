using System.Diagnostics;
using System.Runtime.InteropServices;
using CoreAnimation;
using Foundation;
using Microsoft.Maui.Controls.Compatibility.Platform.iOS;
 
using SkiaSharp.Views.iOS;
 
using UIKit;
using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace DrawnUi.Draw
{

    public partial class Super
    {

        public static void Init()
        {
            if (Initialized)
                return;

            Initialized = true;

            RefreshRate = GetDisplayRefreshRate(60);

            //a cap set before the display rate was known could not be snapped yet
            MaxFps = SnapMaxFpsToDisplay(MaxFps);

            Super.Screen.Density = UIScreen.MainScreen.Scale;
            Super.Screen.WidthDip = UIScreen.MainScreen.Bounds.Width;
            Super.Screen.HeightDip = UIScreen.MainScreen.Bounds.Height;

            if (UIDevice.CurrentDevice.CheckSystemVersion(11, 0))
            {
                var window = new UIWindow(frame: UIScreen.MainScreen.Bounds)
                { BackgroundColor = Colors.Transparent.ToUIColor() };

                Super.Screen.TopInset = (int)(window.SafeAreaInsets.Top);
                Super.Screen.BottomInset = (int)(window.SafeAreaInsets.Bottom);
                Super.Screen.LeftInset = (int)(window.SafeAreaInsets.Left);
                Super.Screen.RightInset = (int)(window.SafeAreaInsets.Right);
            }

            Super.StatusBarHeight = Super.Screen.TopInset;
            if (Super.StatusBarHeight <= 0)
                Super.StatusBarHeight = 20;

            if (Super.NavBarHeight < 0)

                Super.NavBarHeight = 47; //manual

            InitShared();

            InsetsChanged?.Invoke(null, null);

            // the safe area changes with a rotation, a status bar shown or hidden, a split view: the key window reports it
            UIWindow.Notifications.ObserveDidBecomeKey((sender, args) => WatchSafeArea(args.Notification.Object as UIWindow));
            // a window already key: MAUI makes its window key before the app's FinishedLaunching handlers run
            foreach (var window in AppWindows())
                WatchSafeArea(window);

            if (UseDisplaySync)
            {
                Tasks.StartDelayed(TimeSpan.FromMilliseconds(250), async () =>
                {
                    while (!_loopStarted)
                    {
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            if (_loopStarting)
                                return;
                            _loopStarting = true;

                            if (MainThread.IsMainThread) //CADisplayLink is available
                            {
                                if (!_loopStarted)
                                {
                                    _loopStarted = true;
                                    try
                                    {
                                        _displayLink = CADisplayLink.Create(() =>
                                        {
                                            if (MaxFps > 0)
                                            {
                                                // Fallback guard for when the OS ignores PreferredFramesPerSecond
                                                // (it may deliver faster). The primary throttle is the native one
                                                // below — it hands each capped tick the FULL divided frame budget
                                                // (e.g. 33ms at 30fps on 60Hz). Skipping callbacks of a full-rate
                                                // link instead leaves only one native vsync (16.7ms) of budget, so
                                                // any tick running slightly long slips a vsync and the frame shows
                                                // for 50ms instead of 33ms — a very visible 3:2 stutter.
                                                var skipFrames = Math.Ceiling((double)RefreshRate / MaxFps);
                                                var minInterval = skipFrames / RefreshRate - 0.001;
                                                if (_displayLink.Timestamp - _lastDisplayTimestamp < minInterval)
                                                    return;
                                                _lastDisplayTimestamp = _displayLink.Timestamp;
                                            }
                                            // Vsync-aligned animation clock: TargetTimestamp is when the
                                            // frame produced by this tick will actually be displayed.
                                            VsyncFrameTimeNanos = (long)(_displayLink.TargetTimestamp * 1_000_000_000.0);
                                            OnFrame?.Invoke(null, null);
                                        });
                                        ApplyDisplayLinkFps(MaxFps);
                                        _displayLink.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Default);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(e);
                                    }
                                }
                            }

                            _loopStarting = false;
                        });
                        await Task.Delay(100);
                    }
                });
            }
            else
            {
                Looper = new(() =>
                {
                    OnFrame?.Invoke(null, null);
                });

                Looper.StartOnMainThread(120);
            }

            _thermalService = new ThermalStateService();
            OnThermalStateChanged(_thermalService.CurrentState);

            _thermalService.StateChanged += OnThermalStateChanged;
        }

        /// <summary>
        /// Puts a <see cref="SafeAreaWatcher"/> into the window once and reads its safe area now.
        /// </summary>
        /// <summary>The app's windows: those of its connected scenes, or the key window of an app without scenes.</summary>
        static IEnumerable<UIWindow> AppWindows()
        {
            var found = false;
            foreach (var scene in UIApplication.SharedApplication.ConnectedScenes)
            {
                if (scene is UIWindowScene windowScene)
                {
                    foreach (var window in windowScene.Windows)
                    {
                        found = true;
                        yield return window;
                    }
                }
            }

            if (!found)
            {
#pragma warning disable CA1422 // KeyWindow: the only handle on the window of an app without scenes
                var key = UIApplication.SharedApplication.KeyWindow;
#pragma warning restore CA1422
                if (key != null)
                    yield return key;
            }
        }

        static void WatchSafeArea(UIWindow window)
        {
            if (window == null)
                return;

            if (!window.Subviews.Any(x => x is SafeAreaWatcher))
                window.InsertSubview(new SafeAreaWatcher(window.Bounds), 0);

            ReadSafeArea(window);
        }

        /// <summary>
        /// The window's safe area (status bar, notch, home indicator, rounded corners) into the Super.Screen insets, in
        /// points; raises <see cref="InsetsChanged"/> when one of them changed.
        /// </summary>
        static void ReadSafeArea(UIWindow window)
        {
            var insets = window.SafeAreaInsets;
            var changed = SetInset(Super.Screen.TopInset, (int)insets.Top, value => Super.Screen.TopInset = value)
                          | SetInset(Super.Screen.BottomInset, (int)insets.Bottom, value => Super.Screen.BottomInset = value)
                          | SetInset(Super.Screen.LeftInset, (int)insets.Left, value => Super.Screen.LeftInset = value)
                          | SetInset(Super.Screen.RightInset, (int)insets.Right, value => Super.Screen.RightInset = value);
            if (!changed)
                return;

            Super.StatusBarHeight = Super.Screen.TopInset > 0 ? Super.Screen.TopInset : 20;
            InsetsChanged?.Invoke(null, null);
        }

        static bool SetInset(double current, double value, Action<double> set)
        {
            if (Math.Abs(current - value) < 0.01)
                return false;
            set(value);
            return true;
        }

        /// <summary>
        /// An invisible view covering the window, behind its content: UIKit tells it when the window's safe area changes.
        /// </summary>
        sealed class SafeAreaWatcher : UIView
        {
            public SafeAreaWatcher(CoreGraphics.CGRect frame) : base(frame)
            {
                UserInteractionEnabled = false;
                BackgroundColor = UIColor.Clear;
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            }

            public override void SafeAreaInsetsDidChange()
            {
                base.SafeAreaInsetsDidChange();

                if (Window != null)
                    ReadSafeArea(Window);
            }
        }

        private static ThermalStateService _thermalService;

        static Looper Looper { get; set; }

        /// <summary>
        /// When set to true will run loop upon CADisplayLink hits instead of a timer looper. Default is true, change at your own risk.
        /// </summary>
        public static bool UseDisplaySync = true;

        static bool _loopStarting = false;
        static bool _loopStarted = false;

        /// <summary>
        /// Runs the shared display link natively at the capped rate. The OS aligns
        /// callbacks to vsync AND gives each tick the full divided frame budget
        /// (33ms at 30fps on a 60Hz panel) instead of the single native vsync a
        /// callback-skipping scheme would leave. 0 restores the display's native rate.
        /// </summary>
        static void ApplyDisplayLinkFps(int fps)
        {
            var link = _displayLink;
            if (link == null)
                return;

            void Set()
            {
                if (OperatingSystem.IsIOSVersionAtLeast(15))
                {
                    var target = fps > 0 ? fps : RefreshRate;
                    link.PreferredFrameRateRange = new CAFrameRateRange
                    {
                        Minimum = target,
                        Maximum = target,
                        Preferred = target
                    };
                }
                else
                {
                    link.PreferredFramesPerSecond = fps > 0 ? fps : 0;
                }
            }

            if (MainThread.IsMainThread)
                Set();
            else
                MainThread.BeginInvokeOnMainThread(Set);
        }

        static partial void OnMaxFpsChanged(int fps)
        {
            var snapped = SnapMaxFpsToDisplay(fps);
            if (snapped != fps)
            {
                //not a cadence this display can present — re-enters here with one that is
                MaxFps = snapped;
                return;
            }

            // Display link callback reads MaxFps dynamically, no action needed there.
            // Update looper fps if it's being used instead of CADisplayLink.
            Looper?.SetTargetFps(fps > 0 ? fps : RefreshRate);
            ApplyDisplayLinkFps(fps);
            // Capped: MTKViews pace themselves at the cap. Uncapped: they go on-demand and
            // the display link above paces them.
            UpdateRegisteredMetalViewsPacing(fps);
        }


        //static void OnFrame()
        //{
        //    DisplayLinkCallback?.Invoke(null, null);
        //}

        public static event EventHandler OnFrame;

        static CADisplayLink _displayLink;
        static double _lastDisplayTimestamp;

        public static UINavigationController NavigationController { get; set; } = null;

        public static UIStatusBarStyle? OrderedStyle { get; set; }

        public static UIViewController? GetCurrentViewController()
        {
            try
            {
                return Platform.GetCurrentUIViewController();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            return null;
        }

        public static void SetBlackTextStatusBar()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Debug.WriteLine("[StatusBar] BLACK");

                var controller = GetCurrentViewController();

                if (controller == null || controller.NavigationController == null)
                {
                    OrderedStyle = UIStatusBarStyle.DarkContent;

                    UIApplication.SharedApplication.SetStatusBarStyle(UIStatusBarStyle.DarkContent, false);
                    controller?.SetNeedsStatusBarAppearanceUpdate();

                }
                else
                {
                    OrderedStyle = null;
                    controller.NavigationController.NavigationBar.BarStyle = UIBarStyle.Default;
                }
            });
            
        }

        public static void SetWhiteTextStatusBar()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Update the UI
                Debug.WriteLine("[StatusBar] WHITE");

                var controller = GetCurrentViewController();
                if (controller == null || controller.NavigationController == null)
                {
                    OrderedStyle = UIStatusBarStyle.LightContent;

                    UIApplication.SharedApplication.SetStatusBarStyle(UIStatusBarStyle.LightContent, false);
                    controller?.SetNeedsStatusBarAppearanceUpdate();
                }
                else
                {
                    OrderedStyle = null;
                    controller.NavigationController.NavigationBar.BarStyle = UIBarStyle.Black;
                }
            });

        }

        /// <summary>
        /// Completely hides the status bar
        /// </summary>
        public static void HideStatusBar()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Debug.WriteLine("[StatusBar] HIDDEN");
                var controller = GetCurrentViewController();

                UIApplication.SharedApplication.SetStatusBarHidden(true, UIStatusBarAnimation.Fade);
                controller?.SetNeedsStatusBarAppearanceUpdate();
            });
        }

        /// <summary>
        /// Shows the status bar
        /// </summary>
        public static void ShowStatusBar()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Debug.WriteLine("[StatusBar] VISIBLE");
                var controller = GetCurrentViewController();

                UIApplication.SharedApplication.SetStatusBarHidden(false, UIStatusBarAnimation.Fade);
                controller?.SetNeedsStatusBarAppearanceUpdate();
            });
        }

        /// <summary>
        /// Makes status bar text invisible by using black text on black background
        /// </summary>
        public static void MakeStatusBarInvisible()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Debug.WriteLine("[StatusBar] INVISIBLE (black on black)");
                var controller = GetCurrentViewController();

                if (controller == null || controller.NavigationController == null)
                {
                    OrderedStyle = UIStatusBarStyle.DarkContent; // Black text
                    UIApplication.SharedApplication.SetStatusBarStyle(UIStatusBarStyle.DarkContent, false);
                    controller?.SetNeedsStatusBarAppearanceUpdate();
                }
                else
                {
                    OrderedStyle = null;
                    controller.NavigationController.NavigationBar.BarStyle = UIBarStyle.Default;
                    // Set navigation bar background to black to hide black text
                    controller.NavigationController.NavigationBar.BackgroundColor = UIColor.Black;
                    controller.NavigationController.NavigationBar.BarTintColor = UIColor.Black;
                }

                // Ensure your page background is black where status bar area is
                if (controller?.View != null)
                {
                    controller.View.BackgroundColor = UIColor.Black;
                }
            });
        }


    }
}
