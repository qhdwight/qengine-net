using System.Runtime.CompilerServices;
using Silk.NET.Maths;

namespace Game.Voxels;

using Vector3Int = Vector3D<int>;

public record struct BoundingBox
{
    public Vector3Int Center { get; set; }
    public Vector3Int Extents { get; set; }

    public Vector3Int Size
    {
        get => Extents * 2;
        set => Extents = value / 2;
    }

    public Vector3Int Min
    {
        get => Center - Extents;
        set => SetMinMax(value, Max);
    }

    public Vector3Int Max
    {
        get => Center + Extents;
        set => SetMinMax(Min, value);
    }

    public BoundingBox(in Vector3Int center, in Vector3Int size)
    {
        Center = center;
        Extents = size / 2;
    }

    public void SetMinMax(in Vector3Int min, in Vector3Int max)
    {
        Extents = (max - min) / 2;
        Center = min + Extents;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Encapsulate(in Vector3Int point)
        => SetMinMax(Vector3D.Min(Min, point), Vector3D.Max(Max, point));

    public void Encapsulate(in BoundingBox box)
    {
        Encapsulate(box.Center - box.Extents);
        Encapsulate(box.Center + box.Extents);
    }

    public void Expand(int amount)
    {
        amount /= 2;
        Extents += new Vector3Int(amount, amount, amount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Expand(in Vector3Int amount) => Extents += amount / 2;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(Vector3Int point) => Min.X <= point.X && Max.X >= point.X &&
                                              Min.Y <= point.Y && Max.Y >= point.Y &&
                                              Min.Z <= point.Z && Max.Z >= point.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(BoundingBox box) => Min.X <= box.Max.X && Max.X >= box.Min.X &&
                                               Min.Y <= box.Max.Y && Max.Y >= box.Min.Y &&
                                               Min.Z <= box.Max.Z && Max.Z >= box.Min.Z;

    // public bool IntersectRay(Ray ray)
    // {
    //     float distance;
    //     return IntersectRay(ray, out distance);
    // }

    // public bool IntersectRay(Ray ray, out float distance)
    // {
    //     var dirFrac = new Vector3Int(1f / ray.Direction.X,
    //                                     1f / ray.Direction.Y,
    //                                     1f / ray.Direction.Z);
    //
    //     float t1 = (Min.X - ray.Origin.X) * dirFrac.X;
    //     float t2 = (Max.X - ray.Origin.X) * dirFrac.X;
    //     float t3 = (Min.Y - ray.Origin.Y) * dirFrac.Y;
    //     float t4 = (Max.Y - ray.Origin.Y) * dirFrac.Y;
    //     float t5 = (Min.Z - ray.Origin.Z) * dirFrac.Z;
    //     float t6 = (Max.Z - ray.Origin.Z) * dirFrac.Z;
    //
    //     float tmin = Math.Max(Math.Max(Math.Min(t1, t2), Math.Min(t3, t4)), Math.Min(t5, t6));
    //     float tmax = Math.Min(Math.Min(Math.Max(t1, t2), Math.Max(t3, t4)), Math.Max(t5, t6));
    //
    //     // if tmax < 0, ray (line) is intersecting AABB, but the whole AABB is behind us
    //     if (tmax < 0)
    //     {
    //         distance = tmax;
    //         return false;
    //     }
    //
    //     // if tmin > tmax, ray doesn't intersect AABB
    //     if (tmin > tmax)
    //     {
    //         distance = tmax;
    //         return false;
    //     }
    //
    //     distance = tmin;
    //     return true;
    // }
}