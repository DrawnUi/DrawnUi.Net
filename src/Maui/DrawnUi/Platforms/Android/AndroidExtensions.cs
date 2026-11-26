using Android.Graphics.Drawables;
using Microsoft.Maui.Controls.Compatibility.Platform.Android;
using Microsoft.Maui.Controls.Platform;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Timer = System.Timers.Timer;

namespace DrawnUi.Draw;

public static class AndroidExtensions
{

    static Dictionary<string, Tasks.RunningTimer> Timers { get; } = new();

    public static void StartTimerOnMainThreadAsync(TimeSpan when, CancellationToken cancellationToken, Func<Task<bool>> task)
    {
        var repeatDelayMs = when.TotalMilliseconds;
        var id = Guid.NewGuid().ToString();

        async void TimerAction(object state)
        {
            var wrapper = state as Tasks.RunningTimer;

            if (wrapper.IsExecuting)
                return;

            bool ret;
            wrapper.IsExecuting = true;

            MainThread.BeginInvokeOnMainThread(async () =>
            {

                try
                {
                    ret = await task();
                }
                catch (Exception e) //we could land here upon cancellation..
                {
                    Console.WriteLine(e);
                    ret = false; //remove timer
                }

                try
                {
                    var myself = Timers[id];
                    if (myself != null) myself.Executed++;
                    //Debug.WriteLine($"[StartTimer] Exec timer {id} {myself.Executed}");
                    var needCancel = false;
                    if (cancellationToken != default)
                        if (cancellationToken.IsCancellationRequested)
                            needCancel = true;

                    if (!ret || repeatDelayMs < 1 || needCancel)
                        //todo kill timer
                        if (myself != null && myself.Timer != null)
                        {
                            myself.Timer.Change(Timeout.Infinite, Timeout.Infinite);
                            myself.Timer.Dispose();
                            Timers[myself.Id] = null;
#if DEBUG
                            //Console.WriteLine($"[StartTimer] Stopped timer {id}");
#endif
                        }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                finally
                {
                    wrapper.IsExecuting = false;
                }
            });

        }

        var logTimer = new Tasks.RunningTimer
        {
            Id = id,
            RepeatingDelay = when,
            StartDelay = when
        };
        Timers[id] = logTimer;

        // Register a callback to dispose the timer if the cancellationToken is canceled before it starts.
        if (cancellationToken != default)
            cancellationToken.Register(() =>
            {
                logTimer.Timer?.Change(Timeout.Infinite, Timeout.Infinite);
                logTimer.Timer?.Dispose();
                Timers[id] = null;
            });

        logTimer.Timer =
            new System.Threading.Timer(TimerAction, logTimer, logTimer.StartDelay, logTimer.RepeatingDelay);
        //Debug.WriteLine($"[BackgroundTaskQueue] Added task on timer {id}");

#if DEBUG
        //Console.WriteLine($"[StartTimer] Added task on timer delay {logTimer.StartDelay} repeat {logTimer.RepeatingDelay} id  {id}");
#endif
    }

    public static IFontManager RequireFontManager(this Element element, bool fallbackToAppMauiContext = false)
        => element.Handler.MauiContext.Services.GetRequiredService<IFontManager>();

    public static NavigationRootManager GetNavigationRootManager(this IMauiContext mauiContext) =>
        mauiContext.Services.GetRequiredService<NavigationRootManager>();

    public static bool IsNullOrDisposed(this Java.Lang.Object javaObject)
    {
        return javaObject == null || javaObject.Handle == IntPtr.Zero;
    }
    public static async Task<Drawable> GetDrawable(this ImageSource imageSource, IElementHandler handler)
    {
        IImageSourceServiceProvider imageSourceServiceProvider = handler.GetRequiredService<IImageSourceServiceProvider>();
        IImageSourceService service = imageSourceServiceProvider.GetImageSourceService(imageSource);
        var result = await service.GetDrawableAsync(imageSource, handler.MauiContext.Context);



        return result.Value;
    }
    public static IServiceProvider GetServiceProvider(this IElementHandler handler)
    {
        var context = handler.MauiContext ??
                      throw new InvalidOperationException($"Unable to find the context. The {nameof(ElementHandler.MauiContext)} property should have been set by the host.");

        var services = context?.Services ??
                       throw new InvalidOperationException($"Unable to find the service provider. The {nameof(ElementHandler.MauiContext)} property should have been set by the host.");

        return services;
    }

    public static T GetRequiredService<T>(this IElementHandler handler)
        where T : notnull
    {
        var services = handler.GetServiceProvider();

        var service = services.GetRequiredService<T>();

        return service;
    }

    public static IImageSourceHandler GetHandler(this ImageSource source)
    {
        //Image source handler to return
        IImageSourceHandler returnValue = null;
        //check the specific source type and return the correct image source handler
        if (source is UriImageSource)
        {
            returnValue = new ImageLoaderSourceHandler();
        }
        else if (source is FileImageSource)
        {
            returnValue = new FileImageSourceHandler();
        }
        else if (source is StreamImageSource)
        {
            returnValue = new StreamImagesourceHandler();
        }
        else if (source is FontImageSource)
        {
            returnValue = new FontImageSourceHandler();
        }
        return returnValue;
    }
}
