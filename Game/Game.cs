using System.Collections.Generic;
using Game.ECS;
using Game.Graphic;
using Game.Graphic.Vulkan;
using Game.Voxels;
using Game.Voxels.Maps;

namespace Game;

public static class Game
{
    private static readonly List<ISystem> Systems = new() { new MarchingCubes(), new VulkanSystem() };

    private static int Main()
    {
        var world = new World();
        Entity displayEnt = world.AddEntity();
        world.AddComp(displayEnt, new VkGraphics());
        world.AddComp(displayEnt, new WantsQuit());

        Entity cubeEnt = world.AddEntity();
        world.AddComp(cubeEnt, new Position());

        Entity mapEnt = world.AddEntity();
        var mapManager = new MapManager();
        world.AddComp(mapEnt, mapManager);
        world.AddComp(mapEnt, new Mesh());
        world.AddComp(mapEnt, new VkMesh());

        while (world.All((World _, WantsQuit exec) => !exec.Value))
            foreach (ISystem system in Systems)
                system.Execute(world);

        return 0;
    }
}