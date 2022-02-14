using System;
using Silk.NET.Maths;

namespace Game.Voxels;

[Flags]
public enum VoxelFlags
{
    None = 0,
    IsBlock = 1,
    IsGround = 2
}

public record struct Voxel(VoxelFlags Flags, byte Density, Vector4D<byte> Color);