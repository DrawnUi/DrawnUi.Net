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
                    len = new GridLength(double.Parse(token[..^1], System.Globalization.CultureInfo.InvariantCulture), GridUnitType.Star);
                else if (token.Equals("Auto", System.StringComparison.OrdinalIgnoreCase))
                    len = GridLength.Auto;
                else
                    len = new GridLength(double.Parse(token, System.Globalization.CultureInfo.InvariantCulture), GridUnitType.Absolute);
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
                    len = new GridLength(double.Parse(token[..^1], System.Globalization.CultureInfo.InvariantCulture), GridUnitType.Star);
                else if (token.Equals("Auto", System.StringComparison.OrdinalIgnoreCase))
                    len = GridLength.Auto;
                else
                    len = new GridLength(double.Parse(token, System.Globalization.CultureInfo.InvariantCulture), GridUnitType.Absolute);
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





        #endregion

        #region THICKNESS

        public static Thickness WithTop(this Thickness existing, double value)
        {
            return new Thickness
            {
                Top = value,
                Bottom = existing.Bottom,
                Left = existing.Left,
                Right = existing.Right
            };
        }
        public static Thickness WithBottom(this Thickness existing, double value)
        {
            return new Thickness
            {
                Top = existing.Top,
                Bottom = value,
                Left = existing.Left,
                Right = existing.Right
            };
        }
        public static Thickness WithLeft(this Thickness existing, double value)
        {
            return new Thickness
            {
                Top = existing.Top,
                Bottom = existing.Bottom,
                Left = value,
                Right = existing.Right
            };
        }
        public static Thickness WithRight(this Thickness existing, double value)
        {
            return new Thickness
            {
                Top = existing.Top,
                Bottom = existing.Bottom,
                Left = existing.Left,
                Right = value
            };
        }

        #endregion

        /// <summary>
        /// Sets the shape's <see cref="SkiaShape.Points"/> from a string such as "0,0; 1,0; 0.5,1": points separated by
        /// ';', each with its X and Y separated by ',' or a space, invariant culture. Same as the MAUI build.
        /// </summary>
        /// <exception cref="FormatException">A point is not two numbers.</exception>
        public static SkiaShape WithPoints(this SkiaShape shape, string points)
        {
            var list = new List<SkiaPoint>();
            if (!string.IsNullOrWhiteSpace(points))
            {
                foreach (var part in points.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var point = part.Trim();
                    if (point.Length == 0)
                        continue;

                    var xy = point.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (xy.Length != 2)
                        throw new FormatException($"Invalid point format: '{point}'");

                    if (!double.TryParse(xy[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
                        || !double.TryParse(xy[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
                        throw new FormatException($"Invalid coordinate values in point: '{point}'");

                    list.Add(new SkiaPoint(x, y));
                }
            }

            shape.Points = list;
            return shape;
        }

#if !BROWSER && !WEB // the Blazor and pure Wasm builds have their own copies
        /// <summary>
        /// Runs <paramref name="action"/> with the new state every time the toggle (a switch, a checkbox) changes;
        /// unsubscribed when the control is disposed. Same as the MAUI and Blazor builds.
        /// </summary>
        public static T OnToggled<T>(this T view, Action<T, bool> action) where T : SkiaToggle
        {
            void onToggled(object sender, bool isToggled) => action?.Invoke(view, isToggled);

            view.Toggled += onToggled;
            string subscriptionKey = $"toggled_{Guid.NewGuid()}";
            view.ExecuteUponDisposal[subscriptionKey] = () => { view.Toggled -= onToggled; };

            return view;
        }

        /// <summary>
        /// The color with its brightest channel scaled to <paramref name="luminosity"/> (0 to 1), hue and alpha kept.
        /// Same as the Blazor build.
        /// </summary>
        public static Color WithLuminosity(this Color color, float luminosity)
        {
            float target = Math.Clamp(luminosity, 0f, 1f);
            float current = Math.Max(color.Red, Math.Max(color.Green, color.Blue));

            if (current <= 0f)
                return new Color(target, target, target, color.Alpha);

            float scale = target / current;
            return new Color(color.Red * scale, color.Green * scale, color.Blue * scale, color.Alpha);
        }
#endif
    }
}
