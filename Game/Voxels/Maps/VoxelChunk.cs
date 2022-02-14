using Game.Voxels.Collections;
using Silk.NET.Maths;

namespace Game.Voxels.Maps;

using Vector3Int = Vector3D<int>;

public class VoxelChunk : Octree<Voxel>
{
    public Vector3D<int> Position { get; private set; }

    public VoxelChunk(in Vector3Int position) : base(VoxelMap.ChunkSize, position, 1) { Position = position; }

    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public bool InsideChunk(in Vector3Int pos) => pos.X < MapManager.ChunkSize && pos.Y < MapManager.ChunkSize && pos.Z < MapManager.ChunkSize
    //                                            && pos.X >= 0 && pos.Y >= 0 && pos.Z >= 0;
}