namespace Cultiway.Core.Pathfinding;

public interface IPathStreamWriter
{
    void AddStep(PathStep step);
    void Complete();
    void Cancel();
    void Fail(System.Exception error);
    void EnsureCompleted();
}
