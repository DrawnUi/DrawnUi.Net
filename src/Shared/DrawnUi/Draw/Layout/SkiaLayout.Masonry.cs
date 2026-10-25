namespace DrawnUi.Draw;

public partial class SkiaLayout
{
    // the width the masonry structure was built for: a change of only some items' heights keeps the columns
    private float _masonryBuiltForWidth = -1;

    /// <summary>
    /// Measures a <see cref="LayoutType.Masonry"/> layout: <see cref="Split"/> columns of equal width, each item
    /// placed at the bottom of the shortest column. When only some templated items changed size, only those are
    /// measured again and every item is placed again from the sizes already known.
    /// </summary>
    public virtual ScaledSize MeasureMasonry(SKRect rectForChildrenPixels, float scale)
    {
        if (TryMeasureMasonryChanges(rectForChildrenPixels, scale, out var result))
            return result;

        return new BuildMasonryLayout(this).Build(rectForChildrenPixels, scale);
    }

    /// <summary>
    /// Column width of a masonry, the same as a Wrap with <see cref="Split"/> columns.
    /// </summary>
    private float GetMasonryColumnWidth(float width, int columns, float scale)
    {
        return columns > 1
            ? (float)Math.Round((width - (columns - 1) * Spacing * scale) / columns)
            : width;
    }

    /// <summary>
    /// Content size of a masonry from its column bottoms.
    /// </summary>
    private ScaledSize GetMasonryContentSize(SKRect rectForChildrenPixels, float[] bottoms, int cells,
        float columnWidth, float spacing, float scale)
    {
        var used = Math.Min(bottoms.Length, cells);
        var width = used > 0 ? used * columnWidth + (used - 1) * spacing : 0f;
        var height = bottoms.Max();

        if (HorizontalOptions.Alignment == LayoutAlignment.Fill && WidthRequest < 0)
        {
            width = float.IsFinite(rectForChildrenPixels.Width) ? rectForChildrenPixels.Width : width;
        }
        if (VerticalOptions.Alignment == LayoutAlignment.Fill && HeightRequest < 0)
        {
            height = float.IsFinite(rectForChildrenPixels.Height) ? rectForChildrenPixels.Height : height;
        }

        return ScaledSize.FromPixels(width, height, scale);
    }

