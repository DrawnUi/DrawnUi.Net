using System.Reflection;

namespace Sandbox
{
    public partial class App : Application
    {
        public App()
        {
            Super.SetLocale("en");

            InitializeComponent();
        }



        protected override Window CreateWindow(IActivationState activationState) => new Window(new AppShell());

        public void SetMainPage(Page page)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Windows.Count > 0)
                    Windows[0].Page = page;
            });
        }

        public static App Instance => App.Current as App;
    }




}
