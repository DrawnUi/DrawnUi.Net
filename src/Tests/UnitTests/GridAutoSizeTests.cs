using DrawnUi.Draw;
using Xunit;
using SkiaLayout = DrawnUi.Draw.SkiaLayout;

namespace UnitTests
{
    /// <summary>
    /// Grid layout must auto-size its height to children when no RowDefinitions/HeightRequest
    /// are set, including when some children use Fill alignment (they adapt to the row,
    /// they must not inflate it to the whole available constraint).
    /// </summary>
    public class GridAutoSizeTests : DrawnTestsBase
    {
        const float Scale = 1;

        public GridAutoSizeTests()
        {
            // headless: grid structure reads child.RenderingScale which falls back to screen density
            Super.Screen.Density = 1;
        }

        static SkiaLayout CreateReplyPanelLikeGrid()
        {
            // Mirrors the repro "ReplyPanel": columns "24,*,40", implied single Auto row,
            // auto-sized children, one Fill/Fill overlay child in the last column.
            var grid = new SkiaLayout
            {
                Type = LayoutType.Grid,
                ColumnSpacing = 10,
                Padding = new Thickness(12, 8),
                HorizontalOptions = LayoutOptions.Fill,
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(new GridLength(24)),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(40)),
                }
            };

            var icon = new SkiaControl
            {
                HeightRequest = 18,
                WidthRequest = 18,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
            }.WithColumn(0);

            var content = new SkiaControl
            {
                HeightRequest = 30,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center,
            }.WithColumn(1);

            var overlay = new SkiaLayout
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Children =
                {
                    new SkiaControl
                    {
                        HeightRequest = 16,
                        WidthRequest = 16,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                    }
                }
            }.WithColumn(2);

            grid.AddSubView(icon);
            grid.AddSubView(content);
            grid.AddSubView(overlay);

            return grid;
        }

        [Fact]
        public void GridAutoRowHeightAdaptsToChildren_WithFillChild()
        {
            var grid = CreateReplyPanelLikeGrid();

            grid.CommitInvalidations();
            var measured = grid.Measure(360, 700, Scale);

            // tallest child 30 + vertical padding 16
            Assert.Equal(46, measured.Units.Height, 1.0);

            // the Fill child must end up measured at the resolved cell size, not the whole constraint
            var overlay = grid.Views[2];
            Assert.Equal(30, overlay.MeasuredSize.Units.Height, 1.0);
        }

        [Fact]
        public void GridPaddingAppliedOnce()
        {
            var grid = CreateReplyPanelLikeGrid();

            grid.CommitInvalidations();
            var measured = grid.Measure(360, 700, Scale);

            var structure = grid.GridStructureMeasured;

            // the structure works in content coordinates: padding is handled outside of it,
            // by Paint (contracts destination) and SetMeasuredAdaptToContentSize (adds to size)
            Assert.Equal(0, structure.TopEdgeOfRow(0), 1.0);
            Assert.Equal(0, structure.LeftEdgeOfColumn(0), 1.0);

            // star column gets content width minus absolute columns and spacing:
            // 360 - 24 (padding) - 24 - 40 (absolute) - 20 (2 x spacing 10) = 252
            Assert.Equal(252, structure.Columns[1].Size, 1.0);
        }

        [Fact]
        public void GridAutoRowHeightAdaptsToChildren_NoFillChildren()
        {
            var grid = CreateReplyPanelLikeGrid();
            // remove the Fill overlay, keep only auto-sized children
            grid.RemoveSubView(grid.Views[2]);

            grid.CommitInvalidations();
            var measured = grid.Measure(360, 700, Scale);

            Assert.Equal(46, measured.Units.Height, 1.0);
        }
    }
}
