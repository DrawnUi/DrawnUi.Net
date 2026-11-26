using Foundation;

namespace Add3dGallery
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        // iOS scene lifecycle: on a system-forced exit MAUI destroys the window a second time and throws
        // (dotnet/maui #40438); the scene disconnect already destroyed it
        public override void WillTerminate(UIKit.UIApplication application)
        {
            try
            {
                base.WillTerminate(application);
            }
            catch (InvalidOperationException e) when (e.Message == "Window was already destroyed")
            {
            }
        }
    }
}