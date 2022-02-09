using System.Numerics;
using Silk.NET.Windowing;

namespace Game;

public class Game
{
    private readonly IWindow _window;
    private readonly World _world;

    public Game()
    {
        var windowOptions = WindowOptions.DefaultVulkan;
        windowOptions.Title = "Game";
        _window = Window.Create(windowOptions);
        _world = new World();
        Entity entity = _world.AddEntity();
        ref Position position = ref _world.AddComponent<Position>(entity);
        position = new Position(Vector3.One);
    }

    private int Run()
    {
        _window.Initialize();
        _window.Run(OnFrame);
        _window.DoEvents();
        _window.Reset();
        return 0;
    }

    private void OnFrame()
    {
        _window.DoEvents();
        if (!_window.IsClosing) _window.DoUpdate();
        if (_window.IsClosing) return;
        _window.DoRender();
    }

    public void Update() { }

    private static int Main(string[] args) { return new Game().Run(); }
}