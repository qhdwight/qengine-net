using System.Collections.Generic;
using Game.ECS;
using Game.Graphic;
using Game.Graphic.Vulkan;
using Game.Voxels;
using Silk.NET.Maths;

namespace Game;

public static class Game
{
    private static readonly List<ISystem> Systems = new() { new GraphicsSystem() };

    private static int Main()
    {
        var world = new World();
        Entity displayEnt = world.AddEntity();
        world.AddComp(displayEnt, new VkGraphics());
        world.AddComp(displayEnt, new WantsQuit());

        Entity cubeEnt = world.AddEntity();
        world.AddComp(cubeEnt, new Position());

        Entity voxelEnt = world.AddEntity();
        var octree = new Octree<Voxel>(64, Vector3D<int>.Zero, 8);
        world.AddComp(voxelEnt, octree);
        octree.Add(new Voxel(), new Vector3D<int>(1, 2, 7));

        while (world.All((World _, WantsQuit exec) => !exec.Value))
            foreach (ISystem system in Systems)
                system.Execute(world);
        
        return 0;
    }
}