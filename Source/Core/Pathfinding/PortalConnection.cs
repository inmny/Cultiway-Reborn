namespace Cultiway.Core.Pathfinding;

public sealed class PortalConnection
{
    public PortalConnection(string targetId, float travelTime)
    {
        TargetId = targetId;
        TravelTime = travelTime;
    }

    public string TargetId { get; }
    public float TravelTime { get; }
}
