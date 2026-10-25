namespace DrawnUi.Draw;

public partial class SkiaLayout
{
    /// <summary>
    /// Measures a <see cref="LayoutType.Masonry"/> layout: <see cref="Split"/> columns of equal width, each item
    /// placed at the bottom of the shortest column.
    /// </summary>
    public virtual ScaledSize MeasureMasonry(SKRect rectForChildrenPixels, float scale)
    {
        return new BuildMasonryLayout(this).Build(rectForChildrenPixels, scale);
    }

    /// <summary>
    /// Implementation for <see cref="LayoutType.Masonry"/>: columns of equal width, every item goes to the
    /// shortest column, so items of different heights leave no gaps under the shorter ones. Items keep their
    /// order across the columns as far as the heights allow (item 0 is top left). Every item is measured,
    /// whatever <see cref="MeasureItemsStrategy"/> says: a column's height depends on all items above.
    /// The structure is keyed (column, row inside that column) and filled in item order.
    /// </summary>
    public class BuildMasonryLayout : StackLayoutStructure
    {
        /// <summary>
        /// Creates the builder for a masonry layout.
        /// </summary>
        public BuildMasonryLayout(SkiaLayout layout) : base(layout)
        {
        }

        /// <inheritdoc />
        public override ScaledSize Build(SKRect rectForChildrenPixels, float scale)
        {
            var columns = Math.Max(1, _layout.Split);
            var spacing = (float)Math.Round(_layout.Spacing * scale);
            var available = rectForChildrenPixels;

            // same column width as a Wrap with Split columns
            var columnWidth = columns > 1
                ? (float)Math.Round((available.Width - (columns - 1) * _layout.Spacing * scale) / columns)
                : available.Width;

            var heights = new float[columns]; // each column's bottom, from the top of the content
            var rows = new int[columns];
            var structure = new LayoutStructure();
            var index = -1;

            foreach (var child in EnumerateViewsForMeasurement())
            {
                index++;

                child.OnBeforeMeasure();
                if (!child.CanDraw)
                {
                    continue;
                }

                var column = 0;
                for (var c = 1; c < columns; c++)
                {
                    if (heights[c] < heights[column])
                        column = c;
                }

                var left = available.Left + column * (columnWidth + spacing);
                var top = available.Top + heights[column] + (rows[column] > 0 ? spacing : 0);

                var cell = CreateWrapper(index, _layout.IsTemplated ? null : child);

                // the height is the item's own: measured unbounded, then arranged into exactly that slot
                var measured = MeasureCell(new SKRect(left, top, left + columnWidth, float.PositiveInfinity),
                    cell, child, scale);
                if (measured.IsEmpty)
                {
                    continue;
                }

                cell.Area = new SKRect(left, top, left + columnWidth, top + measured.Pixels.Height);
                _layout.LayoutCell(measured, cell, child, cell.Area, scale);

                structure.Add(cell, column, rows[column]++);
                heights[column] = cell.Area.Bottom - available.Top;
            }

            _layout.StackStructureMeasured = structure;

            if (_layout.IsTemplated)
            {
                _layout.FirstMeasuredIndexLocal = index >= 0 ? 0 : -1;
                _layout.LastMeasuredIndexLocal = index;
            }

            var used = Math.Min(columns, structure.GetCount());
            var stackWidth = used > 0 ? used * columnWidth + (used - 1) * spacing : 0f;
            var stackHeight = heights.Max();

            if (_layout.HorizontalOptions.Alignment == LayoutAlignment.Fill && _layout.WidthRequest < 0)
            {
                stackWidth = float.IsFinite(rectForChildrenPixels.Width) ? rectForChildrenPixels.Width : stackWidth;
            }
            if (_layout.VerticalOptions.Alignment == LayoutAlignment.Fill && _layout.HeightRequest < 0)
            {
                stackHeight = float.IsFinite(rectForChildrenPixels.Height) ? rectForChildrenPixels.Height : stackHeight;
            }

            return ScaledSize.FromPixels(stackWidth, stackHeight, scale);
        }
    }
}
