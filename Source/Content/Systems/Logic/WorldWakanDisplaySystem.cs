using Cultiway.Abstract;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>定期公布灵气显示副本，避免地图线程读取正在结算的运行数据。</summary>
public sealed class WorldWakanDisplaySystem : BaseSystem, IWorldStateClearable
{
    private const float PublishInterval = 0.25f;
    private float elapsed;

    protected override void OnUpdateGroup()
    {
        if (!WorldWakanService.IsInitialized) return;
        elapsed += Tick.deltaTime;
        if (elapsed < PublishInterval) return;
        elapsed -= PublishInterval;
        WorldWakanService.PublishDisplayValues();
    }

    public void ClearWorldState()
    {
        elapsed = 0f;
    }
}
