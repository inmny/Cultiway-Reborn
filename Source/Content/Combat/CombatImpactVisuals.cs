using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Content.Visuals;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Core.Visuals;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>只在最终伤害为正数时显示接触碎屑，碰撞尝试不冒充受伤。</summary>
internal static class CombatImpactVisuals
{
    /// <summary>接入屏障实际消耗通知。</summary>
    internal static void Initialize()
    {
        SkillHitResolver.BarrierContactResolved += OnBarrierContact;
    }

    /// <summary>只有实际耐久损耗才在局部出现短亮屑，避免把整面护盾闪白。</summary>
    /// <param name="entity">实际防护执行体。</param><param name="point">真实接触处。</param><param name="damage">真实吸收量。</param>
    private static void OnBarrierContact(Entity entity, Vector2 point, float damage)
    {
        if (damage <= 0) return;
        float size = Mathf.Clamp(Mathf.Sqrt(damage) * .02f, .08f, .3f);
        Color color = entity.GetComponent<SkillEntity>().VfxElement.AccentColor;
        color = Color.Lerp(color, Color.white, .5f);
        Vector3 center = entity.GetComponent<Position>().value;
        Vector2 outward = (point - (Vector2)center).normalized;
        Vector2 tangent = new(-outward.y, outward.x);
        for (int i = -1; i <= 1; i++)
        {
            Vector2 velocity = outward * .46f + tangent * (i * .35f);
            SpatialVisualEffects.Particle(new Vector3(point.x, point.y, center.z + .18f),
                new Vector3(velocity.x, velocity.y, .58f), color, size, .24f - Mathf.Abs(i) * .04f,
                SpatialParticleMaterial.Fragment, -2f);
        }
    }

    /// <summary>只有原版结算明确返回格挡或偏转时才出现冷色接触纹。</summary>
    /// <param name="target">真实防守者。</param><param name="attacker">真实来袭者。</param>
    internal static void OnDefended(BaseSimObject target, Actor attacker)
    {
        Vector3 point = target.GetSimPos();
        Vector3 direction = (point - attacker.GetSimPos()).normalized;
        for (int i = 0; i < 3; i++)
            SpatialVisualEffects.Particle(point, -direction * (.3f + i * .1f) + Vector3.forward * .5f,
                new Color(.65f, .8f, 1f, .8f), .05f, .12f + i * .025f);
    }

    /// <summary>只在实际失去生命后显示接触；原版武器材质在受击入队时已冻结。</summary>
    /// <param name="target">实际受伤者。</param>
    /// <param name="attacker">实际来源。</param>
    /// <param name="healthLost">本次实际扣除的生命。</param>
    /// <param name="composition">实际结算的元素。</param>
    /// <param name="attackType">原攻击类别。</param>
    /// <param name="metallicWeapon">原版明确交来的武器材质；无资料时保留元素色。</param>
    internal static void OnHealthLost(ActorExtend target, BaseSimObject attacker, int healthLost,
        ElementComposition composition, AttackType attackType, bool? metallicWeapon)
    {
        if (healthLost <= 0 || target.Base.isRekt()) return;
        bool weapon = attackType == AttackType.Weapon;
        Vector3 point = weapon
            ? SpatialActorPose.Foot(target.Base) + Vector3.forward * (SpatialActorPose.Height(target.Base) * .55f)
            : target.Base.GetSimPos();
        if (!SpatialVisualEffects.IsRelevant(point, .4f)) return;
        if (weapon && attacker?.isActor() == true && !attacker.isRekt())
            BattleSpellVisualRuntime.Contact(attacker.a, target.Base);
        Color color = metallicWeapon == false ? new Color(.61f, .45f, .31f, .76f) :
            Color.Lerp(SkillVfxColor.GetElementColor(composition), Color.white, .22f);
        Vector2 approach = attacker != null && !attacker.isRekt() ? (Vector2)(point - attacker.GetSimPos()) : Vector2.zero;
        Vector3 direction = approach.normalized;
        Vector3 side = new(-direction.y, direction.x, 0f);
        float size = Mathf.Clamp(target.Base.actor_scale * .8f, .035f, .16f);
        int count = metallicWeapon == false ? 2 : 3;
        SpatialParticleMaterial material = weapon
            ? SpatialParticleMaterial.Fragment : SpatialParticleMaterial.Energy;
        for (int i = 0; i < count; i++)
        {
            Vector3 velocity = direction * (.34f + i * .12f) + side * ((i - (count - 1) * .5f) * .45f) +
                Vector3.forward * (.6f - i * .12f);
            SpatialVisualEffects.Particle(point, velocity, color, size, .16f + i * .025f, material, -3f);
        }
    }
}
