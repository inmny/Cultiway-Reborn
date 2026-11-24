using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Visuals;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public static class SkillHitResolver
{
    /// <summary>旧迁移脚本的范围观测接口，随脚本清理移除。</summary>
    public static event System.Action<Entity, Vector3, float> AreaResolved;
    /// <summary>一次真实对象接触的结果通知；订阅者只能在离开当前查询后安排额外作用。</summary>
    public static event System.Action<Entity, BaseSimObject> ContactResolved;
    /// <summary>旧迁移脚本的屏障观测接口，随脚本清理移除。</summary>
    public static event System.Action<Entity, Vector2, float> BarrierContactResolved;

    /// <summary>实际刃面命中后应用法宝伤害与元素反馈。</summary>
    internal static void HitArtifact(SkillEntityAsset asset, ref SkillContext context,
        Entity execution, BaseSimObject target)
    {
        if (target == null || target.isRekt()) return;

        SkillVfxElementAsset element = execution.GetComponent<SkillEntity>().VfxElement;
        SkillGroundFx.OnImpact(target.GetSimPos(), element, 0f, isArea: false, sourceObj: context.SourceObj);
        ApplyDamage(asset, ref context, target, element.ImpactSound);
        NotifyContact(execution, target);
    }

    /// <summary>普通命中与身体接触共用的结果通知，不额外造成一次伤害。</summary>
    public static void NotifyContact(Entity skillEntity, BaseSimObject target)
    {
        ContactResolved?.Invoke(skillEntity,target);
    }

    private static void ApplyDamage(SkillEntityAsset asset, ref SkillContext context, BaseSimObject target,
        string contactSound)
    {
        float damage = context.Strength * context.EffectScale;
        if (!asset.DealsBaseDamage || damage <= 0f) return;
        BaseSimObject attacker = context.ResolveSpatialSource();
        if (attacker == null || attacker.isRekt()) attacker = context.SourceObj;
        if (target.isActor())
        {
            var evt = new GetHitEvent
            {
                TargetID = target.a.data.id,
                Damage = damage,
                Element = context.ResolveElement(asset.Element),
                AttackType = AttackType.Other,
                ContactSound = contactSound,
                AttackerPowerLevel = context.PowerLevel,
                DamageOrigin = context.RuntimeData.DamageOrigin,
            };
            evt.BindAttacker(attacker);
            EventSystemHub.Publish(evt);
            return;
        }

        float before = target.getHealth();
        Vector3 point = target.GetSimPos();
        target.b.getHit(damage, pAttacker: context.SourceObj);
        if (target.getHealth() < before) SpatialCombatFeedback.Sound(contactSound, point);
    }
}
