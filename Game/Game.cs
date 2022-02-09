using System.Collections.Generic;
using System.Numerics;
using Game.Graphic;
using Game.Graphic.Vulkan;

namespace Game;

public static class Game
{
    private static readonly List<ISystem> Systems = new() { new GraphicsSystem() };

    private static int Main()
    {
        var world = new World();
        Entity displayEnt = world.AddEntity();
        world.AddComp(displayEnt, new Graphics());
        world.AddComp(displayEnt, new WantsQuit());
        Entity cubeEnt = world.AddEntity();
        world.AddComp(cubeEnt, new Position(Vector3.Zero));
        while (world.All((World _, WantsQuit exec) => !exec.Value))
            foreach (ISystem system in Systems)
                system.Execute(world);
        return 0;
    }
}