using System.Numerics;

namespace DrawnUi.Draw;

public class SkPatch3D
{
    public Vector3 fU, fV;
    public Vector3 fOrigin;

    public SkPatch3D()
    {
        Reset();
    }

    void Reset()
    {
        fOrigin = new(0, 0, 0);
        fU = new(1, 0, 0);
        fV = new(0, -1, 0);
    }

    public float DotWith(float dx, float dy, float dz)
    {
        float cx = fU.Y * fV.Z - fU.Z * fV.Y;
        float cy = fU.Z * fV.X - fU.X * fV.Y;
        float cz = fU.X * fV.Y - fU.Y * fV.X;

        return cx * dx + cy * dy + cz * dz;
    }

    //public void Transform(SKMatrix44 m)
    //{
    //    fU = m * fU;
    //    fV = m * fV;
    //    var auto = m.MapPoint(fOrigin.X, fOrigin.Y, fOrigin.Z);
    //    fOrigin = new Vector3(auto.X, auto.Y, auto.Z);
    //}

    public void Transform(Matrix4x4 m)
    {
        fU = Vector3.Transform(fU, m);
        fV = Vector3.Transform(fV, m);
        SKMatrix44 skia = m;
        var auto = skia.MapPoint(fOrigin.X, fOrigin.Y, fOrigin.Z);
        fOrigin = new Vector3(auto.X, auto.Y, auto.Z);
    }

    // deprecated, but still here for animator (for now)
    void rotate(float x, float y, float z) { }
    void rotateDegrees(float x, float y, float z) { }
}


