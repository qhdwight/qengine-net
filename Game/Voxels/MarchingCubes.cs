using System;
using System.Runtime.CompilerServices;
using Game.ECS;
using Game.Graphic;
using Game.Graphic.Vulkan;
using Game.Voxels.Maps;
using Silk.NET.Maths;

namespace Game.Voxels;

using Vector3Int = Vector3D<int>;
using Vector3 = Vector3D<float>;

public partial class MarchingCubes : ISystem
{
    private const float IsoLevel = 0.5f;
    private const float Epsilon = 1e-5f;

    private static readonly Vector3 Offset = new(0.5f, 0.5f, 0.5f);

    private bool _hasAdded = false;

    public void Execute(World world)
    {
        if (!_hasAdded)
        {
            foreach (Entity entity in world.View<VkGraphics>())
            {
                ref VkGraphics graphics = ref world.GetComp<VkGraphics>(entity);
                if (graphics.vk == default) break;
                
                float[] heights = VulkanGraphics.Compute(ref graphics);
                foreach (Entity ent in world.View<VoxelMap>())
                {
                    var mapManager = world.GetComp<VoxelMap>(ent);
                    VoxelChunk c = mapManager.Chunks[Vector3Int.Zero];
                    for (var x = 0; x < 32; x++)
                    for (var y = 0; y < 32; y++)
                        c.Add(new Voxel(VoxelFlags.IsBlock, 0, Vector4D<byte>.One), new Vector3Int(x, y, 3 + (int)(heights[x + y * 32] * 2.0f)));
                }
                _hasAdded = true;
                break;
            }
        }

        foreach (Entity ent in world.View<Mesh, VoxelMap>())
        {
            var mesh = world.GetComp<Mesh>(ent);
            var mapManager = world.GetComp<VoxelMap>(ent);
            foreach (VoxelChunk chunk in mapManager.Chunks.Values)
            {
                RenderChunk(mapManager, chunk, mesh, default);
            }
        }
    }

    public static void RenderChunk(VoxelMap manager, VoxelChunk chunk, Mesh solidMesh, Mesh foliageMesh)
    {
        solidMesh.vertices.Clear();
        solidMesh.indices.Clear();
        Span<float> densities = stackalloc float[8];
        Span<Vector3> vertices = stackalloc Vector3[8];
        Span<Vector3> positions = stackalloc Vector3[8];
        foreach ((Voxel voxel, Vector3Int pos) in chunk.Iterate())
        {
            if ((voxel.Flags & VoxelFlags.IsBlock) != 0)
            {
                for (var orient = 0; orient < 6; orient++)
                {
                    bool isAdjacent = manager.TryGetVoxel(pos + AdjOffsets[orient], out Voxel adjVoxel);
                    if (!isAdjacent || ShouldRenderBlock(adjVoxel, orient))
                        GenerateBlock(voxel, pos, orient, solidMesh);
                }
            }
            else
            {
                var cubeIndex = 0;
                for (var orient = 0; orient < 8; orient++)
                {
                    Vector3Int internalPos = pos + Positions[orient];
                    // bool isOnWorldEdge = internalPosition.X == 0 && lowerBound.X == chunk.Position.X
                    //                   || internalPosition.Y == 0 && lowerBound.Y == chunk.Position.Y
                    //                   || internalPosition.Z == 0 && lowerBound.Z == chunk.Position.Z;
                    // var isOnWorldEdge = false;
                    // float density;
                    // if (isOnWorldEdge) density = 0.0f;
                    // else if (chunk.InsideChunk(internalPosition)) density = (float)chunk.TryGet(internalPosition).density / byte.MaxValue * 2;
                    // else
                    // {
                    //     bool isAdjacent = manager.TryGetVoxel(internalPosition + chunk.Position * MapManager.ChunkSize, out Voxel adjacentVoxel);
                    //     bool useEmpty = !isAdjacent || isOnWorldEdge;
                    //     density = useEmpty ? 0.0f : (float)adjacentVoxel.Density / byte.MaxValue * 2;
                    // }
                    bool isAdjacent = manager.TryGetVoxel(internalPos + AdjOffsets[orient], out Voxel adjVoxel);
                    float density = isAdjacent ? adjVoxel.Density : 0.0f;
                    densities[orient] = density;
                    if (density < IsoLevel) cubeIndex |= 1 << orient;
                    positions[orient] = (Vector3)(pos + Positions[orient]);
                }
                if (cubeIndex is byte.MinValue or byte.MaxValue) continue;

                for (var i = 0; i < 12; i++)
                    vertices[i] = (EdgeTable[cubeIndex] & (1 << i)) != 0
                        ? InterpolateVertex(positions[VertIdx1[i]], positions[VertIdx2[i]],
                                            densities[VertIdx1[i]], densities[VertIdx2[i]])
                        : Vector3.Zero;
                for (var i = 0; TriangleTable[cubeIndex][i] != -1; i += 3)
                {
                    for (var j = 0; j < 3; j++)
                    {
                        var index = (uint)solidMesh.vertices.Count;
                        Vector3D<float> vertPos = vertices[TriangleTable[cubeIndex][i + j]] - Offset;
                        solidMesh.vertices.Add(new Vertex(vertPos, Vector4D<float>.One));
                        solidMesh.indices.Add(index);
                    }
                    // if (voxel.texture == VoxelTexture.Solid && voxel.IsNatural)
                    //     GenerateFoliage(solidMesh, foliageMesh, chunk.Position + position, ref voxel);
                    // int length = voxel.FaceUVs(CachedUvs);
                    // for (var j = 0; j < length; j++) solidMesh.uvs.Add(CachedUvs[j]);
                }
            }
        }
    }

    private static void GenerateBlock(in Voxel voxel, in Vector3Int pos, int orient, Mesh mesh)
    {
        Vector4D<byte> color = voxel.Color;
        foreach (Vector3 vert in BlockVerts[orient])
            mesh.vertices.Add(new Vertex((Vector3)pos + vert, (Vector4D<float>)color));
        mesh.indices.Add((uint)(mesh.vertices.Count - 4));
        mesh.indices.Add((uint)(mesh.vertices.Count - 3));
        mesh.indices.Add((uint)(mesh.vertices.Count - 2));
        mesh.indices.Add((uint)(mesh.vertices.Count - 4));
        mesh.indices.Add((uint)(mesh.vertices.Count - 2));
        mesh.indices.Add((uint)(mesh.vertices.Count - 1));
        // int length = voxel.FaceUVs(CachedUvs);
        // for (var i = 0; i < length; i++) mesh.uvs.Add(CachedUvs[i]);
    }

    private static Vector3 InterpolateVertex(in Vector3 p1, in Vector3 p2, float v1, float v2)
    {
        if (Scalar.Abs(IsoLevel - v1) < Epsilon || Scalar.Abs(v1 - v2) < Epsilon)
            return p1;
        if (Scalar.Abs(IsoLevel - v2) < Epsilon)
            return p2;
        float mu = (IsoLevel - v1) / (v2 - v1);
        return new Vector3(p1.X + mu * (p2.X - p1.X),
                           p1.Y + mu * (p2.Y - p1.Y),
                           p1.Z + mu * (p2.Z - p1.Z));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldRenderBlock(in Voxel voxel, int orient) => (voxel.Flags & VoxelFlags.IsBlock) == 0;
}