global using System.Collections.Specialized;
global using AppoMobi.Gestures;
global using System.ComponentModel;
global using DrawnUi.Draw;
global using System.Numerics;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Windows.Input;
global using AppoMobi.Specials;
global using DrawnUi.Draw;
global using DrawnUi.Extensions;
global using DrawnUi.Infrastructure;
global using DrawnUi.Infrastructure.Models;
global using DrawnUi.Models;
global using DrawnUi.Draw.ApplicationModel;
global using DrawnUi;
global using DrawnUi.Views;
global using SkiaSharp;
global using PointF = System.Drawing.PointF;
//global using SkiaSharp.Views.Maui;
//global using SkiaSharp.Views.Maui.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrawnUi.Draw
{
    public partial class Super
    {
        private static readonly object FrameLoopLock = new();
        private static readonly object NativeAppLifecycleLock = new();
        private static CancellationTokenSource _frameLoopCancellation;
        private static IJSObjectReference? _nativeAppLifecycleModule;
        private static DotNetObjectReference<AppLifecycleProxy>? _lifecycleProxyRef;
        private static bool _loopStarted;
        private static bool _nativeAppCreated;
        private static bool _nativeAppDestroyed;
        private static bool _nativeAppLifecycleAttached;
        private static bool? _nativeAppVisible;

        private static readonly string NativeAppLifecycleModulePath =
            $"./_content/DrawnUi.Blazor.Core/drawnui-app-lifecycle.js?v={System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        public static object App { get; set; }

        public static event EventHandler OnFrame;

        private const int DEFAULT_TARGET_FPS = 120;

        public static int RefreshRate { get; protected set; } = DEFAULT_TARGET_FPS;
        public static void Init()
        {
            Initialized = true;

            Super.Screen.Density = 1;

            RefreshRate = GetDisplayRefreshRate(DEFAULT_TARGET_FPS);
        }

        public static int GetDisplayRefreshRate(int fallback)
        {
            return DEFAULT_TARGET_FPS;
        }

        public static IServiceProvider Services
        {
            get => _services;
            set
            {
                _services = value;
                _servicesFromHandler = value != null;
            }
        }

        public static async Task AttachNativeAppLifecycleAsync(IJSRuntime jsRuntime)
        {
            lock (NativeAppLifecycleLock)
            {
                if (_nativeAppLifecycleAttached)
                {
                    return;
                }

                _nativeAppLifecycleAttached = true;
            }

            try
            {
                _nativeAppLifecycleModule ??= await jsRuntime.InvokeAsync<IJSObjectReference>(
                    "import",
                    NativeAppLifecycleModulePath);

                _lifecycleProxyRef ??= DotNetObjectReference.Create(new AppLifecycleProxy());
                await _nativeAppLifecycleModule.InvokeVoidAsync("attachNativeAppLifecycle", _lifecycleProxyRef);
            }
            catch
            {
                lock (NativeAppLifecycleLock)
                {
                    _nativeAppLifecycleAttached = false;
                }

                throw;
            }
        }

        public static object AppContext => null;

        public static void DisplayException(SkiaControl view, Exception e)
        {
            Log(e?.ToString() ?? string.Empty, LogLevel.Error);

            if (view is SkiaControl skia)
            {
                var scroll = new SkiaScroll()
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = new SkiaLabel
                    {
                        Margin = new Thickness(32),
                        TextColor = Colors.Red,
                        Text = $"{e}"
                    }
                };

                if (skia is ContentLayout content)
                {
                    content.Content = scroll;
                }
                else
                {
                    skia.AddSubView(scroll);
                }
            }
        }

        public static void DisplayException(VisualElement view, Exception e)
        {
            Log(e?.ToString() ?? string.Empty, LogLevel.Error);

            if (view == null)
                return;


            //todo

            view.Update();
        }

        public static void Log(string message, LogLevel logLevel = LogLevel.Warning, [System.Runtime.CompilerServices.CallerMemberName] string caller = null)
        {
            if (DrawnExtensions.StartupSettings != null)
            {
                DrawnExtensions.StartupSettings.Logger?.Log(logLevel, message);
            }

            Console.WriteLine(message);
        }

        public static void Log(LogLevel level, string message, [System.Runtime.CompilerServices.CallerMemberName] string caller = null)
        {
            Log(message, level, caller);
        }

        public static void EnsureFrameLoopStarted()
        {
            lock (FrameLoopLock)
            {
                if (_loopStarted)
                {
                    return;
                }

                _loopStarted = true;
                _frameLoopCancellation = new CancellationTokenSource();
                _ = RunFrameLoopAsync(_frameLoopCancellation.Token);
            }
        }

        private static async Task RunFrameLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var fps = MaxFps > 0 ? MaxFps : RefreshRate;
                    if (fps <= 0)
                    {
                        fps = 60;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1.0 / fps), cancellationToken);
                    OnFrame?.Invoke(null, EventArgs.Empty);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static void RestartFrameLoop()
        {
            lock (FrameLoopLock)
            {
                if (!_loopStarted)
                {
                    return;
                }

                _frameLoopCancellation?.Cancel();
                _frameLoopCancellation?.Dispose();
                _frameLoopCancellation = new CancellationTokenSource();
                _ = RunFrameLoopAsync(_frameLoopCancellation.Token);
            }
        }

        [JSInvokable]
        public static void HandleNativeAppCreated()
        {
            bool raiseCreated = false;

            lock (NativeAppLifecycleLock)
            {
                if (!_nativeAppCreated)
                {
                    _nativeAppCreated = true;
                    _nativeAppDestroyed = false;
                    raiseCreated = true;
                }
            }

            if (raiseCreated)
            {
                OnCreated();
            }
        }

        [JSInvokable]
        public static void HandleNativeAppHidden()
        {
            if (!TrySetNativeAppVisibility(false))
            {
                return;
            }

            EnsureNativeAppCreated();
            OnWentBackground();
        }

        [JSInvokable]
        public static void HandleNativeAppVisible()
        {
            if (!TrySetNativeAppVisibility(true))
            {
                return;
            }

            EnsureNativeAppCreated();
            OnWentForeground();
        }

        [JSInvokable]
        public static void HandleNativeAppDestroyed()
        {
            bool raiseDestroyed = false;

            lock (NativeAppLifecycleLock)
            {
                if (!_nativeAppDestroyed)
                {
                    _nativeAppDestroyed = true;
                    _nativeAppVisible = false;
                    raiseDestroyed = true;
                }
            }

            if (!raiseDestroyed)
            {
                return;
            }

            InBackground = true;
            OnNativeAppDestroyed?.Invoke(null, EventArgs.Empty);
        }

        private static void EnsureNativeAppCreated()
        {
            if (_nativeAppCreated)
            {
                return;
            }

            HandleNativeAppCreated();
        }

        private static bool TrySetNativeAppVisibility(bool isVisible)
        {
            lock (NativeAppLifecycleLock)
            {
                if (_nativeAppDestroyed)
                {
                    return false;
                }

                if (_nativeAppVisible == isVisible)
                {
                    return false;
                }

                _nativeAppVisible = isVisible;
                return true;
            }
        }

        static partial void OnMaxFpsChanged(int fps)
        {
            RestartFrameLoop();
        }

        public class AppLifecycleProxy
        {
            [JSInvokable] public void HandleNativeAppCreated() => Super.HandleNativeAppCreated();
            [JSInvokable] public void HandleNativeAppHidden() => Super.HandleNativeAppHidden();
            [JSInvokable] public void HandleNativeAppVisible() => Super.HandleNativeAppVisible();
            [JSInvokable] public void HandleNativeAppDestroyed() => Super.HandleNativeAppDestroyed();
        }
    }
}
