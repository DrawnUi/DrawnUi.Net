namespace HelloMaui;

/// <summary>
/// Hosts <see cref="HelloShell"/>. Implicit styles give every label and button caption the app font,
/// as HelloWpf does with ConfigureStyles.
/// </summary>
public class App : Application
{
    /// <summary>Registers the implicit styles.</summary>
    public App()
    {
        Resources.Add(new Style(typeof(SkiaLabel))
        {
            ApplyToDerivedTypes = true,
            Setters = { new Setter { Property = SkiaLabel.FontFamilyProperty, Value = "FontText" } },
        });
        // SkiaButton pushes its own FontFamily onto its label, so a SkiaLabel style does not reach captions.
        Resources.Add(new Style(typeof(SkiaButton))
        {
            ApplyToDerivedTypes = true,
            Setters = { new Setter { Property = SkiaButton.FontFamilyProperty, Value = "FontText" } },
        });
    }

    /// <inheritdoc/>
    protected override Window CreateWindow(IActivationState activationState)
        => new(new HelloShell()) { Title = "DrawnUI for MAUI" };
}
