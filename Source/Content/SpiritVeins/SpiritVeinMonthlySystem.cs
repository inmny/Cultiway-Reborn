using Cultiway.Abstract;
using Cultiway.Const;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.SpiritVeins;

/// <summary>按经过的世界月份推进灵脉恢复、供灵、衰弱和污染。</summary>
internal sealed class SpiritVeinMonthlySystem : BaseSystem, IWorldStateClearable
{
    private float remaining = TimeScales.SecPerMonth;

    protected override void OnUpdateGroup()
    {
        SpiritVeinManager manager = WorldboxGame.I?.SpiritVeins;
        manager?.UpdateRerouteTask();
        if (manager?.IsReady != true || Tick.deltaTime <= 0f) return;
        remaining -= Tick.deltaTime;
        int guard = 0;
        while (remaining <= 0f && guard++ < 1200)
        {
            remaining += TimeScales.SecPerMonth;
            manager.UpdateMonth();
        }
    }

    public void ClearWorldState()
    {
        remaining = TimeScales.SecPerMonth;
    }
}
