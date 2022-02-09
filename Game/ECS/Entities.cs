using System.Numerics;

namespace Game.ECS;

public record struct Entity(int Index);

public record struct Position(Vector3 Value);

public record struct Rotation(Quaternion Value);

public record struct WantsQuit(bool Value);