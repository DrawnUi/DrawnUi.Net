namespace Add3dGallery;

/// <summary>The 3D pieces made in code: smooth shapes in flat colors, drawn with an outline.</summary>
public partial class GalleryPage
{
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

    /// <summary>A strip of paper for the confetti; the part to recolor is "piece".</summary>
    static Model3D Paper() => new Model3D().Part("piece", Mesh3D.Cuboid(1, 0.45f, 0.06f), Colors.White);

    static Model3D ConfettiStar() => new Model3D().Part("piece", Mesh3D.Extrude(Star(5, 0.6f, 0.26f), 0.12f), Colors.White);
}
