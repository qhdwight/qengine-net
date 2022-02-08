using Silk.NET.Windowing;

namespace Game
{
    public class Game
    {
        private int Run()
        {
            var windowOptions = WindowOptions.DefaultVulkan;
            windowOptions.Title = "Game";
            IWindow window = Window.Create(windowOptions);
            window.Initialize();
            window.Run(() =>
            {
                window.DoEvents();
                if (!window.IsClosing)
                    window.DoUpdate();
                if (window.IsClosing)
                    return;
                window.DoRender();
            });
            window.DoEvents();
            window.Reset();
            return 0;
        }

        private static int Main(string[] args) { return new Game().Run(); }
    }
}