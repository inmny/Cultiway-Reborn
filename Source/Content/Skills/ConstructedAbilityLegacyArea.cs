using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Core.Skills;
using Cultiway.Utils;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>原世界冲击和建筑伤害由真实爆发提交一次，共同人物接触随后继续。</summary>
    private static void ConfirmLegacyAreaImpact(RunningCast running)
    {
        LegacyAreaImpact area = running.AreaImpact;
        int node = running.AreaImpactNode;
        if (area == null || node < 0 || running.WorldScope != null ||
            running.Cast.Role(node) == ConstructionContactRole.Decoration ||
            !running.Cast.TryGetActionActivation(node, out Vector3 point)) return;
        running.AreaImpact = null;
        long generation = worldGeneration;
        CastAuthority authority = running.Owner(node);
        Actor attacker = authority.IsLive ? authority.Actor : null;
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
}
