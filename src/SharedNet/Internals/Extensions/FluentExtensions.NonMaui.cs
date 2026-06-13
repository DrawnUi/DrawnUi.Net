using System;

namespace DrawnUi.Draw
{
    /// <summary>
    /// Fluent gesture extensions available on non-MAUI targets (Net / OpenTK / Blazor). The MAUI build
    /// has its own OnLongPressing in FluentExtensions.Maui.cs (command/attached-property based); this
    /// event-based version is compiled only for non-MAUI (SharedNet is not part of the MAUI build), so
    /// there is no ambiguity. It subscribes to SkiaControl.LongPressing, raised by the shared gesture
    /// pipeline when the control receives a LongPressing gesture.
    /// </summary>
    public static class FluentExtensionsNonMaui
    {
        public static T OnLongPressing<T>(this T view, Action<T> action) where T : SkiaControl
        {
            try
            {
                void onLong(object s, ControlTappedEventArgs a) => action?.Invoke(view);
 
                view.LongPressing += onLong;
                string subscriptionKey = $"longpress_{Guid.NewGuid()}";
                view.ExecuteUponDisposal[subscriptionKey] = () => { view.LongPressing -= onLong; };
            }
            catch (Exception e)
            {
                Super.Log(e);
            }

            return view;
        }
    }
}
