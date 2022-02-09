using System.Collections.Generic;
using System.Numerics;
using Game.Graphics;
using Silk.NET.Windowing;

namespace Game;

public static class Game
{
    private static readonly List<ISystem> Systems = new() { new GraphicsSystem() };

    private static IWindow CreateWindow()
    {
        var windowOptions = WindowOptions.DefaultVulkan;
        windowOptions.Title = "Game";
        return Window.Create(windowOptions);
    }

    private static int Main(string[] args)
    {
        var world = new World();
        Entity displayEnt = world.AddEntity();
        world.Add(displayEnt, new Display(CreateWindow()));
        world.Add(displayEnt, new WantsQuit());
        Entity cubeEnt = world.AddEntity();
        world.Add(cubeEnt, new Position(Vector3.Zero));
        while (world.All((World _, WantsQuit exec) => !exec.Yes))
            foreach (ISystem system in Systems)
                system.Update(world);
        return 0;
    }
}