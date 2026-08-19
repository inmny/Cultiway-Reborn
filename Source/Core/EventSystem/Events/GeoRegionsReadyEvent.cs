namespace Cultiway.Core.EventSystem.Events;

/// <summary>当前世界的地理地区已经完成首次安装，可以开始生成依赖地区的内容。</summary>
public struct GeoRegionsReadyEvent
{
    public int WorldSeedId;
    public int Width;
    public int Height;
    public int MembershipRevision;
}
