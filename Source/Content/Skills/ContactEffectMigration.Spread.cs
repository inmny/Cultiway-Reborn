using System;
using Cultiway.Content.Components.Skill;
using Cultiway.Core;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Skills;

internal static partial class ContactEffectMigration
{
    /// <summary>原爆炸只展开其自身伤害，不把燃烧、换位等其他词条带给附近人物。</summary>
    /// <param name="skill">原技能容器。</param><param name="caster">真实支付者。</param>
    /// <param name="forceScale">本体量换回原附加强度的比例。</param>
    private static ConstructionContactSpread[] ContactSpreads(Entity skill, ActorExtend caster, float forceScale)
    {
        if (!skill.TryGetComponent(out ExplosionModifier explosion)) return Array.Empty<ConstructionContactSpread>();
        var asset = skill.GetComponent<SkillContainer>().Asset;
        float original = SkillEffectRadius.ResolveContainer(skill, 1f), growth = SkillEffectRadius.ResolveCasterGrowth(caster, skill);
        float basis = Mathf.Max(asset.ImpactProfile.EffectRadius, asset.ImpactProfile.CollisionRadius);
        if (basis > 0f) growth = Mathf.Max(1f, Mathf.Min(growth, 32f / (basis * original)));
        float radius = Mathf.Clamp(explosion.Radius, .5f, 10f) * original * growth;
        float ratio = Mathf.Clamp(explosion.DamageRatio, 0f, 2f) * forceScale;
        if (radius <= 0f || ratio <= 0f) return Array.Empty<ConstructionContactSpread>();
        return new[] { new ConstructionContactSpread
        {
            Radius = radius, BuildingDamagePerForce = ratio,
            Outcome = new ConstructionContactOutcome { Damage = new[] { new ConstructionContactDamage { PerForce = ratio, Element = asset.Element } } }
        } };
    }
}
