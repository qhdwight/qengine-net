using System.Runtime.CompilerServices;
using Game.Voxels.Collections;
using Silk.NET.Maths;

namespace Game.Voxels.Maps;

using Vector3Int = Vector3D<int>;

public class Chunk : Octree<Voxel>
{
    public Vector3D<int> Position { get; private set; }

    public Chunk(in Vector3Int position) : base(MapManager.ChunkSize, position, 4) { Position = position; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool InsideChunk(in Vector3Int pos) => pos.X < MapManager.ChunkSize && pos.Y < MapManager.ChunkSize && pos.Z < MapManager.ChunkSize
                                               && pos.X >= 0 && pos.Y >= 0 && pos.Z >= 0;
}