using System.Collections.ObjectModel;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Changing <c>Children</c> changes what is drawn, for every collection action (GitHub #156). Before, only Add
/// (always appended, whatever the index) and Remove were applied: Insert drew the new child last, Replace and Move
/// did nothing, and Clear did nothing on a collection the app assigned (ObservableCollection sends Reset without
/// the old items).
/// </summary>
public class ChildrenCollectionTests
{
    private static SkiaShape Item(string tag) => new() { Tag = tag, HeightRequest = 20, BackgroundColor = Colors.Red };

    private static string Tags(IEnumerable<SkiaControl> views) => string.Join(",", views.Select(v => v.Tag));

    private static (HeadlessCanvasHost host, SkiaLayout column) Column(bool appCollection)
    {
        var host = new HeadlessCanvasHost(200, 300, scale: 1f, background: Colors.White);
        var column = new SkiaLayout { Type = LayoutType.Column, Spacing = 0 };
        if (appCollection)
            column.Children = new ObservableCollection<SkiaControl>();
        column.Children.Add(Item("a"));
        column.Children.Add(Item("b"));
        host.Canvas.Content = column;
        host.AdvanceFrames(3);
        return (host, column);
    }

    /// <summary>The order the column draws its children in, read from where they landed.</summary>
    private static string DrawnOrder(SkiaLayout column) => Tags(column.Views.OrderBy(v => v.DrawingRect.Top));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Insert_Replace_Move_FollowTheCollection(bool appCollection)
    {
        var (host, column) = Column(appCollection);
        using var _ = host;

        column.Children.Insert(0, Item("i"));
        host.AdvanceFrames(2);
        Assert.Equal("i,a,b", Tags(column.Views));
        Assert.Equal("i,a,b", DrawnOrder(column));

        var replaced = column.Children[1];
        column.Children[1] = Item("r");
        host.AdvanceFrames(2);
        Assert.Equal("i,r,b", Tags(column.Views));
        Assert.Equal("i,r,b", DrawnOrder(column));
        Assert.Null(replaced.Parent);

        ((ObservableCollection<SkiaControl>)column.Children).Move(0, 2);
        host.AdvanceFrames(2);
        Assert.Equal("r,b,i", Tags(column.Views));
        Assert.Equal("r,b,i", DrawnOrder(column));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Clear_RemovesTheChildren(bool appCollection)
    {
        var (host, column) = Column(appCollection);
        using var _ = host;

        column.Children.Clear();
        host.AdvanceFrames(2);

        Assert.Empty(column.Views);
    }

    /// <summary>A Reset drops only what came from Children: a subview the control added itself stays.</summary>
    [Fact]
    public void Reset_KeepsSubviewsAddedByTheControl()
    {
        var (host, column) = Column(appCollection: true);
        using var _ = host;
        var own = Item("own");
        column.AddSubView(own);

        column.Children.Clear();
        host.AdvanceFrames(2);

        Assert.Equal("own", Tags(column.Views));
    }
}
