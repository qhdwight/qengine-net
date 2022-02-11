using System;
using System.Linq;
using Game.ECS;
using Game.Graphic.Vulkan;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace Game.Input;

public class InputSystem : ISystem
{
    public void Execute(World world)
    {
        foreach (Entity inputEnt in world.View<VkGraphics, Keyboard>())
        {
            ref VkGraphics graphics = ref world.GetComp<VkGraphics>(inputEnt);
            ref Keyboard keyboard = ref world.GetComp<Keyboard>(inputEnt);
            if (graphics.window is not null)
            {
                graphics.input ??= graphics.window.CreateInput();
                IKeyboard? physKeyboard = graphics.input.Keyboards.FirstOrDefault();
                if (physKeyboard is null)
                {
                    keyboard.Move = Vector3D<double>.Zero;
                }
                else
                {
                    double right = (physKeyboard.IsKeyPressed(Key.D) ? 1.0 : 0.0) - (physKeyboard.IsKeyPressed(Key.A) ? 1.0 : 0.0);
                    double forward = (physKeyboard.IsKeyPressed(Key.W) ? 1.0 : 0.0) - (physKeyboard.IsKeyPressed(Key.S) ? 1.0 : 0.0);
                    double up = (physKeyboard.IsKeyPressed(Key.Space) ? 1.0 : 0.0) - (physKeyboard.IsKeyPressed(Key.ShiftLeft) ? 1.0 : 0.0);
                    keyboard.Move = new Vector3D<double>(right, forward, up);
                }
            }
        }
        foreach (Entity inputEnt in world.View<VkGraphics, Mouse>())
        {
            ref VkGraphics graphics = ref world.GetComp<VkGraphics>(inputEnt);
            ref Mouse mouse = ref world.GetComp<Mouse>(inputEnt);
            if (graphics.window is not null)
            {
                graphics.input ??= graphics.window.CreateInput();
                IMouse? physMouse = graphics.input.Mice.FirstOrDefault();
                if (physMouse is null)
                {
                    mouse.Position = Vector2D<double>.Zero;
                }
                else
                {
                    var mousePos = new Vector2D<double>(physMouse.Position.X, physMouse.Position.Y);
                    mouse.Delta = mousePos - mouse.Position;
                    mouse.Position = mousePos;
                }
            }
        }
    }
}