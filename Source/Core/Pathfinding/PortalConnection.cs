namespace Cultiway.Core.Pathfinding;

public sealed class PortalConnection
{
    public PortalConnection(long targetId, float travelTime, long country = 0, int passengerKind = 0)
    {
        TargetId = targetId;
        TravelTime = travelTime;
        Country = country;
        PassengerKind = passengerKind;
    }

    public long TargetId { get; }
    public float TravelTime { get; }
    /// <summary>限定乘客所属国家；零表示沿用通用连接。</summary>
    public long Country { get; }
    /// <summary>零为通用，一为民用，二为军队专用路线。</summary>
    public int PassengerKind { get; }
    /// <summary>后台寻路只比较数值，不访问国家或人物对象。</summary>
    /// <param name="country">乘客国家。</param>
    /// <param name="military">是否属于军队。</param>
    public bool Accepts(long country, bool military)
        => (Country == 0 || Country == country) &&
           (PassengerKind == 0 || PassengerKind == (military ? 2 : 1));
}
