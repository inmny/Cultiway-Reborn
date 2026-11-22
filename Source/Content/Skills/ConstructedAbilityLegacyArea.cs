using System.Linq;
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
        if (area.UtilityForce.HasValue)
        {
            if (running.WorldScope?.IsValid() == false || !authority.WorldSource && attacker == null) return;
            ConfirmUtilityLanding(running, node, area, authority, attacker, point);
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

    /// <summary>使用原落地的对象选取、伤害与位移规则。换位时固定当次对象，避免换到另一个地块后重复命中。</summary>
    /// <param name="running">真实施放。</param><param name="node">实际落地部位。</param>
    /// <param name="area">本次落地范围与逐发强度。</param><param name="source">本次归属与境界。</param>
    /// <param name="actor">仍有效的原施法者。</param><param name="point">实际落地点。</param>
    private static void ConfirmUtilityLanding(RunningCast running, int node, LegacyAreaImpact area, CastAuthority source,
        Actor actor, Vector3 point)
    {
        long generation = worldGeneration;
        CompiledConstructionImpact impact = running.Cast.Impact(node);
        float force = area.UtilityForce.Value;
        var carrier = running.SpatialSource;
        if (carrier?.data?.id != running.SpatialSourceId || !running.SpatialSourceLive || !carrier.isAlive()) carrier = actor;
        long carrierId = carrier?.data?.id ?? 0;
        var recipients = SkillUtils.IterEnemyInSphere(point, area.Radius, actor, source.AttackKingdom())
            .Select(target => (Target: target, Id: target.getID()));
        if (impact.Exchange != null && carrier != null) recipients = recipients.ToArray();
        foreach (var (target, id) in recipients)
        {
            if (target.isRekt() || target.getID() != id) continue;
            if (target.isActor())
            {
                foreach (var status in impact.Statuses)
                {
                    ApplyStatus(target.a, source, status, 1f, force);
                    if (generation != worldGeneration) return;
                }
            }

            foreach (var effect in impact.AdditionalDamage)
            {
                float amount = Mathf.Max(0f, force * effect.PerForce);
                if (amount <= 0f) continue;
                if (target.isActor())
                {
                    var damage = source.Damage;
                    damage.TargetID = id;
                    damage.Damage = amount;
                    damage.Element = effect.Element;
                    EventSystemHub.Publish(damage);
                }
                else target.b.getHit(amount, pAttacker: actor);
                if (generation != worldGeneration) return;
            }

            Actor liveCarrier = carrier?.data?.id == carrierId && carrier.isAlive() ? carrier : null;
            if (liveCarrier != null && impact.Restoration != null)
            {
                int amount = Mathf.RoundToInt(force * impact.Restoration.HealthPerForce);
                if (amount > 0) RestoreLivingActor(liveCarrier, amount);
                if (generation != worldGeneration) return;
            }
            if (impact.Knockback == null && impact.Exchange == null || !target.isActor() || target.isRekt() || target.getID() != id) continue;
            if (area.SwapFirst)
                ApplyContactExchange(liveCarrier, target.a, impact.Exchange, ApplyAttackState.Hit, 1f, useTiles: true);
            if (generation != worldGeneration) return;
            if (target.getID() != id || target.isRekt()) continue;
            ApplyContactKnockback(running, node, target.a, impact.Knockback, ApplyAttackState.Hit, out _, point, 1f);
            if (generation != worldGeneration) return;
            if (!area.SwapFirst && !target.isRekt() && target.getID() == id)
                ApplyContactExchange(liveCarrier?.data?.id == carrierId ? liveCarrier : null, target.a, impact.Exchange,
                    ApplyAttackState.Hit, 1f, useTiles: true);
            if (generation != worldGeneration) return;
        }
    }
}