    /// <summary>
    /// Templated masonry whose width did not change and where some items asked to be measured again (an image
    /// arrived, a text grew): measures only those, then places every item again from the known sizes. Same
    /// result as a full measure, without measuring every item. Returns false when a full measure is needed.
    /// </summary>
    private bool TryMeasureMasonryChanges(SKRect rectForChildrenPixels, float scale, out ScaledSize result)
    {
        result = ScaledSize.Default;

        if (!IsTemplated || DirtyChildrenTracker.IsEmpty || !WasMeasured
            || UsingCacheType == SkiaCacheType.ImageDoubleBuffered || IsCacheComposite
            || rectForChildrenPixels.Width != _masonryBuiltForWidth
            || Superview?.DrawingThreadId != Thread.CurrentThread.ManagedThreadId)
        {
            return false;
        }

        var structure = GetStackStructureForMeasuring();
        var count = structure?.GetCount() ?? 0;
        if (count == 0 || count != ChildrenFactory.GetChildrenCount())
        {
            return false;
        }

        var useOneTemplate = RecyclingTemplate != RecyclingTemplate.Disabled;
        var template = useOneTemplate ? ChildrenFactory.GetTemplateInstance() : null;
        try
        {
            HashSet<ControlInStack> changed = null;
            foreach (var dirty in DirtyChildrenTracker.GetList())
            {
                var cell = FindCellByContextIndex(structure, dirty.ContextIndex);
                if (cell == null)
                    return false;

                var view = ChildrenFactory.GetViewForIndex(cell.ControlIndex, template, 0, useOneTemplate);
                if (view == null || !view.CanDraw)
                    return false;

                var measured = MeasureChild(view, cell.Area.Width, float.PositiveInfinity, scale);
                view.NeedMeasure = false;
                if (measured.IsEmpty)
                    return false;

                if (Math.Abs(measured.Pixels.Height - cell.Measured.Pixels.Height) < 0.5f
                    && Math.Abs(measured.Pixels.Width - cell.Measured.Pixels.Width) < 0.5f)
                    continue;

                cell.Measured = measured;
                (changed ??= new()).Add(cell);
            }

            var columns = Math.Max(1, Split);
            var spacing = (float)Math.Round(Spacing * scale);
            var columnWidth = GetMasonryColumnWidth(rectForChildrenPixels.Width, columns, scale);
            var bottoms = new float[columns];

            if (changed == null)
            {
                // nothing moved: the content keeps its size
                foreach (var cell in structure.GetChildren())
                    bottoms[cell.Column] = Math.Max(bottoms[cell.Column], cell.Area.Bottom - rectForChildrenPixels.Top);
            }
            else
            {
                var rows = new int[columns];
                var placed = new LayoutStructure();
                foreach (var cell in structure.GetChildren()) // item order
                {
                    var column = 0;
                    for (var c = 1; c < columns; c++)
                    {
                        if (bottoms[c] < bottoms[column])
                            column = c;
                    }

                    var left = rectForChildrenPixels.Left + column * (columnWidth + spacing);
                    var top = rectForChildrenPixels.Top + bottoms[column] + (rows[column] > 0 ? spacing : 0);
                    var height = cell.Measured.Pixels.Height;

                    if (changed.Contains(cell))
                    {
                        var view = ChildrenFactory.GetViewForIndex(cell.ControlIndex, template, 0, useOneTemplate);
                        cell.Area = new SKRect(left, top, left + columnWidth, top + height);
                        LayoutCell(cell.Measured, cell, view, cell.Area, scale);
                    }
                    else
                    {
                        var dx = left - cell.Area.Left;
                        var dy = top - cell.Area.Top;
                        if (dx != 0 || dy != 0)
                        {
                            cell.Area = new SKRect(cell.Area.Left + dx, cell.Area.Top + dy,
                                cell.Area.Right + dx, cell.Area.Bottom + dy);
                            cell.Destination = new SKRect(cell.Destination.Left + dx, cell.Destination.Top + dy,
                                cell.Destination.Right + dx, cell.Destination.Bottom + dy);
                        }
                    }

                    cell.Column = column;
                    cell.Row = rows[column];
                    placed.Add(cell, column, rows[column]++);
                    bottoms[column] = cell.Area.Bottom - rectForChildrenPixels.Top;
                }

                StackStructureMeasured = placed;
            }

            ClearDirtyChildren();
            result = GetMasonryContentSize(rectForChildrenPixels, bottoms, count, columnWidth, spacing, scale);
            return true;
        }
        finally
        {
            if (useOneTemplate && template != null)
                ChildrenFactory.ReleaseTemplateInstance(template);
        }
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
            var columnWidth = _layout.GetMasonryColumnWidth(available.Width, columns, scale);

            var bottoms = new float[columns]; // each column's bottom, from the top of the content
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
                    if (bottoms[c] < bottoms[column])
                        column = c;
                }

                var left = available.Left + column * (columnWidth + spacing);
                var top = available.Top + bottoms[column] + (rows[column] > 0 ? spacing : 0);

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

                cell.Column = column;
                cell.Row = rows[column];
                structure.Add(cell, column, rows[column]++);
                bottoms[column] = cell.Area.Bottom - available.Top;
            }

            _layout.StackStructureMeasured = structure;
            _layout._masonryBuiltForWidth = available.Width;

            if (_layout.IsTemplated)
            {
                _layout.FirstMeasuredIndexLocal = index >= 0 ? 0 : -1;
                _layout.LastMeasuredIndexLocal = index;
            }

            return _layout.GetMasonryContentSize(rectForChildrenPixels, bottoms, structure.GetCount(),
                columnWidth, spacing, scale);
        }
    }
}
