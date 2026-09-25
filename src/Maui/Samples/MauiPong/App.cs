namespace MauiPong;

/// <summary>Hosts the one game page in a window titled like the other Pong hosts.</summary>
public class App : Application
{
    /// <inheritdoc/>
    protected override Window CreateWindow(IActivationState activationState)
        => new(new MainPage()) { Title = "Pong – DrawnUI MAUI" };
}
