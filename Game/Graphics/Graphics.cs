using System;
using Silk.NET.Vulkan;
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

    private IWindow CreateWindow()
    {
        var windowOptions = WindowOptions.DefaultVulkan;
        windowOptions.Title = WindowName;
        IWindow window = Window.Create(windowOptions);
        window.Initialize();

        Vk vulkan = Vk.GetApi();
        if (vulkan == null) throw new Exception("Vulkan not supported");

        unsafe
        {
            var appInfo = new ApplicationInfo(pApplicationName: CStr(WindowName),
                                              applicationVersion: 1,
                                              pEngineName: CStr(EngineName),
                                              engineVersion: 1,
                                              apiVersion: GetVersion(0, 1, 0, 0));
            var instCreateInfo = new InstanceCreateInfo(pApplicationInfo: &appInfo,
                                                        enabledLayerCount: 0);
            Instance vulkanInst;
            vulkan.CreateInstance(instCreateInfo, null, &vulkanInst);
        }
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