using Silk.NET.Maths;

namespace Game.ECS;

public record struct Entity(int Index);

public record struct Position(Vector3D<double> Value);

public record struct Orientation(Quaternion<double> Value);

public record struct Player(Vector3D<double> Position, Vector3D<double> EulerOrientation);

public record struct Time(long Elapsed, long Delta);

public record struct WantsQuit(bool Value);