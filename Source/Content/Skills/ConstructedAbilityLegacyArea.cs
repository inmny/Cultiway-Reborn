using Cultiway.Core.EventSystem;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Core.Skills;
using Cultiway.Utils;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>原范围的落地词条、世界冲击或建筑伤害由真实爆发提交一次。</summary>
    private static void ConfirmLegacyAreaImpact(RunningCast running)
    {
        LegacyAreaImpact area = running.AreaImpact;
        int node = running.AreaImpactNode;
        if (area == null || node < 0 || running.WorldScope != null && !area.UtilityForce.HasValue ||
            running.Cast.Role(node) == ConstructionContactRole.Decoration ||
            !running.Cast.TryGetActionActivation(node, out Vector3 point)) return;
        running.AreaImpact = null;
        long generation = worldGeneration;
        CastAuthority authority = running.Owner(node);
        Actor attacker = authority.IsLive ? authority.Actor : null;
        if (area.UtilityForce is { } force)
        {
            if (running.WorldScope?.IsValid() == false || !authority.WorldSource && attacker == null) return;
            ConfirmUtilityLanding(running.Cast.Impact(node), authority, attacker, point, area.Radius, force, generation);
            return;
        }
        if (area.Element != null)
        {
            SkillGroundFx.ApplyImpact(point, area.Element, area.Radius, true, attacker, true);
            if (generation != worldGeneration) return;
            // 旧雷系地块规则最远固定三格。这里扩展采样边界只为读取已经变化后的地面。
            float reach = Mathf.Max(3, Mathf.RoundToInt(area.Radius)) + 1;
            simulation.RefreshTerrainIn(new Rect(point.x - reach, point.y - reach, reach * 2f, reach * 2f));
        }

        float damage = running.Cast.DamageForce(node) * running.Cast.Impact(node).DamageScale;
        if (!area.DealsBaseDamage || damage <= 0f) return;
        foreach (BaseSimObject target in SkillUtils.IterEnemyInSphere(point, area.Radius, attacker, authority.AttackKingdom()))
        {
            if (!target.isBuilding() || target.isRekt()) continue;
            target.b.getHit(damage, pAttacker: attacker);
            if (generation != worldGeneration) return;
        }
    }

    /// <summary>维持原对象选取与受击队列。先挂周期状态，人物直接火伤交原队列，建筑沿原版受击。</summary>
    /// <param name="impact">已编译的落地词条。</param><param name="source">本次归属与境界。</param>
    /// <param name="actor">仍有效的原施法者。</param><param name="point">实际落地点。</param>
    /// <param name="radius">原水平半径。</param><param name="force">本次固定附加强度。</param>
    /// <param name="generation">本次执行世界。</param>
    private static void ConfirmUtilityLanding(CompiledConstructionImpact impact, CastAuthority source, Actor actor,
        Vector3 point, float radius, float force, long generation)
    {
        foreach (BaseSimObject target in SkillUtils.IterEnemyInSphere(point, radius, actor, source.AttackKingdom()))
        {
            if (target.isRekt()) continue;
            if (target.isActor())
                foreach (var status in impact.Statuses)
                {
                    ApplyStatus(target.a, source, status, 1f, force);
                    if (generation != worldGeneration) return;
                }

            foreach (var effect in impact.AdditionalDamage)
            {
                float amount = Mathf.Max(0f, force * effect.PerForce);
                if (amount <= 0f) continue;
                if (target.isActor())
                {
                    var damage = source.Damage;
                    damage.TargetID = target.getID();
                    damage.Damage = amount;
                    damage.Element = effect.Element;
                    EventSystemHub.Publish(damage);
                }
                else target.b.getHit(amount, pAttacker: actor);
                if (generation != worldGeneration) return;
            }
        }
    }
}
