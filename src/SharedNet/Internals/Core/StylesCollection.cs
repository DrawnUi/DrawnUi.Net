namespace DrawnUi.Draw
{
    public class StylesCollection : IStylesCollection
    {
        public IStylesCollection AddStyle(Style style)
        {
            Styles.Add(style);
            return this;
        }

        public static List<Style> Styles { get; } = new();
    }
}