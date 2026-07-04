namespace Cultiway.Core.Pathfinding;

public interface IPathStreamWriter
{
    void AddStep(PathStep step);
    void Complete();
    void Cancel(PathFailureReason reason = PathFailureReason.CancelledByNewRequest);
    void Fail(PathFailureReason reason, System.Exception error = null);
    void Fail(System.Exception error);
    void EnsureCompleted();
}
