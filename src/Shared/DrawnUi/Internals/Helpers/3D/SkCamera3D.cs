using System.Numerics;

namespace DrawnUi.Draw;

public class SkCamera3D
{
    public SkCamera3D()
    {
        Reset();
    }

    public Vector3 fLocation;   // origin of the camera's space
    public Vector3 fAxis;       // view direction
    public Vector3 fZenith;     // up direction
    public Vector3 fObserver;   // eye position (may not be the same as the origin)

    bool fNeedToUpdate;
    SKMatrix fOrientation;

    private Matrix4x4 transformationMatrix;

    protected void DoUpdate()
    {
        Vector3 axis, zenith, cross;

        // construct a orthonormal basis of cross (x), zenith (y), and axis (z)
        axis = Vector3.Normalize(fAxis);

        zenith = fZenith - (axis * fZenith) * axis;
        zenith = Vector3.Normalize(zenith);
        cross = Vector3.Cross(axis, zenith);

        {
            //SkMatrix* orien = &fOrientation;
            var x = fObserver.X;
            var y = fObserver.Y;
            var z = fObserver.Z;

            // Looking along the view axis we have:
            //
            //   /|\ zenith
            //    |
            //    |
            //    |  * observer (projected on XY plane)
            //    |
            //    |____________\ cross
            //                 /
            //
            // So this does a z-shear along the view axis based on the observer's x and y values,
            // and scales in x and y relative to the negative of the observer's z value
            // (the observer is in the negative z direction).

            fOrientation.ScaleX = x * axis.X - z * cross.X;
            fOrientation.SkewX = x * axis.Y - z * cross.Y;
            fOrientation.TransX = x * axis.Z - z * cross.Z;
            fOrientation.SkewY = y * axis.X - z * zenith.X;
            fOrientation.ScaleY = y * axis.Y - z * zenith.Y;
            fOrientation.TransY = y * axis.Z - z * zenith.Z;
            fOrientation.Persp0 = axis.X;
            fOrientation.Persp1 = axis.Y;
            fOrientation.Persp2 = axis.Z;
        }

        fNeedToUpdate = false;
    }

    public void Update()
    {
        fNeedToUpdate = true;
    }



    public void Reset()
    {
        transformationMatrix = Matrix4x4.Identity;

        fLocation = new(0, 0, -576);   // 8 inches backward
        fAxis = new(0, 0, 1);                // forward
        fZenith = new(0, -1, 0);             // up
        fObserver = new(0, 0, fLocation.Z);
        fNeedToUpdate = true;
    }

    /*
     *     public SKMatrix PatchToMatrix(SkPatch3D quilt)
    {
        if (fNeedToUpdate)
        {
            DoUpdate();
        }

        //const float* mapPtr = (const float*)(const void*)&fOrientation;
        //const float* patchPtr;

        Vector3 diff = quilt.fOrigin - fLocation;

        float dot = diff.Dot(quilt { mapPtr[6], mapPtr[7], mapPtr[8]});

        // This multiplies fOrientation by the matrix [quilt.fU quilt.fV diff] -- U, V, and diff are
        // column vectors in the matrix -- then divides by the length of the projection of diff onto
        // the view axis (which is 'dot'). This transforms the patch (which transforms from local path
        // space to world space) into view space (since fOrientation transforms from world space to
        // view space).
        //
        // The divide by 'dot' isn't strictly necessary as the homogeneous divide would do much the
        // same thing (it's just scaling the entire matrix by 1/dot). It looks like it's normalizing
        // the matrix into some canonical space.
        patchPtr = (const float*)&quilt;
        matrix->set(SkMatrix::kMScaleX, SkScalarDotDiv(3, patchPtr, 1, mapPtr, 1, dot));
        matrix->set(SkMatrix::kMSkewY, SkScalarDotDiv(3, patchPtr, 1, mapPtr + 3, 1, dot));
        matrix->set(SkMatrix::kMPersp0, SkScalarDotDiv(3, patchPtr, 1, mapPtr + 6, 1, dot));

        patchPtr += 3;
        matrix->set(SkMatrix::kMSkewX, SkScalarDotDiv(3, patchPtr, 1, mapPtr, 1, dot));
        matrix->set(SkMatrix::kMScaleY, SkScalarDotDiv(3, patchPtr, 1, mapPtr + 3, 1, dot));
        matrix->set(SkMatrix::kMPersp1, SkScalarDotDiv(3, patchPtr, 1, mapPtr + 6, 1, dot));

        patchPtr = (const float*)(const void*)&diff;
        matrix->set(SkMatrix::kMTransX, SkScalarDotDiv(3, patchPtr, 1, mapPtr, 1, dot));
        matrix->set(SkMatrix::kMTransY, SkScalarDotDiv(3, patchPtr, 1, mapPtr + 3, 1, dot));
        matrix->set(SkMatrix::kMPersp2, SK_Scalar1);
    }

     */

