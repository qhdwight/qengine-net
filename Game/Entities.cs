using System.Numerics;
using Silk.NET.Windowing;

namespace Game;

public record struct Entity(int Index);

public record struct Position(Vector3 Value);

public record struct Rotation(Quaternion Value);

public record struct Display(IWindow? Window);

public record struct WantsQuit(bool Value);