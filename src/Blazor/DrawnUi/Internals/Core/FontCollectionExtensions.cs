namespace DrawnUi.Draw;

/// <summary>
/// Registers font subsets shipped inside the DrawnUi.Blazor.Core package
/// (served as static web assets from <c>_content/DrawnUi.Blazor.Core/fonts/</c>).
/// Browsers give WASM no access to OS fonts, so every glyph must come from a
/// downloaded font; these opt-ins cover the common gaps at minimal transfer cost.
/// Subsets are built by <c>dev/fonts/subset_fonts.py</c>.
/// </summary>
public static class FontCollectionExtensions
{
    private const string ContentRoot = "_content/DrawnUi.Blazor.Core/fonts/";

    /// <summary>
    /// Registers a color emoji subset (NotoColorEmoji faces+hands tier, ~900 KB,
    /// alias <c>FontEmoji</c>) so common emoji render instead of tofu.
    /// Downloaded at startup together with the other registered fonts.
    /// </summary>
    public static IFontCollection AddEmojis(this IFontCollection fonts)
    {
        return fonts.AddFont(ContentRoot + "NotoColorEmoji-Subset.ttf", "FontEmoji");
    }

    /// <summary>
    /// Registers text-symbol subsets (~285 KB total) covering arrows, math operators,
    /// geometric shapes, letterlike/technical symbols (Noto Sans Math subset, alias
    /// <c>FontSymbols</c>) plus misc symbols and dingbats (Noto Sans Symbols 2 subset,
    /// alias <c>FontSymbols2</c>). Fills blocks most text fonts lack, e.g. U+2191 '↑'.
    /// Downloaded at startup together with the other registered fonts.
    /// </summary>
    public static IFontCollection AddSymbols(this IFontCollection fonts)
    {
        return fonts
            .AddFont(ContentRoot + "NotoSansMathSymbols-Subset.ttf", "FontSymbols")
            .AddFont(ContentRoot + "NotoSansSymbols2-Subset.ttf", "FontSymbols2");
    }
}
