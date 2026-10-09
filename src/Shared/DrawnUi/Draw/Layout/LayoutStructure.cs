namespace DrawnUi.Draw;

public class LayoutStructure : DynamicGrid<ControlInStack>
{
    public LayoutStructure()
    {

    }

    /// <summary>
    /// Returns a new instance of LayoutStructure with the same items.
    /// This performs a shallow copy of the existing structure.
    /// </summary>
    public LayoutStructure Clone()
    {
        var clone = new LayoutStructure();
        foreach (var kvp in grid)
        {
            var item = kvp.Value;
            clone.Add(item, item.Column, item.Row);
        }
        return clone;
    }

    public ControlInStack GetForIndex(int index)
    {
        return grid.Values.FirstOrDefault(x => x.ControlIndex == index);
    }

    public LayoutStructure(List<List<ControlInStack>> grid)
    {
        int row = 0;
        foreach (var line in grid)
        {
            var col = 0;
            foreach (var controlInStack in line)
            {
                controlInStack.Column = col;
                controlInStack.Row = row;

                Add(controlInStack, col, row);
                col++;
            }
            row++;
        }
    }

    /// <summary>
    /// Appends cells in index order to a grid of <paramref name="columns"/> columns: the first ones fill the
    /// last row when it is partial, the rest start whole rows. Keeps structure rows equal to the grid's rows
    /// when items arrive in batches that end mid-row (background measurement of a Split grid).
    /// </summary>
    public void AppendContinuing(IReadOnlyList<ControlInStack> cells, int columns)
    {
        int row = MaxRows - 1;
        int col = row >= 0 ? GetRow(row).Count() : columns;
        if (row < 0 || col >= columns)
        {
            row++;
            col = 0;
        }

        foreach (var cell in cells)
        {
            cell.Column = col;
            cell.Row = row;
            Add(cell, col, row);
            if (++col >= columns)
            {
                col = 0;
                row++;
            }
        }
    }

    public void Append(List<List<ControlInStack>> grid)
    {
        int row = MaxRows;
        foreach (var line in grid)
        {
            var col = 0;
            foreach (var controlInStack in line)
            {
                controlInStack.Column = col;
                controlInStack.Row = row;

                Add(controlInStack, col, row);
                col++;
            }
            row++;
        }
    }

}
