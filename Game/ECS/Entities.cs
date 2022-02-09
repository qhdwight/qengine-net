using Silk.NET.Maths;

namespace Game.ECS;

public record struct Entity(int Index);

public record struct Position(Vector3D<double> Value);

public record struct Rotation(Quaternion<double> Value);

public record struct WantsQuit(bool Value);