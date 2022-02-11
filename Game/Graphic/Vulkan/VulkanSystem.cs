using Game.ECS;
using Silk.NET.Windowing;

namespace Game.Graphic.Vulkan;

public class VulkanSystem : ISystem
{
    public void Execute(World world)
    {
        foreach (Entity ent in world.View<VkGraphics, WantsQuit>())
        {
            ref VkGraphics graphics = ref world.GetComp<VkGraphics>(ent);
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
                foreach (Entity meshEnt in world.View<VkMesh>())
                {
                    ref VkMesh vkMesh = ref world.GetComp<VkMesh>(meshEnt);
                    VulkanGraphics.CleanupMeshBuffers(ref graphics, ref vkMesh);
                }
                VulkanGraphics.CleanUp(ref graphics);
            }
        }
    }

    private static void Render(World world)
    {
        foreach (Entity graphicsEnt in world.View<VkGraphics>())
        {
            ref VkGraphics graphics = ref world.GetComp<VkGraphics>(graphicsEnt);
            if (VulkanGraphics.TryBeginDraw(ref graphics, out uint imgIdx))
            {
                foreach (Entity ent in world.View<Mesh, VkMesh>())
                {
                    ref Mesh mesh = ref world.GetComp<Mesh>(ent);
                    ref VkMesh vkMesh = ref world.GetComp<VkMesh>(ent);
                    VulkanGraphics.Draw(ref graphics, mesh, ref vkMesh);
                }
                VulkanGraphics.EndDraw(ref graphics, imgIdx);
            }
        }
    }
}