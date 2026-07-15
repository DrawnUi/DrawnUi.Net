namespace DrawnUi.Draw
{
    public enum PrebuiltControlStyle
    {
        Unset,

        /// <summary>
        /// Will select the style upon the current platform
        /// </summary>
        Platform,

        /// <summary>
        /// Apple iOS style
        /// </summary>
        Cupertino,

        /// <summary>
        /// Google Android style, Material Design 2 era. Kept as-is for apps that already
        /// ship with this look; new apps usually want <see cref="Material3"/>.
        /// </summary>
        Material,

        /// <summary>
        /// Windows style
        /// </summary>
        Windows,

        /// <summary>
        /// Google Android style, Material Design 3 (Material You): primary #6750A4 palette,
        /// outlined/filled switch, pill buttons, progress with gap + stop indicator.
        /// This is what <see cref="Platform"/> resolves to on Android.
        /// Appended after Windows to keep existing enum numeric values stable.
        /// </summary>
        Material3
    }
}
