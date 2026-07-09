using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3;

public static class SkillHitResolver
{
    public static OnObjCollision Single(SkillEntityAsset asset, bool recycleOnHit, bool continueAfterHit)
    {
        return (ref SkillContext context, Entity skillContainer, Entity skillEntity, BaseSimObject target) =>
        {
            HitTarget(asset, ref context, skillContainer, skillEntity, target);
            if (recycleOnHit)
            {
                ModClass.I.CommandBuffer.AddTag<TagRecycle>(skillEntity.Id);
            }

            return continueAfterHit;
        };
    }

    public static OnObjCollision Area(SkillEntityAsset asset, float radius, bool recycleOnHit,
        bool continueAfterHit = false)
    {
        return (ref SkillContext context, Entity skillContainer, Entity skillEntity, BaseSimObject target) =>
        {
            var position = skillEntity.GetComponent<Position>().value;
            var vfxElement = skillEntity.GetComponent<SkillEntity>().VfxElement;
            SkillGroundFx.OnImpact(position, vfxElement, radius, isArea: true);

            foreach (var obj in SkillUtils.IterEnemyInSphere(position, radius, context.SourceObj,
                         context.AttackKingdom))
            {
                HitTarget(asset, ref context, skillContainer, skillEntity, obj);
            }

            if (recycleOnHit)
            {
                ModClass.I.CommandBuffer.AddTag<TagRecycle>(skillEntity.Id);
            }

            return continueAfterHit;
        };
    }

    public static void HitTarget(SkillEntityAsset asset, ref SkillContext context, Entity skillContainer,
        Entity skillEntity, BaseSimObject target)
    {
        if (target == null || target.isRekt()) return;

        var vfxElement = skillEntity.GetComponent<SkillEntity>().VfxElement;
        SkillGroundFx.OnImpact(target.GetSimPos(), vfxElement, 0, isArea: false);
        ApplyDamage(asset, ref context, target);
        InvokeOnEffect(skillContainer, skillEntity, target);
    }

    private static void ApplyDamage(SkillEntityAsset asset, ref SkillContext context, BaseSimObject target)
    {
        var attacker = context.SourceObj;
        if (target.isActor())
        {
            EventSystemHub.Publish(new GetHitEvent
            {
                TargetID = target.a.data.id,
                Damage = context.Strength,
                Element = asset.Element,
                Attacker = attacker,
                AttackerPowerLevel = context.PowerLevel
            });
            return;
        }

        target.b.getHit(context.Strength, pAttacker: attacker);
    }

    private static void InvokeOnEffect(Entity skillContainer, Entity skillEntity, BaseSimObject target)
    {
        if (skillContainer.IsNull) return;
        skillContainer.GetComponent<SkillContainer>().OnEffectObj?.Invoke(skillEntity, target);
    }
}
