using Cultiway.Core;
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
    /// <summary>接入现有最终伤害通知。</summary>
    internal static void Initialize()
    {
        ActorExtend.RegisterActionOnDamageResolved(OnDamage);
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
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.PI * 2 / 5;
            SpatialVisualEffects.Particle(new Vector3(point.x, point.y, .3f),
                new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), .5f) * .8f, color, size, .22f);
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

    /// <summary>在真实受伤部位产生少量集中反馈。</summary>
    /// <param name="target">实际受伤者。</param><param name="attacker">实际来源。</param><param name="damage">最终伤害。</param>
    /// <param name="composition">实际元素。</param><param name="attackType">原攻击类别。</param>
    private static void OnDamage(ActorExtend target, BaseSimObject attacker, float damage, ElementComposition composition, AttackType attackType)
    {
        if (damage <= 0f || target.Base.isRekt()) return;
        Vector3 point = target.Base.GetSimPos();
        if (!SpatialVisualEffects.IsRelevant(point, .4f)) return;
        if(attackType==AttackType.Weapon && attacker?.isActor()==true && !attacker.isRekt())
            Cultiway.Content.Visuals.BattleSpellVisualRuntime.Contact(attacker.a,target.Base);
        if (attackType == AttackType.Weapon && attacker?.isActor() == true)
            Cultiway.Content.Visuals.BattleSpellVisualRuntime.Contact(attacker.a, target.Base);
        Color color = Color.Lerp(SkillVfxColor.GetElementColor(composition), Color.white, .22f);
        Vector3 direction = attacker != null && !attacker.isRekt() ? (point - attacker.GetSimPos()).normalized : Vector3.up;
        float size = Mathf.Clamp(target.Base.actor_scale * .8f, .035f, .16f);
        for (int i = 0; i < 3; i++)
        {
            Vector3 velocity = direction * (.3f + i * .12f) + Vector3.forward * (.6f - i * .15f);
            SpatialVisualEffects.Particle(point, velocity, color, size, .14f + i * .025f, gravity: -3f);
        }
    }
}
