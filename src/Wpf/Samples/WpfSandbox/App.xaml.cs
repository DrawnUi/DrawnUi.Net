using System.Windows;
using DrawnUi.Draw;

namespace WpfSandbox;

/// <summary>
/// Sample entry point. DrawnUI is initialized exactly as on the other non-MAUI heads.
/// </summary>
public partial class App : Application
{
    /// <inheritdoc/>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Super.UseDrawnUi().Build();
    }
}
