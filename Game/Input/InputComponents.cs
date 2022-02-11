using Silk.NET.Maths;

namespace Game.Input;

public record struct Mouse(Vector2D<double> Position, Vector2D<double> Delta, bool LeftButton, bool RightButton, bool MiddleButton);

public record struct Keyboard(Vector3D<double> Move);