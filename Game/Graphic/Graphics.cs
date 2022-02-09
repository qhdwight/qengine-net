using Game.ECS;
using Game.Graphic.Vulkan;
using Silk.NET.Windowing;

namespace Game.Graphic;

public class GraphicsSystem : ISystem
{
    public void Execute(World world)
    {
        foreach (Entity ent in world.View<Graphics, WantsQuit>())
        {
            ref Graphics graphics = ref world.GetComp<Graphics>(ent);
            if (graphics.window is null)
                VulkanGraphics.InitVulkan(ref graphics);
            IWindow window = graphics.window!;

            void OnFrame()
            {
                window.DoEvents();
                if (!window.IsClosing) window.DoUpdate();
                if (window.IsClosing) return;
                Render(world);
            }

            window.Run(OnFrame);
            window.DoEvents();

            if (window.IsClosing)
            {
                window.Reset();
                world.GetComp<WantsQuit>(ent).Value = true;
                VulkanGraphics.CleanUp(ref graphics);
            }
        }
    }

    private static void Render(World world)
    {
        foreach (Entity ent in world.View<Graphics>())
        {
            ref Graphics graphics = ref world.GetComp<Graphics>(ent);
            VulkanGraphics.Render(ref graphics);
        }
    }
}