namespace DrawnUi.Draw
{
    /// <summary>
    /// Columns of equal width where every child goes to the bottom of the shortest column, so items of different
    /// heights leave no gaps (Pinterest-like feed). A <see cref="SkiaLayout"/> of type <see cref="LayoutType.Masonry"/>
    /// with horizontal Fill and 2 columns by default; set <see cref="SkiaLayout.Split"/> for another count.
    /// Works with children or ItemsSource + ItemTemplate.
    /// </summary>
    public class SkiaMasonry : SkiaLayout
    {
        /// <summary>
        /// Creates a masonry layout: Type Masonry, Split 2, horizontal Fill.
        /// </summary>
        public SkiaMasonry()
        {
            Type = LayoutType.Masonry;
            Split = 2;
            HorizontalOptions = LayoutOptions.Fill;
        }
    }
}
