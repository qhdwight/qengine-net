using System.Collections.Generic;
using System.Diagnostics;
using Game.ECS;
using Game.Graphic;
using Game.Graphic.Vulkan;
using Game.Input;
using Game.Players;
using Game.Voxels;
using Game.Voxels.Maps;

namespace Game;

public static class Game
{
    private static readonly List<ISystem> Systems = new() { new InputSystem(), new PlayerSystem(), new MarchingCubes(), new VulkanSystem() };

    public static long ElapsedMicroseconds(this Stopwatch stopwatch) => (long)(stopwatch.ElapsedTicks * (1_000_000.0m / Stopwatch.Frequency));

    private static int Main()
    {
        var world = new World();
        Entity globalEnt = world.AddEntity();
        world.AddComp(globalEnt, new VkGraphics());
        world.AddComp(globalEnt, new Keyboard());
        world.AddComp(globalEnt, new Mouse());
        world.AddComp(globalEnt, new WantsQuit());
        world.AddComp(globalEnt, new Time());

        Entity cubeEnt = world.AddEntity();
        world.AddComp(cubeEnt, new Position());

        Entity mapEnt = world.AddEntity();
        var mapManager = new MapManager();
        world.AddComp(mapEnt, mapManager);
        world.AddComp(mapEnt, new Mesh());
        world.AddComp(mapEnt, new VkMesh());

        Entity playerEnt = world.AddEntity();
        world.AddComp(playerEnt, new Player());

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        while (world.All((World _, WantsQuit exec) => !exec.Value))
        {
            ref Time time = ref world.GetComp<Time>(globalEnt);
            time.Elapsed = stopwatch.ElapsedMicroseconds();

            foreach (ISystem system in Systems)
                system.Execute(world);

            long elapsed = stopwatch.ElapsedMicroseconds();
            time.Delta = elapsed - time.Elapsed;
            time.Elapsed = elapsed;
        }
        stopwatch.Stop();

        return 0;
    }
}