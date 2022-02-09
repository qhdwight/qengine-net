using Silk.NET.Windowing;

namespace Game.Graphics;

public class GraphicsSystem : ISystem
{
    private const string WindowName = "Game", EngineName = "QLib";

    private static uint GetVersion(uint variant, uint major, uint minor, uint patch)
        => (variant << 29) | (major << 22) | (minor << 12) | patch;

    private static unsafe byte* CStr(string netString)
    {
        fixed (char* pString = netString) return (byte*)pString;
    }

    private static IWindow CreateWindow()
    {
        var windowOptions = WindowOptions.DefaultVulkan;
        windowOptions.Title = WindowName;
        IWindow window = Window.Create(windowOptions);
        window.Initialize();
        return window;
    }

    public void Execute(World world)
    {
        foreach (Entity ent in world.View<Display, WantsQuit>())
        {
            ref Display display = ref world.GetComp<Display>(ent);
            display.Window ??= CreateWindow();
            IWindow window = display.Window;

            void OnFrame()
            {
                window.DoEvents();
                if (!window.IsClosing) window.DoUpdate();
                if (window.IsClosing) return;
                window.DoRender();
            }

            window.Run(OnFrame);
            window.DoEvents();

            if (window.IsClosing)
            {
                window.Reset();
                world.GetComp<WantsQuit>(ent).Value = true;
            }
        }
    }
}