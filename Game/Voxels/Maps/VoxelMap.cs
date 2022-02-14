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
        if (GetChunkFromWorldPosition(position, out chunk))
            return chunk!.TryGet(position, out voxel);
        
        voxel = default;
        return false;
    }

    /// <summary>
    /// Given a world position, determine if a chunk is there.
    /// </summary>
    /// <param name="position">World position of chunk</param>
    /// <param name="containerChunk">Container chunk</param>
    /// <returns>Chunk instance, or null if it doe not exist</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetChunkFromWorldPosition(in Vector3Int position, out VoxelChunk? containerChunk)
        => Chunks.TryGetValue(WorldToChunk(position), out containerChunk);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetChunkFromPosition(in Vector3Int chunkPosition, out VoxelChunk? containerChunk)
        => Chunks.TryGetValue(chunkPosition, out containerChunk);

    /// <summary>
    /// Given a world position, return the position of the chunk that would contain it.
    /// </summary>
    /// <param name="worldPosition">World position inside of chunk</param>
    /// <returns>Position of chunk in respect to chunks dictionary</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector3Int WorldToChunk(in Vector3 worldPosition)
        => new((int)Scalar.Floor(worldPosition.X / ChunkSize),
               (int)Scalar.Floor(worldPosition.Y / ChunkSize),
               (int)Scalar.Floor(worldPosition.Z / ChunkSize));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector3Int WorldToChunk(in Vector3Int worldPosition)
        => new(worldPosition.X / ChunkSize, worldPosition.Y / ChunkSize, worldPosition.Z / ChunkSize);
}