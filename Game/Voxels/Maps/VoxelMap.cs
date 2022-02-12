using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Silk.NET.Maths;

namespace Game.Voxels.Maps;

using Vector3 = Vector3D<float>;
using Vector3Int = Vector3D<int>;

public class VoxelMap
{
    public static int ChunkSize => 32;

    public Dictionary<Vector3Int, VoxelChunk> Chunks { get; } = new();

    public VoxelMap()
    {
        var chunk = new VoxelChunk(Vector3Int.Zero);
        Chunks[Vector3Int.Zero] = chunk;
        // chunk.Add(new Voxel(VoxelFlags.IsBlock, 0, Vector4D<byte>.One), Vector3Int.Zero);
    }

    public bool TryGetVoxel(in Vector3Int position, out Voxel voxel, VoxelChunk? chunk = null)
    {
        chunk ??= GetChunkFromWorldPosition(position);
        if (chunk is null)
        {
            voxel = default;
            return false;
        }
        return chunk.TryGet(position, out voxel);
    }

    /// <summary>
    /// Given a world position, determine if a chunk is there.
    /// </summary>
    /// <param name="position">World position of chunk</param>
    /// <returns>Chunk instance, or null if it doe not exist</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VoxelChunk? GetChunkFromWorldPosition(in Vector3Int position)
    {
        Vector3Int chunkPosition = WorldToChunk((Vector3)position);
        return GetChunkFromPosition(chunkPosition);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VoxelChunk? GetChunkFromPosition(in Vector3Int chunkPosition)
    {
        Chunks.TryGetValue(chunkPosition, out VoxelChunk? containerChunk);
        return containerChunk;
    }

    /// <summary>
    /// Given a world position, return the position of the chunk that would contain it.
    /// </summary>
    /// <param name="worldPosition">World position inside of chunk</param>
    /// <returns>Position of chunk in respect to chunks dictionary</returns>
    private Vector3Int WorldToChunk(in Vector3 worldPosition)
    {
        float chunkSize = ChunkSize;
        return new Vector3Int((int)Scalar.Floor(worldPosition.X / chunkSize),
                              (int)Scalar.Floor(worldPosition.Y / chunkSize),
                              (int)Scalar.Floor(worldPosition.Z / chunkSize));
    }
}