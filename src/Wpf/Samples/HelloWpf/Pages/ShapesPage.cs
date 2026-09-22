using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;
using Color = DrawnUi.Color;

namespace HelloWpf.Pages;

/// <summary>
/// SkiaShape: every Type, fill + stroke, gradients, bevel, shadows, corner radii, and children
/// clipped to the shape. Ported from the React demo's ShapesPage.tsx.
/// </summary>
public class ShapesPage : SkiaLayer
{
    private const string Heart =
        "M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z";

    private static readonly double[] StarPoints =
    {
        0.5, 0.0, 0.62, 0.38, 1.0, 0.38, 0.69, 0.61, 0.81, 1.0,
        0.5, 0.76, 0.19, 1.0, 0.31, 0.61, 0.0, 0.38, 0.38, 0.38,
    };

    /// <summary>Builds the page.</summary>
    public ShapesPage()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;

        Children = new List<SkiaControl>
        {
            new SkiaScroll
            {
                Orientation = ScrollOrientation.Vertical,
                Content = new SkiaStack
                {
                    Spacing = 20,
                    Padding = new Thickness(16),
                    Children = new List<SkiaControl>
                    {
                        Title("SkiaShape", 24),
                        Caption("Stroke is drawn inside the bounds; children are clipped to the shape."),

                        Title("FillGradient / StrokeGradient", 20),
                        Row(new List<SkiaControl>
                        {
                            Demo("Linear · Angle=45", new SkiaShape
                            {
                                Type = ShapeType.Rectangle,
                                CornerRadius = 12,
                                WidthRequest = 110,
                                HeightRequest = 70,
                                FillGradient = Gradient(GradientType.Linear, new[] { "#0D6EFD", "#D63384" }, angle: 45),
                            }.Center()),

                            Demo("Circular at (0.5, 0.5)", new SkiaShape
                            {
                                Type = ShapeType.Circle,
                                WidthRequest = 80,
                                LockRatio = 1,
                                FillGradient = Gradient(GradientType.Circular, new[] { "#FFFFFF", "#0D6EFD", "#0A2A6B" },
                                    startX: 0.5f, startY: 0.5f, positions: new List<double> { 0, 0.6, 1 }),
                            }.Center()),

                            Demo("Oval · Light=1.4", new SkiaShape
                            {
                                Type = ShapeType.Ellipse,
                                WidthRequest = 120,
                                HeightRequest = 60,
                                FillGradient = Gradient(GradientType.Oval, new[] { "#20C997", "#0F3460" },
                                    startX: 0.5f, startY: 0.5f, light: 1.4f),
                            }.Center()),

                            Demo("Sweep · full circle", new SkiaShape
                            {
                                Type = ShapeType.Circle,
                                WidthRequest = 80,
                                LockRatio = 1,
                                // A sweep turns about its centre, so it needs one — at the default
                                // (0,0) every angle samples the same colour and it renders flat.
                                FillGradient = Gradient(GradientType.Sweep,
                                    new[] { "#E94560", "#FFC107", "#20C997", "#0D6EFD", "#E94560" },
                                    startX: 0.5f, startY: 0.5f),
                            }.Center()),

                            Demo("StrokeGradient · StrokeWidth 8", new SkiaShape
                            {
                                Type = ShapeType.Rectangle,
                                CornerRadius = 16,
                                WidthRequest = 110,
                                HeightRequest = 70,
                                StrokeWidth = 8,
                                ClipBackgroundColor = true,
                                StrokeGradient = Gradient(GradientType.Linear, new[] { "#FFC107", "#D63384" },
                                    endX: 1, endY: 0),
                            }.Center()),

                            Demo("SkiaLabel FillGradient → glyphs", new SkiaLabel("Gradient text, line by line")
                            {
                                FontSize = 18,
                                FontFamily = "FontTextBold",
                                TextColor = Colors.White,
                                HorizontalTextAlignment = DrawTextAlignment.Center,
                                WidthRequest = 130,
                                FillGradient = Gradient(GradientType.Linear, new[] { "#FFC107", "#D63384" }, angle: 90),
                            }.Center()),

                            Demo("Label: background + gradient text", new SkiaLabel("bg + text")
                            {
                                FontSize = 16,
                                TextColor = Colors.White,
                                BackgroundColor = Color.Parse("#212529"),
                                Padding = new Thickness(12, 8),
                                GradientByLines = false,
                                FillGradient = Gradient(GradientType.Linear, new[] { "#6610F2", "#0DCAF0" }, angle: 0),
                            }.Center()),
                        }),

                        Title("Bevel / Emboss", 20),
                        Row(new List<SkiaControl>
                        {
                            Demo("BevelType=Bevel · Depth 4", Beveled(BevelType.Bevel, 4)),
                            Demo("BevelType=Emboss · Depth 4", Beveled(BevelType.Emboss, 4)),
                            Demo("Circle · Bevel, coloured edges", new SkiaShape
                            {
                                Type = ShapeType.Circle,
                                BackgroundColor = Color.Parse("#0D6EFD"),
                                WidthRequest = 80,
                                LockRatio = 1,
                                BevelType = BevelType.Bevel,
                                Bevel = new SkiaBevel
                                {
                                    Depth = 6,
                                    LightColor = Color.Parse("#9EC5FE"),
                                    ShadowColor = Color.Parse("#052C65"),
                                    Opacity = 0.8,
                                },
                            }.Center()),
                            Demo("Polygon (star) · Emboss", new SkiaShape
                            {
                                Type = ShapeType.Polygon,
                                Points = Star(),
                                BackgroundColor = Color.Parse("#FFC107"),
                                WidthRequest = 90,
                                HeightRequest = 90,
                                BevelType = BevelType.Emboss,
                                Bevel = new SkiaBevel { Depth = 3, Opacity = 0.7 },
                            }.Center()),
                            Demo("Path (heart) · Bevel", new SkiaShape
                            {
                                Type = ShapeType.Path,
                                PathData = Heart,
                                BackgroundColor = Color.Parse("#D63384"),
                                WidthRequest = 90,
                                HeightRequest = 90,
                                BevelType = BevelType.Bevel,
                                Bevel = new SkiaBevel { Depth = 3, Opacity = 0.7 },
                            }.Center()),
                            Demo("Sharp rectangle · Bevel", new SkiaShape
                            {
                                Type = ShapeType.Rectangle,
                                BackgroundColor = Color.Parse("#6C757D"),
                                WidthRequest = 110,
                                HeightRequest = 70,
                                BevelType = BevelType.Bevel,
                                Bevel = new SkiaBevel { Depth = 5, Opacity = 1 },
                            }.Center()),
                        }),

                        Title("Shadows", 20),
                        Row(new List<SkiaControl>
                        {
                            Demo("Shadows=[Y 4, Blur 6, Opacity .5]", new SkiaShape
                            {
                                Type = ShapeType.Rectangle,
                                CornerRadius = 12,
                                BackgroundColor = Colors.White,
                                WidthRequest = 100,
                                HeightRequest = 60,
                                Shadows = new List<SkiaShadow>
                                {
                                    new() { X = 0, Y = 4, Blur = 6, Opacity = 0.5, Color = Colors.Black },
                                },
                            }.Center()),
                            Demo("Coloured, offset X", new SkiaShape
                            {
                                Type = ShapeType.Circle,
                                BackgroundColor = Color.Parse("#FFC107"),
                                WidthRequest = 64,
                                LockRatio = 1,
                                Shadows = new List<SkiaShadow>
                                {
                                    new() { X = 6, Y = 6, Blur = 4, Opacity = 0.8, Color = Color.Parse("#6610F2") },
                                },
                            }.Center()),
                            Demo("Two shadows (glow + drop)", new SkiaShape
                            {
                                Type = ShapeType.Rectangle,
                                CornerRadius = 30,
                                BackgroundColor = Color.Parse("#20C997"),
                                WidthRequest = 110,
                                HeightRequest = 60,
                                Shadows = new List<SkiaShadow>
                                {
                                    new() { X = 0, Y = 0, Blur = 10, Opacity = 0.9, Color = Color.Parse("#20C997") },
                                    new() { X = 0, Y = 6, Blur = 4, Opacity = 0.6, Color = Colors.Black },
                                },
                            }.Center()),
                            Demo("Hollow + shadow", new SkiaShape
                            {
                                Type = ShapeType.Rectangle,
                                CornerRadius = 12,
                                ClipBackgroundColor = true,
                                StrokeColor = Colors.White,
                                StrokeWidth = 2,
                                WidthRequest = 100,
                                HeightRequest = 60,
                                Shadows = new List<SkiaShadow>
                                {
                                    new() { X = 0, Y = 5, Blur = 5, Opacity = 0.7, Color = Colors.Black },
                                },
                            }.Center()),
                        }),

                        Title("Types", 20),
                        Row(new List<SkiaControl>
                        {
                            Demo("Rectangle CornerRadius=16", new SkiaShape
                            {
                                Type = ShapeType.Rectangle,
                                CornerRadius = 16,
                                BackgroundColor = Color.Parse("#0D6EFD"),
                                StrokeColor = Colors.White,
                                StrokeWidth = 3,
                                WidthRequest = 110,
                                HeightRequest = 70,
                            }.Center()),
                            Demo("CornerRadius(24, 0, 0, 24)", new SkiaShape
                            {
                                Type = ShapeType.Rectangle,
                                CornerRadius = new CornerRadius(24, 0, 0, 24),
                                BackgroundColor = Color.Parse("#20C997"),
                                WidthRequest = 110,
                                HeightRequest = 70,
                            }.Center()),
                            Demo("Circle + stroke", new SkiaShape
                            {
                                Type = ShapeType.Circle,
                                BackgroundColor = Color.Parse("#6610F2"),
                                StrokeColor = Color.Parse("#FFD93D"),
                                StrokeWidth = 4,
                                WidthRequest = 80,
                                LockRatio = 1,
                            }.Center()),
                            Demo("Ellipse", new SkiaShape
                            {
                                Type = ShapeType.Ellipse,
                                BackgroundColor = Color.Parse("#DC3545"),
                                WidthRequest = 120,
                                HeightRequest = 70,
                            }.Center()),
                            Demo("Arc Value1=-90 Value2=270", new SkiaShape
                            {
                                Type = ShapeType.Arc,
                                Value1 = -90,
                                Value2 = 270,
                                StrokeColor = Color.Parse("#0DCAF0"),
                                StrokeWidth = 8,
                                WidthRequest = 80,
                                LockRatio = 1,
                            }.Center()),
                            Demo("Polygon (star, Points)", new SkiaShape
                            {
                                Type = ShapeType.Polygon,
                                Points = Star(),
                                BackgroundColor = Color.Parse("#FFC107"),
                                WidthRequest = 90,
                                LockRatio = 1,
                            }.Center()),
                            Demo("Line (Points)", new SkiaShape
                            {
                                Type = ShapeType.Line,
                                Points = new List<SkiaPoint>
                                {
                                    new(0, 1), new(0.33, 0.2), new(0.66, 0.8), new(1, 0),
                                },
                                StrokeColor = Color.Parse("#FD7E14"),
                                StrokeWidth = 4,
                                WidthRequest = 120,
                                HeightRequest = 70,
                            }.Center()),
                            Demo("Path (SVG PathData)", new SkiaShape
                            {
                                Type = ShapeType.Path,
                                PathData = Heart,
                                BackgroundColor = Color.Parse("#E83E8C"),
                                WidthRequest = 80,
                                LockRatio = 1,
                            }.Center()),
                            Demo("Hollow: ClipBackgroundColor", new SkiaShape
                            {
                                Type = ShapeType.Rectangle,
                                CornerRadius = 12,
                                ClipBackgroundColor = true,
                                BackgroundColor = Color.Parse("#0D6EFD"),
                                StrokeColor = Color.Parse("#0D6EFD"),
                                StrokeWidth = 3,
                                WidthRequest = 110,
                                HeightRequest = 70,
                            }.Center()),
                            Demo("Children clipped", new SkiaShape
                            {
                                Type = ShapeType.Circle,
                                BackgroundColor = Color.Parse("#1F2937"),
                                StrokeColor = Color.Parse("#67E8F9"),
                                StrokeWidth = 3,
                                WidthRequest = 80,
                                LockRatio = 1,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaShape
                                    {
                                        Type = ShapeType.Rectangle,
                                        BackgroundColor = Color.Parse("#67E8F9"),
                                        WidthRequest = 100,
                                        HeightRequest = 24,
                                        HorizontalOptions = LayoutOptions.Center,
                                        VerticalOptions = LayoutOptions.End,
                                    },
                                    new SkiaLabel("AB")
                                    {
                                        FontSize = 22,
                                        FontFamily = "FontTextBold",
                                        TextColor = Colors.White,
                                        HorizontalOptions = LayoutOptions.Center,
                                        VerticalOptions = LayoutOptions.Center,
                                        Margin = new Thickness(0, 0, 0, 10),
                                    },
                                },
                            }.Center()),
                            Demo("StrokeCap Butt, thin", new SkiaShape
                            {
                                Type = ShapeType.Line,
                                StrokeCap = SKStrokeCap.Butt,
                                Points = new List<SkiaPoint> { new(0, 0.5), new(1, 0.5) },
                                StrokeColor = Colors.White,
                                StrokeWidth = 1,
                                WidthRequest = 120,
                                HeightRequest = 40,
                            }.Center()),
                            Demo("Gradient fill", new SkiaShape
                            {
                                Type = ShapeType.Rectangle,
                                CornerRadius = 35,
                                WidthRequest = 120,
                                HeightRequest = 70,
                                FillGradient = Gradient(GradientType.Linear,
                                    new[] { "#FF6B6B", "#FFD93D", "#4ECDC4" }, endX: 1, endY: 0),
                            }.Center()),
                        }),
                    },
                },
            }.Fill(),
        };
    }

    private static SkiaLabel Title(string text, double size) => new(text)
    {
        FontSize = size,
        TextColor = Colors.White,
        HorizontalOptions = LayoutOptions.Center,
        Margin = new Thickness(0, 8, 0, 0),
    };

    private static SkiaLabel Caption(string text) => new(text)
    {
        FontSize = 13,
        TextColor = Colors.LightGray,
        HorizontalOptions = LayoutOptions.Center,
    };

    private static SkiaControl Row(List<SkiaControl> children) => new SkiaWrap
    {
        Spacing = 16,
        HorizontalOptions = LayoutOptions.Center,
        MaximumWidthRequest = 680,
        Children = children,
    };

    /// <summary>One labelled swatch: the demo shape centred on a card.</summary>
    private static SkiaControl Demo(string title, SkiaControl content) => new SkiaStack
    {
        Spacing = 8,
        WidthRequest = 150,
        Children = new List<SkiaControl>
        {
            new SkiaShape
            {
                WidthRequest = 150,
                HeightRequest = 110,
                BackgroundColor = Color.Parse("#2B3035"),
                CornerRadius = 8,
                Children = new List<SkiaControl> { content },
            },
            new SkiaLabel(title)
            {
                FontSize = 13,
                TextColor = Color.Parse("#ADB5BD"),
                HorizontalOptions = LayoutOptions.Center,
            },
        },
    };

    private static SkiaShape Beveled(BevelType type, double depth) => new SkiaShape
    {
        Type = ShapeType.Rectangle,
        CornerRadius = 12,
        BackgroundColor = Color.Parse("#495057"),
        WidthRequest = 110,
        HeightRequest = 70,
        BevelType = type,
        Bevel = new SkiaBevel { Depth = depth },
    }.Center();

    private static List<SkiaPoint> Star()
    {
        var points = new List<SkiaPoint>();
        for (var i = 0; i < StarPoints.Length; i += 2)
            points.Add(new SkiaPoint(StarPoints[i], StarPoints[i + 1]));

        return points;
    }

    private static SkiaGradient Gradient(
        GradientType type,
        string[] colors,
        double angle = 0,
        float startX = 0,
        float startY = 0,
        float endX = 0,
        float endY = 1,
        float light = 0,
        List<double> positions = null)
    {
        var gradient = new SkiaGradient
        {
            Type = type,
            StartXRatio = startX,
            StartYRatio = startY,
            EndXRatio = endX,
            EndYRatio = endY,
            Colors = colors.Select(Color.Parse).ToList(),
        };

        if (angle != 0)
            gradient.Angle = angle;

        if (light != 0)
            gradient.Light = light;

        if (positions != null)
            gradient.ColorPositions = positions;

        return gradient;
    }
}
