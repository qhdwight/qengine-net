namespace Game.ECS;

public interface ISystem
{
    void Execute(World world);
}