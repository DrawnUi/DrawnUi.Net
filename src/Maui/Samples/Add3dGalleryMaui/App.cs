namespace Add3dGallery;

/// <summary>Hosts the one page.</summary>
public class App : Application
{
    /// <inheritdoc/>
    protected override Window CreateWindow(IActivationState activationState)
        => new(new MainPage()) { Title = "DrawnUI 3D" };
}
