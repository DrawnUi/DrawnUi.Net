namespace DrawnUi.Draw;

using SkiaSharp;
using System;
using System.Collections.Generic;

/// <summary>
/// Custom implementation of Android's Camera 3D helper for SkiaSharp
/// </summary>
public class Sk3dView
{
    private float _rotationX = 0;
    private float _rotationY = 0;
    private float _rotationZ = 0;
    private float _translationX = 0;
    private float _translationY = 0;
    private float _translationZ = 0;
    private SKMatrix _baseMatrix = SKMatrix.Identity;

    public Sk3dView()
    {
        Reset();
    }

    protected bool Invalidated = true;
    private SKMatrix _matrix;

    private readonly Stack<SKMatrix> _states = new Stack<SKMatrix>();
    private readonly Stack<TransformState> _transformStates = new Stack<TransformState>();

    /// <summary>
    /// 3D camera distance (8 inches in pixels, similar to Android implementation)
    /// </summary>
    public float CameraDistance = 576;

    /// <summary>
    /// Saves the current transformation state
    /// </summary>
    public void Save()
    {
        _states.Push(_baseMatrix);
        _transformStates.Push(new TransformState
        {
            RotationX = _rotationX,
            RotationY = _rotationY,
            RotationZ = _rotationZ,
            TranslationX = _translationX,
            TranslationY = _translationY,
            TranslationZ = _translationZ
        });
    }

    /// <summary>
    /// Restores the previously saved transformation state
    /// </summary>
    public void Restore()
    {
        if (_states.Count > 0 && _transformStates.Count > 0)
        {
            _baseMatrix = _states.Pop();
            var state = _transformStates.Pop();

            _rotationX = state.RotationX;
            _rotationY = state.RotationY;
            _rotationZ = state.RotationZ;
            _translationX = state.TranslationX;
            _translationY = state.TranslationY;
            _translationZ = state.TranslationZ;

            Invalidated = true;
            OnRestore();
        }
    }

    /// <summary>
    /// Applies the current 3D transformation to the canvas
    /// </summary>
    public void ApplyToCanvas(SKCanvas canvas)
    {
        if (canvas == null)
            throw new ArgumentNullException(nameof(canvas));

        canvas.Concat(Matrix);
    }

    /// <summary>
    /// Resets the current state and clears all saved states
    /// </summary>
    public void Reset()
    {
        _states.Clear();
        _transformStates.Clear();

        _baseMatrix = SKMatrix.Identity;

        _rotationX = 0;
        _rotationY = 0;
        _rotationZ = 0;
        _translationX = 0;
        _translationY = 0;
        _translationZ = 0;

        Invalidated = true;
        OnReset();
    }

    protected virtual void OnReset() { }

    protected virtual void OnRestore() { }

    /// <summary>
    /// Rotates around the X axis
    /// </summary>
    public void RotateXDegrees(float degrees)
    {
        _rotationX += degrees;
        Invalidated = true;
    }

    /// <summary>
    /// Rotates around the Y axis
    /// </summary>
    public void RotateYDegrees(float degrees)
    {
        _rotationY += degrees;
        Invalidated = true;
    }

    /// <summary>
    /// Rotates around the Z axis
    /// </summary>
    public void RotateZDegrees(float degrees)
    {
        _rotationZ += degrees;
        Invalidated = true;
    }

    /// <summary>
    /// Translates along the X axis
    /// </summary>
    public void TranslateX(float value)
    {
        _translationX += value;
        Invalidated = true;
    }

    /// <summary>
    /// Translates along the Y axis
    /// </summary>
    public void TranslateY(float value)
    {
        _translationY += value;
        Invalidated = true;
    }

    /// <summary>
    /// Translates along the Z axis
    /// </summary>
    public void TranslateZ(float value)
    {
        _translationZ += value;
        Invalidated = true;
    }

    /// <summary>
    /// Translates along all axes
    /// </summary>
    public void Translate(float x, float y, float z)
    {
        _translationX += x;
        _translationY += y;
        _translationZ += z;
        Invalidated = true;
    }

    /// <summary>
    /// Gets the current transformation matrix
    /// </summary>
    public SKMatrix Matrix
    {
        get
        {
            if (Invalidated)
            {
                _matrix = CreateProjectionMatrix();
                Invalidated = false;
            }
            return _matrix;
        }
    }

    /// <summary>
    /// Creates a 3D projection matrix based on current rotations and translations
    /// </summary>
    private SKMatrix CreateProjectionMatrix()
    {
        // Convert degrees to radians
        float radX = DegreesToRadians(_rotationX);
        float radY = DegreesToRadians(_rotationY);
        float radZ = DegreesToRadians(_rotationZ);

        // Compute sine and cosine of rotation angles
        float sinX = MathF.Sin(radX);
        float cosX = MathF.Cos(radX);
        float sinY = MathF.Sin(radY);
        float cosY = MathF.Cos(radY);
        float sinZ = MathF.Sin(radZ);
        float cosZ = MathF.Cos(radZ);

        // Compute the rotation matrix elements (3x3 rotation matrix)
        float m00 = cosY * cosZ;
        float m01 = cosY * sinZ;
        float m02 = -sinY;

        float m10 = sinX * sinY * cosZ - cosX * sinZ;
        float m11 = sinX * sinY * sinZ + cosX * cosZ;
        float m12 = sinX * cosY;

        float m20 = cosX * sinY * cosZ + sinX * sinZ;
        float m21 = cosX * sinY * sinZ - sinX * cosZ;
        float m22 = cosX * cosY;

        // Apply translations (camera is at origin, objects move)
        float tx = _translationX;
        float ty = _translationY;
        float tz = _translationZ;

        // Calculate the perspective division factor based on camera distance
        float Z = CameraDistance - tz;
        if (Z < 1.0f) Z = 1.0f; // Prevent division by near-zero values

        // Calculate the perspective transformation matrix
        SKMatrix perspectiveMatrix = new SKMatrix
        {
            ScaleX = m00,
            SkewX = m01,
            TransX = m02 * tx + m00 * tx + m01 * ty,

            SkewY = m10,
            ScaleY = m11,
            TransY = m12 * ty + m10 * tx + m11 * ty,

            Persp0 = m20 / Z,
            Persp1 = m21 / Z,
            Persp2 = 1.0f + m22 / Z
        };

        // Combine with the base matrix (restored from Save/Restore operations)
        return perspectiveMatrix.PreConcat(_baseMatrix);
    }

    /// <summary>
    /// Helper method to convert degrees to radians
    /// </summary>
    private static float DegreesToRadians(float degrees)
    {
        return degrees * MathF.PI / 180.0f;
    }

    /// <summary>
    /// Structure to store transformation state for Save/Restore operations
    /// </summary>
    private struct TransformState
    {
        public float RotationX;
        public float RotationY;
        public float RotationZ;
        public float TranslationX;
        public float TranslationY;
        public float TranslationZ;
    }
}
