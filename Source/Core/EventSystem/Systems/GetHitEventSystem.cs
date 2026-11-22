using Cultiway.Core.Combat;
using Cultiway.Core.Visuals;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Utils.Extension;

namespace Cultiway.Core.EventSystem.Systems;

/// <summary>
/// 处理命中事件，在主线程执行伤害结算。
/// </summary>
public class GetHitEventSystem : GenericEventSystem<GetHitEvent>
{
    protected override int MaxEventsPerUpdate => 1024;

    protected override void HandleEvent(GetHitEvent evt) => Resolve(evt);

    /// <summary>在主线程立即执行相同标准受击规则，供必须按接触顺序确认死亡及破盾的过程使用。</summary>
    /// <param name="evt">已经绑定稳定来源及作用范围的伤害。</param>
    internal static DamageSettlement Resolve(GetHitEvent evt)
    {
        var result = ResolveHit(evt);
        evt.Observation?.Complete(result);
        return result;
    }

    /// <summary>执行共同受击规则，不把找不到目标的事件报成已结算。</summary>
    /// <param name="evt">实际入队或明确立即结算的伤害。</param>
    private static DamageSettlement ResolveHit(GetHitEvent evt)
    {
        if (evt.TargetID == 0 || evt.Damage <= 0)
        {
            return default;
        }

        var actor = World.world.units.get(evt.TargetID);
        if (actor == null || actor.isRekt())
        {
            return default;
        }

        BaseSimObject attacker = null;
        if (evt.HasAttacker)
        {
            attacker = evt.AttackerIsActor
                ? World.world.units.get(evt.AttackerID)
                : World.world.buildings.get(evt.AttackerID);
            if (attacker.isRekt()) attacker = null;
        }

        using (DamageResolutionContext.Enter(evt.DamageOrigin, evt.SourceScopeId))
        {
            var element = evt.Element;
            UnityEngine.Vector3 contact = !string.IsNullOrEmpty(evt.ContactSound) ? actor.GetSimPos() : default;
            var result = actor.GetExtend().ResolveHit(evt.Damage, ref element, attacker,
                ignore_damage_reduction: evt.IgnoreDamageReduction,
                attack_type_for_vanilla: evt.AttackType,
                attacker_power_level_override: evt.AttackerPowerLevel,
                metallic_weapon: evt.MetallicWeapon,
                contactPoint: evt.ContactPoint, usesDefenseSurface: evt.UsesDefenseSurface);
            if (result.HealthLost > 0) SpatialCombatFeedback.Sound(evt.ContactSound, contact);
            return result;
        }
    }
}
