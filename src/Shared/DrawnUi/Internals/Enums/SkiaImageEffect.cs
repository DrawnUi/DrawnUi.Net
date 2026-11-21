namespace DrawnUi.Draw
{
	public enum SkiaImageEffect
	{
		None,
		BlackAndWhite,
		Pastel,
		/// <summary>
		/// Background color will be used to tint
		/// </summary>
		Tint,
		Darken,
		Lighten,
		Grayscale,
		Sepia,
		InvertColors,
		Contrast,
		Saturation,
		Brightness,
		Gamma,
		TSL,
		HSL,
        Custom,

        /// <summary>
        /// The picture's brightness painted in ColorTint, stretched around its middle by Contrast (1 = as is).
        /// Gives a gray or any picture one color: a texture recolored per theme, a room, a team.
        /// </summary>
        Recolor
	}
}
