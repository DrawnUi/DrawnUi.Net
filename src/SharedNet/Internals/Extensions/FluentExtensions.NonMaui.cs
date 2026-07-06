using System;

namespace DrawnUi.Draw
{
    /// <summary>
    /// Fluent gesture extensions available on non-MAUI targets (Net / OpenTK / Blazor). The MAUI build
    /// has its own OnLongPressing in FluentExtensions.Maui.cs (command/attached-property based); this
    /// event-based version is compiled only for non-MAUI (SharedNet is not part of the MAUI build), so
    /// there is no ambiguity. It subscribes to SkiaControl.LongPressing, raised by the shared gesture
    /// pipeline when the control receives a LongPressing gesture.
    /// </summary>
    public static partial class FluentExtensions
    {
        public static T OnLongPressing<T>(this T view, Action<T> action) where T : SkiaControl
        {
            try
            {
                void onLong(object s, ControlTappedEventArgs a) => action?.Invoke(view);
 
                view.LongPressing += onLong;
                string subscriptionKey = $"longpress_{Guid.NewGuid()}";
                view.ExecuteUponDisposal[subscriptionKey] = () => { view.LongPressing -= onLong; };
            }
            catch (Exception e)
            {
                Super.Log(e);
            }

            return view;
        }

        #region GRID


#if BROWSER

        public static T WithColumn<T>(this T view, int column) where T : SkiaControl
        {
            Grid.SetColumn(view, column);
            return view;
        }

        public static SkiaLayout WithColumnDefinitions(this SkiaLayout grid, string columnDefinitions)
        {
            var columns = new ColumnDefinitionCollection();

            foreach (var segment in columnDefinitions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                columns.Add(new ColumnDefinition(ParseGridLength(segment)));
            }

            grid.ColumnDefinitions = columns;
            return grid;
        }

#else
        /// <summary>
        /// Sets the Grid.Row attached property for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set the row for</param>
        /// <param name="row">The row index</param>
        /// <returns>The control for chaining</returns>
        public static T WithRow<T>(this T view, int row) where T : SkiaControl
        {
            Grid.SetRow(view, row);
            return view;
        }

        /// <summary>
        /// Sets the Grid.Column attached property for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set the column for</param>
        /// <param name="column">The column index</param>
        /// <returns>The control for chaining</returns>
        public static T WithColumn<T>(this T view, int column) where T : SkiaControl
        {
            Grid.SetColumn(view, column);
            return view;
        }



        /// <summary>
        /// Sets the Grid.RowSpan attached property for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set the row span for</param>
        /// <param name="rowSpan">The number of rows to span</param>
        /// <returns>The control for chaining</returns>
        public static T WithRowSpan<T>(this T view, int rowSpan) where T : SkiaControl
        {
            Grid.SetRowSpan(view, rowSpan);
            return view;
        }

        /// <summary>
        /// Sets the Grid.ColumnSpan attached property for the control
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set the column span for</param>
        /// <param name="columnSpan">The number of columns to span</param>
        /// <returns>The control for chaining</returns>
        public static T WithColumnSpan<T>(this T view, int columnSpan) where T : SkiaControl
        {
            Grid.SetColumnSpan(view, columnSpan);
            return view;
        }

        /// <summary>
        /// Parses a string representation of column definitions and sets them on the grid
        /// </summary>
        /// <param name="grid">The grid to set column definitions for</param>
        /// <param name="columnDefinitions">String in format like "Auto,*,2*,100"</param>
        /// <returns>The grid for chaining</returns>
        /// <exception cref="InvalidOperationException">Thrown if conversion fails</exception>
        public static SkiaLayout WithColumnDefinitions(this SkiaLayout grid, string columnDefinitions)
        {
            var cols = new ColumnDefinitionCollection();
            foreach (var raw in columnDefinitions.Split(','))
            {
                var token = raw.Trim();
                GridLength len;
                if (token == "*")
                    len = GridLength.Star;
                else if (token.EndsWith("*"))
                    len = new GridLength(double.Parse(token[..^1]), GridUnitType.Star);
                else if (token.Equals("Auto", System.StringComparison.OrdinalIgnoreCase))
                    len = GridLength.Auto;
                else
                    len = new GridLength(double.Parse(token), GridUnitType.Absolute);
                cols.Add(new ColumnDefinition(len));
            }
            grid.ColumnDefinitions = cols;
            return grid;
        }

        /// <summary>
        /// Parses a string representation of row definitions and sets them on the grid
        /// </summary>
        /// <param name="grid">The grid to set row definitions for</param>
        /// <param name="definitions">String in format like "Auto,*,2*,100"</param>
        /// <returns>The grid for chaining</returns>
        /// <exception cref="InvalidOperationException">Thrown if conversion fails</exception>
        public static SkiaLayout WithRowDefinitions(this SkiaLayout grid, string definitions)
        {
            var rows = new RowDefinitionCollection();
            foreach (var raw in definitions.Split(','))
            {
                var token = raw.Trim();
                GridLength len;
                if (token == "*")
                    len = GridLength.Star;
                else if (token.EndsWith("*"))
                    len = new GridLength(double.Parse(token[..^1]), GridUnitType.Star);
                else if (token.Equals("Auto", System.StringComparison.OrdinalIgnoreCase))
                    len = GridLength.Auto;
                else
                    len = new GridLength(double.Parse(token), GridUnitType.Absolute);
                rows.Add(new RowDefinition(len));
            }
            grid.RowDefinitions = rows;
            return grid;
        }

        /// <summary>
        /// Sets the Grid row and column in a single call
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set the grid position for</param>
        /// <param name="column">The column index</param>
        /// <param name="row">The row index</param>
        /// <returns>The control for chaining</returns>
        public static T SetGrid<T>(this T view, int column, int row) where T : SkiaControl
        {
            Grid.SetRow(view, row);
            Grid.SetColumn(view, column);
            return view;
        }

        /// <summary>
        /// Sets the Grid row, column, rowspan and columnspan in a single call
        /// </summary>
        /// <typeparam name="T">Type of SkiaControl</typeparam>
        /// <param name="view">The control to set the grid position for</param>
        /// <param name="row">The row index</param>
        /// <param name="column">The column index</param>
        /// <param name="rowSpan">The number of rows to span</param>
        /// <param name="columnSpan">The number of columns to span</param>
        /// <returns>The control for chaining</returns>
        public static T SetGrid<T>(this T view, int column, int row,
            int columnSpan, int rowSpan) where T : SkiaControl
        {
            Grid.SetRow(view, row);
            Grid.SetColumn(view, column);
            Grid.SetRowSpan(view, rowSpan);
            Grid.SetColumnSpan(view, columnSpan);
            return view;
        }

#endif




        #endregion

    }
}
