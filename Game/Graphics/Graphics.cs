using Silk.NET.Windowing;

namespace Game.Graphics;

public class GraphicsSystem : ISystem
{
    public void Update(World world)
    {
        foreach (Entity ent in world.View<Display, WantsQuit>())
        {
            ref Display display = ref world.Get<Display>(ent);
            IWindow window = display.Handle;
            if (!window.IsInitialized)
                window.Initialize();
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
                world.Get<WantsQuit>(ent).Yes = true;
            }
        }
    }
}