    public SKMatrix PatchToMatrix(SkPatch3D quilt)
    {
        if (fNeedToUpdate)
        {
            DoUpdate();
        }

        Vector3 diff = quilt.fOrigin - fLocation;
        float dot = Vector3.Dot(diff, new Vector3(fOrientation.Persp0, fOrientation.Persp1, fOrientation.Persp2));

        float[] patchData = new float[] { quilt.fU.X, quilt.fU.Y, quilt.fU.Z, quilt.fV.X, quilt.fV.Y, quilt.fV.Z, diff.X, diff.Y, diff.Z };

        SKMatrix matrix = new SKMatrix();
        matrix.ScaleX = SkScalarDotDiv(3, patchData, 0, fOrientation, 0, dot);
        matrix.SkewY = SkScalarDotDiv(3, patchData, 0, fOrientation, 3, dot);
        matrix.Persp0 = SkScalarDotDiv(3, patchData, 0, fOrientation, 6, dot);

        matrix.SkewX = SkScalarDotDiv(3, patchData, 3, fOrientation, 0, dot);
        matrix.ScaleY = SkScalarDotDiv(3, patchData, 3, fOrientation, 3, dot);
        matrix.Persp1 = SkScalarDotDiv(3, patchData, 3, fOrientation, 6, dot);

        matrix.TransX = SkScalarDotDiv(3, new float[] { diff.X, diff.Y, diff.Z }, 0, fOrientation, 0, dot);
        matrix.TransY = SkScalarDotDiv(3, new float[] { diff.X, diff.Y, diff.Z }, 0, fOrientation, 3, dot);
        matrix.Persp2 = 1;

        return matrix;
    }

    private float SkScalarDotDiv(int count, float[] a, int aOffset, SKMatrix m, int mOffset, float denom)
    {
        float prod = 0;
        for (int i = 0; i < count; i++)
        {
            prod += a[aOffset + i] * GetMatrixValue(m, mOffset + i);
        }
        return prod / denom;
    }

    private float GetMatrixValue(SKMatrix matrix, int index)
    {
        return index switch
        {
            0 => matrix.ScaleX,
            1 => matrix.SkewX,
            2 => matrix.TransX,
            3 => matrix.SkewY,
            4 => matrix.ScaleY,
            5 => matrix.TransY,
            6 => matrix.Persp0,
            7 => matrix.Persp1,
            8 => matrix.Persp2,
            _ => throw new IndexOutOfRangeException("Invalid matrix index"),
        };
    }
}

public class SkCamera3D2
{
    public Vector3 Position;   // Camera position in world space
    public Vector3 Forward;    // Forward direction vector
    public Vector3 Up;         // Up direction vector

    private bool needToUpdate;
    private SKMatrix44 viewMatrix;

    public SkCamera3D2()
    {
        Reset();
    }

    public void Reset()
    {
        Position = new Vector3(0, 0, -10);  // Position the camera back along the Z-axis
        Forward = new Vector3(0, 0, 1);     // Looking towards positive Z
        Up = new Vector3(0, 1, 0);          // Up is positive Y
        needToUpdate = true;
        viewMatrix = SKMatrix44.CreateIdentity();
    }

    public void RotateXDegrees(float degrees)
    {
        Rotate(degrees, 0, 0);
    }

    public void RotateYDegrees(float degrees)
    {
        Rotate(0, degrees, 0);
    }

    public void RotateZDegrees(float degrees)
    {
        Rotate(0, 0, degrees);
    }

    private void Rotate(float degreesX, float degreesY, float degreesZ)
    {
        // Convert degrees to radians
        float radiansX = SkiaControl.DegreesToRadians(degreesX);
        float radiansY = SkiaControl.DegreesToRadians(degreesY);
        float radiansZ = SkiaControl.DegreesToRadians(degreesZ);

        // Create rotation quaternions
        var rotationX = Quaternion.CreateFromAxisAngle(Vector3.UnitX, radiansX);
        var rotationY = Quaternion.CreateFromAxisAngle(Vector3.UnitY, radiansY);
        var rotationZ = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, radiansZ);

        // Combine rotations (order matters)
        var combinedRotation = rotationZ * rotationY * rotationX;

        // Rotate the camera's forward and up vectors
        Forward = Vector3.Transform(Forward, combinedRotation);
        Up = Vector3.Transform(Up, combinedRotation);

        needToUpdate = true;
    }

    public void Update()
    {
        if (needToUpdate)
        {
            DoUpdate();
        }
    }

    private void DoUpdate()
    {
        // Calculate the view matrix using the camera's position, target, and up vector
        Vector3 target = Position + Forward;
        var viewMatrix4x4 = Matrix4x4.CreateLookAt(Position, target, Up);

        // Convert Matrix4x4 to SKMatrix44
        viewMatrix = new SKMatrix44(
            viewMatrix4x4.M11, viewMatrix4x4.M12, viewMatrix4x4.M13, viewMatrix4x4.M14,
            viewMatrix4x4.M21, viewMatrix4x4.M22, viewMatrix4x4.M23, viewMatrix4x4.M24,
            viewMatrix4x4.M31, viewMatrix4x4.M32, viewMatrix4x4.M33, viewMatrix4x4.M34,
            viewMatrix4x4.M41, viewMatrix4x4.M42, viewMatrix4x4.M43, viewMatrix4x4.M44);

        needToUpdate = false;
    }

    public SKMatrix44 GetViewMatrix44()
    {
        Update();
        return viewMatrix;
    }
}


