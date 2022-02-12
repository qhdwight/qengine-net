using System;
using System.Diagnostics;
using Silk.NET.Maths;

namespace Game.Graphic;

public static class Graphics
{
    public static readonly Vector3D<double> Right = new() { X = 1.0f };
    public static readonly Vector3D<double> Forward = new() { Y = 1.0f };
    public static readonly Vector3D<double> Up = new() { Z = 1.0f };

    public static Vector3D<double> Rotate(this in Vector3D<double> v, in Quaternion<double> q)
    {
        Debug.Assert(Math.Abs(q.LengthSquared() - 1.0) < 1e-6f);
        var u = new Vector3D<double>(q.X, q.Y, q.Z);
        double s = q.W;
        return 2.0 * Vector3D.Dot(u, v) * u
             + (s * s - Vector3D.Dot(u, u)) * v
             + 2.0 * s * Vector3D.Cross(u, v);
    }

    public static Quaternion<double> FromEuler(this Vector3D<double> eulerOrientation)
        => Quaternion<double>.CreateFromAxisAngle(Forward, eulerOrientation.Y)
         * Quaternion<double>.CreateFromAxisAngle(Up, eulerOrientation.Z)
         * Quaternion<double>.CreateFromAxisAngle(Right, eulerOrientation.X);
}

public record struct DrawInfo(Vector3D<double> Position, Vector3D<double> EulerOrientation)
{
    public Vector3D<double> Forward
        => Graphics.Forward.Rotate(EulerOrientation.FromEuler());
}