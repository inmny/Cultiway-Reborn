using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;

namespace Cultiway.Content.SpiritVeins;

/// <summary>把地区系统提交的稳定地形变化交给灵脉管理器延迟处理。</summary>
internal sealed class StableTerrainChangesSpiritVeinEventSystem :
    GenericEventSystem<StableTerrainChangesCommittedEvent>
{
    protected override void HandleEvent(StableTerrainChangesCommittedEvent evt)
    {
        if (!evt.HasChanges || evt.WorldSeedId != MapBox.current_world_seed_id ||
            evt.Width != MapBox.width || evt.Height != MapBox.height)
        {
            return;
        }

        WorldboxGame.I?.SpiritVeins?.ApplyTerrainChanges(evt.ChangedTileIds, evt.TopologyChanged);
    }
}
