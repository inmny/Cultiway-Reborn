namespace Cultiway.Core.Pathfinding;

public sealed class PortalConnection
{
    public PortalConnection(long targetId, float travelTime)
    {
        TargetId = targetId;
        TravelTime = travelTime;
    }

    public long TargetId { get; }
    public float TravelTime { get; }
}
