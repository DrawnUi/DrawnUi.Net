using DrawnUi.Extensions;
using DrawnUi.Views;
using LightNavigation;

namespace Sandbox.Views
{
    /// <summary>
    /// A Sandbox page. LightNavigation tells it when it leaves the navigation stack for good (<see cref="OnRemoved"/>),
    /// not when another page only covers it: then it is disposed with everything it shows, its canvas included (GPU
    /// context, caches, surfaces). MAUI never disposes a page or its canvas by itself.
    /// </summary>
    public class BasePageCodeBehind : BasePageReloadable, INavigationAware
    {
        public void OnRemoved()
        {
            Dispose();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                BindingContext = null;
                Handler?.DisconnectHandler();
                Content?.DisposeControlAndChildren();
                Content = null;
            });
        }

        public void OnTopmost()
        {
        }

        public void OnCovered()
        {
        }

        public void OnPopping()
        {
        }

        public void OnPushing()
        {
        }
    }
}
