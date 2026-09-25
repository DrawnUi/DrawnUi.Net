namespace EmptyCode;

public static partial class Program
{
    /// <summary>Called by wwwroot/main.js once the .NET runtime is up.</summary>
    [JSExport]
    public static Task Main() => Super.UseDrawnUi()
        // Same default as the DrawnUI Fiddle: labels without a FontFamily draw in FontText.
        .ConfigureStyles(styles => styles.AddStyle(new Style
        {
            ApplyToDerivedTypes = true,
            TargetType = typeof(SkiaLabel),
            Setters = { new Setter { Property = SkiaLabel.FontFamilyProperty, Value = "FontText" } },
        }))
        .ConfigureFonts(fonts =>
        {
            // Same aliases the DrawnUI Fiddle registers, so exported snippets
            // find the fonts they were written against.
            fonts.AddFont("OpenSans-Regular.ttf", "FontText");
            fonts.AddFont("OpenSans-Semibold.ttf", "FontTextTitle");
        })
        // The factory runs again on every hot reload (dotnet watch), so it builds a fresh tree each time.
        .RunAsync("drawnui-canvas", MainPage.CreateCanvas);
}
