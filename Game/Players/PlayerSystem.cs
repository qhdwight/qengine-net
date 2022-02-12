using System;
using Game.ECS;
using Game.Graphic;
using Game.Input;
using Silk.NET.Maths;

namespace Game.Players;

public class PlayerSystem : ISystem
{
    public void Execute(World world)
    {
        foreach (Entity inputEnt in world.View<Time, Mouse, Keyboard>())
        {
            foreach (Entity playerEnt in world.View<Player>())
            {
                ref Mouse mouse = ref world.GetComp<Mouse>(inputEnt);
                ref Keyboard keyboard = ref world.GetComp<Keyboard>(inputEnt);
                ref Time time = ref world.GetComp<Time>(inputEnt);
                ref Player player = ref world.GetComp<Player>(playerEnt);
                var lateralMove = new Vector3D<double> { X = keyboard.Move.X, Y = keyboard.Move.Y };
                lateralMove = lateralMove.Rotate(player.EulerOrientation.FromEuler());
                Vector3D<double> move = lateralMove + new Vector3D<double> { Z = keyboard.Move.Z };
                player.Position += move * time.Delta * 1e-6d * 10.0f;
                double newX = Math.Clamp(player.EulerOrientation.X - mouse.Delta.Y * 1e-2d, -Math.PI / 2.01, Math.PI / 2.01);
                double newZ = (player.EulerOrientation.Z - mouse.Delta.X * 1e-2d) % Math.Tau;
                player.EulerOrientation = new Vector3D<double> { X = newX, Z = newZ };
            }
        }
    }
}