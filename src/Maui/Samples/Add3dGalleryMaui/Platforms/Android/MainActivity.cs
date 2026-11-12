using Android.App;
using Android.Content.PM;

namespace Add3dGallery;

[Activity(Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Android.OS.Bundle savedInstanceState)
    {
        // a PerfProbe run asked with: adb shell am start -n com.appomobi.add3dgallery/<activity> --es perf globechip
        MainPage.PerfRequest = Intent?.GetStringExtra("perf") ?? MainPage.PerfRequest;
        base.OnCreate(savedInstanceState);
    }
}
