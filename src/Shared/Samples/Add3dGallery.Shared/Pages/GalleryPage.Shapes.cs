namespace Add3dGallery;

/// <summary>The 3D pieces made in code: smooth shapes in flat colors, drawn with an outline.</summary>
public partial class GalleryPage
{
    static readonly Color Gold = Color.FromRgb(255, 196, 61);
    static readonly Color GoldLight = Color.FromRgb(255, 222, 122);
    static readonly Color Grey = Color.FromRgb(196, 192, 206);

    static readonly Color[] ConfettiColors =
    {
        Color.FromRgb(255, 99, 132),
        Color.FromRgb(255, 196, 61),
        Color.FromRgb(76, 201, 140),
        Color.FromRgb(92, 140, 255),
        Color.FromRgb(178, 102, 255),
    };

    static SKPath Star(int points, float outer, float inner)
    {
        var path = new SKPath();
        for (var i = 0; i < points * 2; i++)
        {
            var r = i % 2 == 0 ? outer : inner;
            var a = i * MathF.Tau / (points * 2) - MathF.Tau / 4;
            if (i == 0)
                path.MoveTo(r * MathF.Cos(a), r * MathF.Sin(a));
            else
                path.LineTo(r * MathF.Cos(a), r * MathF.Sin(a));
        }
        path.Close();
        return path;
    }

    static SKPath Heart()
    {
        var path = new SKPath();
        path.MoveTo(50, 88);
        path.CubicTo(20, 65, 0, 45, 0, 28);
        path.CubicTo(0, 12, 12, 0, 27, 0);
        path.CubicTo(38, 0, 46, 7, 50, 15);
        path.CubicTo(54, 7, 62, 0, 73, 0);
        path.CubicTo(88, 0, 100, 12, 100, 28);
        path.CubicTo(100, 45, 80, 65, 50, 88);
        path.Close();
        return path;
    }

    /// <summary>A medal on two ribbons.</summary>
    static Model3D Medal() => new Model3D()
        .Part("ribbon_left", Mesh3D.Cuboid(0.55f, 1.5f, 0.06f), Color.FromRgb(255, 99, 132)).At(-0.38f, 1.05f, -0.12f).Turned(0, 0, 22)
        .Part("ribbon_right", Mesh3D.Cuboid(0.55f, 1.5f, 0.06f), Color.FromRgb(92, 140, 255)).At(0.38f, 1.05f, -0.12f).Turned(0, 0, -22)
        .Part("disc", Mesh3D.Cylinder(1, 0.22f), Gold).Turned(90, 0, 0)
        .Part("inner", Mesh3D.Cylinder(0.76f, 0.34f), GoldLight).Turned(90, 0, 0)
        .Part("star", Mesh3D.Extrude(Star(5, 0.52f, 0.22f), 0.08f), Colors.White).At(0, 0, 0.2f);

    static Model3D HeartModel() => new Model3D().Part("heart", Mesh3D.Extrude(Heart(), 22), Color.FromRgb(255, 77, 109));

    /// <summary>A strip of paper for the confetti; the part to recolor is "piece".</summary>
    static Model3D Paper() => new Model3D().Part("piece", Mesh3D.Cuboid(1, 0.45f, 0.06f), Colors.White);

    static Model3D ConfettiStar() => new Model3D().Part("piece", Mesh3D.Extrude(Star(5, 0.6f, 0.26f), 0.12f), Colors.White);
}
