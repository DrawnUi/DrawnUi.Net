using Android.App;
using Android.Content.PM;

namespace HelloMaui;

[Activity(Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    /// <summary>Hardware / gesture back goes through the drawn shell first.</summary>
    public override void OnBackPressed()
    {
        if (HelloShell.Instance?.GoBack(true) != true)
            base.OnBackPressed();
    }
}
