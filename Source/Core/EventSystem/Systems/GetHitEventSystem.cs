using Cultiway.Core.EventSystem.Events;
using Cultiway.Utils.Extension;

namespace Cultiway.Core.EventSystem.Systems;

/// <summary>
/// 处理命中事件，在主线程执行伤害结算。
/// </summary>
public class GetHitEventSystem : GenericEventSystem<GetHitEvent>
{
    protected override int MaxEventsPerUpdate => 1024;

    protected override void HandleEvent(GetHitEvent evt)
    {
        if (evt.TargetID == 0 || evt.Damage <= 0)
        {
            return;
        }

        var actor = World.world.units.get(evt.TargetID);
        if (actor == null || actor.isRekt())
        {
            return;
        }

        var element = evt.Element;
        actor.GetExtend().GetHit(evt.Damage, ref element, evt.Attacker, ignore_damage_reduction: evt.IgnoreDamageReduction);
    }
}

