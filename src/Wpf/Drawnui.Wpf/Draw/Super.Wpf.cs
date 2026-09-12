namespace DrawnUi.Draw;

public partial class Super
{
    /// <summary>
    /// Entry point for app startup, same as on the other heads:
    /// <c>Super.UseDrawnUi().ConfigureFonts(...).Build()</c>. Call it once before the first
    /// <see cref="DrawnUi.Wpf.DrawnUiElement"/> is shown.
    /// </summary>
    public static DrawnUiBuilder UseDrawnUi() => new();
}
