using CommunityToolkit.Maui.Markup;
using DrawnUi.Draw;
using SkiaSharp;
using Xunit;

namespace UnitTests;

public class RenderingTests : DrawnTestsBase
{

    [Fact]
    public void MeasuredAndRenderedWithBindings()
    {

        var vm = new TestViewModel()
        {
            TestColor = Colors.Red,
            TestText = "Passed"
        };

        var layout = new SkiaLayout()
        {
            BindingContext = vm,
            BackgroundColor = Colors.Black,
            UseCache = SkiaCacheType.Image,
            Children = new List<SkiaControl>()
            {
                new SkiaLabel()
                    {
                        Tag="Label",
                        TextColor = Colors.White,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center
                    }
                    .Bind(SkiaLabel.TextProperty, static (TestViewModel vm) => vm.TestText),
                new SkiaShape()
                    {
                        ZIndex=-1,
                        Tag="Shape",
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill
                    }
                    .Bind(VisualElement.BackgroundColorProperty, static (TestViewModel vm) => vm.TestColor),
            }
        };

        var shape = layout.FindView<SkiaShape>("Shape");
        var label = layout.FindView<SkiaLabel>("Label");

        var destination = new SKRect(0, 0, 100, float.PositiveInfinity);
        layout.Measure(destination.Width, destination.Height, 1);

        //prepare DrawingRect
        layout.Arrange(new SKRect(0, 0, layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height),
            layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height, 1);

        // bindings applied (pixels are checked on a real canvas in DrawnUi.Net.Tests: a render without a canvas
        // is not how DrawnUI draws)
        Assert.Equal(label.Text, "Passed");
        Assert.Equal(shape.BackgroundColor, Colors.Red);
    }

    public class TestViewModel : BindableObject
    {
        private Color _testColor;
        public Color TestColor
        {
            get
            {
                return _testColor;
            }
            set
            {
                if (_testColor != value)
                {
                    _testColor = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _testText;
        public string TestText
        {
            get
            {
                return _testText;
            }
            set
            {
                if (_testText != value)
                {
                    _testText = value;
                    OnPropertyChanged();
                }
            }
        }

    }
}
