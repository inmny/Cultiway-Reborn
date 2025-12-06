namespace Cultiway.Core.Pathfinding;

public interface IPathStreamWriter
{
    void AddStep(WorldTile tile, MovementMethod method, StepPenalty penalty);
    void Complete();
    void Cancel();
    void Fail(System.Exception error);
    void EnsureCompleted();
}
