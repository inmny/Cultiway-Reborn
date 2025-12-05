namespace Cultiway.Core.Pathfinding;

public interface IPathStreamWriter
{
    void AddStep(WorldTile tile, MovementMethod method);
    void Complete();
}
