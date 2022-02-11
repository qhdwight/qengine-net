using System;
using System.Diagnostics;
using Silk.NET.Maths;

namespace Game.Graphic;

public static class Graphics
{
    public static readonly Vector3D<double> Right = new() { X = 1.0f };
    public static readonly Vector3D<double> Forward = new() { Y = 1.0f };
    public static readonly Vector3D<double> Up = new() { Z = 1.0f };

    // public static Quaternion<double> FromEuler(in Vector3D<double> eulerAngles)
    //     => Quaternion<double>.CreateFromAxisAngle(Graphics.Forward, eulerAngles.Y)
    //      * Quaternion<double>.CreateFromAxisAngle(Graphics.Up, eulerAngles.Z)
    //      * Quaternion<double>.CreateFromAxisAngle(Graphics.Right, eulerAngles.X);

    // public static Vector4D<double> Hamilton(in Vector4D<double> a, in Vector4D<double> b)
    //     => new(a[0] * b[0] - b[1] * b[1] - a[2] * b[2] - a[3] * b[3],
    //            a[0] * b[1] + b[1] * b[0] + a[2] * b[3] - a[3] * b[2],
    //            a[0] * b[2] - b[1] * b[3] + a[2] * b[0] + a[3] * b[1],
    //            a[0] * b[3] + b[1] * b[2] - a[2] * b[1] + a[3] * b[0]);

    public static Vector3D<double> Rotate(in Vector3D<double> v, in Quaternion<double> q)
    {
        // Vector4D<double> inner = Hamilton(new Vector4D<double>(q.W, q.X, q.Y, q.Z),
        //                                   new Vector4D<double>(0.0, p.X, p.Y, p.Z));
        // Vector4D<double> rotated = Hamilton(inner,
        //                                     new Vector4D<double>(q.W, -q.X, -q.Y, -q.Z));
        // return new Vector3D<double>(rotated[1], rotated[2], rotated[3]);
        Debug.Assert(Math.Abs(q.LengthSquared() - 1.0) < 1e-6f);
        // var p = new Quaternion<double>(r, 0.0);
        // Quaternion<double> qinv = Quaternion<double>.Conjugate(q);
        // Quaternion<double> res = q * p * qinv;
        // return new Vector3D<double>(res.X, res.Y, res.Z);
        var u = new Vector3D<double>(q.X, q.Y, q.Z);
        double s = q.W;
        return 2.0 * Vector3D.Dot(u, v) * u
             + (s * s - Vector3D.Dot(u, u)) * v
             + 2.0 * s * Vector3D.Cross(u, v);
    }
}

public record struct DrawInfo(Vector3D<double> Position, Vector3D<double> EulerOrientation)
{
    public Vector3D<double> Forward
    {
        get
        {
            // Quaternion<double> rotation = Quaternion<double>.CreateFromYawPitchRoll(EulerOrientation.Z, EulerOrientation.X, EulerOrientation.Y);
            Quaternion<double> rotation = Quaternion<double>.CreateFromAxisAngle(Graphics.Forward, EulerOrientation.Y)
                                        * Quaternion<double>.CreateFromAxisAngle(Graphics.Up, EulerOrientation.Z)
                                        * Quaternion<double>.CreateFromAxisAngle(Graphics.Right, EulerOrientation.X);
            return Graphics.Rotate(Graphics.Forward, rotation);
        }
    }
}