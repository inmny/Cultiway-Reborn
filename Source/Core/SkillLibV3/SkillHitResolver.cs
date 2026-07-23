using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public static class SkillHitResolver
{
    public static bool ResolveProfile(ref SkillContext context, Entity skillContainer, Entity skillEntity,
        BaseSimObject target)
    {
        SkillEntityAsset asset = skillEntity.GetComponent<SkillEntity>().Asset;
        SkillImpactProfileAsset profile = asset.ImpactProfile;
        float damageMultiplier = profile.DamageMultiplier * asset.ImpactTuning.DamageMultiplier;
        switch (profile.Kind)
        {
            case SkillImpactKind.Explosion:
            case SkillImpactKind.HeavySkyfall:
            case SkillImpactKind.GroundManifest:
                ResolveArea(asset, profile, ref context, skillContainer, skillEntity);
                break;
            case SkillImpactKind.Chain:
                ResolveChain(asset, profile, ref context, skillContainer, skillEntity, target);
                break;
            case SkillImpactKind.Wall:
            case SkillImpactKind.Shield:
                ApplyContactForce(asset, ref context, skillEntity, target);
                if (profile.ContactDamage || asset.ImpactTuning.ContactDamage)
                {
                    HitTarget(asset, ref context, skillContainer, skillEntity, target, true,
                        damageMultiplier, applyGroundFx: false);
                }
                break;
            default:
                HitTarget(asset, ref context, skillContainer, skillEntity, target, true,
                    damageMultiplier);
                break;
        }

        if (profile.RecycleOnHit)
        {
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(skillEntity.Id);
        }
        return profile.ContinueAfterHit;
    }

    public static void ResolvePositionImpact(Entity skillEntity)
    {
        ref SkillPositionImpactState state = ref skillEntity.GetComponent<SkillPositionImpactState>();
        if (!state.Requested || state.Resolved) return;
        state.Resolved = true;

        ref SkillContext context = ref skillEntity.GetComponent<SkillContext>();
        SkillEntity skill = skillEntity.GetComponent<SkillEntity>();
        SkillEntityAsset asset = skill.Asset;
        SkillImpactProfileAsset profile = asset.ImpactProfile;
        ResolveArea(asset, profile, ref context, skill.SkillContainer, skillEntity);
        if (profile.RecycleOnHit && !skillEntity.Tags.Has<TagRecycle>())
        {
            skillEntity.AddTag<TagRecycle>();
        }
    }

    private static void ApplyContactForce(SkillEntityAsset asset, ref SkillContext context, Entity skillEntity,
        BaseSimObject target)
    {
        float force = asset.ImpactTuning.ContactForce;
        if (force <= 0f || !target.isActor() || !context.SourceObj.isActor()) return;

        Vector2 center = skillEntity.GetComponent<Position>().v2;
        Vector2 direction = target.current_position - center;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
        direction.Normalize();
        target.a.GetExtend().GetForce(context.SourceObj.a, direction.x * force, direction.y * force, 0f);
    }

    public static OnObjCollision Single(SkillEntityAsset asset, bool recycleOnHit, bool continueAfterHit)
    {
        return (ref SkillContext context, Entity skillContainer, Entity skillEntity, BaseSimObject target) =>
        {
            HitTarget(asset, ref context, skillContainer, skillEntity, target, playImpact: true);
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
            var effectRadius = SkillEffectRadius.Resolve(skillEntity, radius);
            if (TryBeginImpactFeedback(skillEntity, vfxElement))
            {
                vfxElement.PlayImpactSound(position, isArea: true);
            }
            SkillGroundFx.OnImpact(position, vfxElement, effectRadius, isArea: true, sourceObj: context.SourceObj);

            foreach (var obj in SkillUtils.IterEnemyInSphere(position, effectRadius, context.SourceObj,
                         context.AttackKingdom))
            {
                HitTarget(asset, ref context, skillContainer, skillEntity, obj, playImpact: false);
            }

            if (recycleOnHit)
            {
                ModClass.I.CommandBuffer.AddTag<TagRecycle>(skillEntity.Id);
            }

            return continueAfterHit;
        };
    }

    public static void HitTarget(SkillEntityAsset asset, ref SkillContext context, Entity skillContainer,
        Entity skillEntity, BaseSimObject target, bool playImpact, float damageMultiplier = 1f,
        bool applyGroundFx = true)
    {
        if (target == null || target.isRekt()) return;

        var vfxElement = skillEntity.GetComponent<SkillEntity>().VfxElement;
        if (playImpact && TryBeginImpactFeedback(skillEntity, vfxElement))
        {
            vfxElement.PlayImpactSound(target.GetSimPos(), isArea: false);
        }
        if (applyGroundFx)
        {
            SkillGroundFx.OnImpact(target.GetSimPos(), vfxElement, 0, isArea: false, sourceObj: context.SourceObj);
        }
        ApplyDamage(asset, ref context, target, damageMultiplier);
        InvokeOnEffect(skillContainer, skillEntity, target);
    }

    private static void ResolveArea(SkillEntityAsset asset, SkillImpactProfileAsset profile,
        ref SkillContext context, Entity skillContainer, Entity skillEntity)
    {
        if (skillEntity.HasComponent<SkillPositionImpactState>())
        {
            ref SkillPositionImpactState state = ref skillEntity.GetComponent<SkillPositionImpactState>();
            state.Resolved = true;
        }
        Vector3 position = skillEntity.GetComponent<Position>().value;
        SkillVfxElementAsset vfxElement = skillEntity.GetComponent<SkillEntity>().VfxElement;
        float effectRadius = SkillEffectRadius.Resolve(
            skillEntity, profile.EffectRadius * asset.ImpactTuning.EffectRadiusMultiplier);
        if (TryBeginImpactFeedback(skillEntity, vfxElement))
        {
            vfxElement.PlayImpactSound(position, isArea: true);
        }
        SkillGroundFx.OnImpact(position, vfxElement, effectRadius, isArea: true, sourceObj: context.SourceObj);

        foreach (BaseSimObject obj in SkillUtils.IterEnemyInSphere(
                     position, effectRadius, context.SourceObj, context.AttackKingdom))
        {
            HitTarget(asset, ref context, skillContainer, skillEntity, obj, false,
                profile.DamageMultiplier * asset.ImpactTuning.DamageMultiplier,
                applyGroundFx: false);
        }
    }

    private static void ResolveChain(SkillEntityAsset asset, SkillImpactProfileAsset profile,
        ref SkillContext context, Entity skillContainer, Entity skillEntity, BaseSimObject firstTarget)
    {
        var visited = new HashSet<long>();
        BaseSimObject current = firstTarget;
        Vector2 previousPosition = context.SourceObj.GetSimPos();
        float damageMultiplier = profile.DamageMultiplier * asset.ImpactTuning.DamageMultiplier;
        float jumpRadius = SkillEffectRadius.Resolve(skillEntity, profile.JumpRadius);
        for (int i = 0; i < profile.MaxTargets && current != null && !current.isRekt(); i++)
        {
            SpawnChainLink(skillEntity, previousPosition, current.current_position);
            visited.Add(GetTargetKey(current));
            HitTarget(asset, ref context, skillContainer, skillEntity, current, true, damageMultiplier);

            BaseSimObject next = null;
            float nearestSqrDistance = float.MaxValue;
            Vector2 currentPosition = current.current_position;
            foreach (BaseSimObject candidate in SkillUtils.IterEnemyInSphere(
                         currentPosition, jumpRadius, context.SourceObj, context.AttackKingdom))
            {
                if (candidate == null || candidate.isRekt() || visited.Contains(GetTargetKey(candidate))) continue;
                float sqrDistance = (candidate.current_position - currentPosition).sqrMagnitude;
                if (sqrDistance >= nearestSqrDistance) continue;
                nearestSqrDistance = sqrDistance;
                next = candidate;
            }

            previousPosition = currentPosition;
            current = next;
            damageMultiplier *= profile.JumpDamageFalloff;
        }
    }

    private static void SpawnChainLink(Entity skillEntity, Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.01f) return;

        var position = new Vector3((start.x + end.x) * 0.5f, (start.y + end.y) * 0.5f);
        var direction = new Vector3(delta.x / length, delta.y / length);
        Sprite[] frames = skillEntity.GetComponent<AnimData>().frames;
        Color tint = skillEntity.GetComponent<AnimTint>().Value;
        Entity link = ModClass.I.SkillV3.SpawnAnim(
            frames,
            position,
            direction,
            tint: tint,
            frameInterval: 0.04f,
            lifeTime: 0.14f);
        Vector3 baseScale = skillEntity.GetComponent<Scale>().value;
        link.GetComponent<Scale>().value = SkillLinearVisual.ResolveScale(skillEntity, length, baseScale);
    }

    private static bool TryBeginImpactFeedback(Entity skillEntity, SkillVfxElementAsset element)
    {
        ref var state = ref skillEntity.GetComponent<SkillImpactFeedbackState>();
        var elapsed = skillEntity.GetComponent<AliveTimer>().value;
        if (elapsed < state.NextAllowedTime) return false;

        state.NextAllowedTime = elapsed + element.ImpactFeedbackInterval;
        return true;
    }

    private static void ApplyDamage(SkillEntityAsset asset, ref SkillContext context, BaseSimObject target,
        float damageMultiplier)
    {
        var attacker = context.SourceObj;
        if (target.isActor())
        {
            EventSystemHub.Publish(new GetHitEvent
            {
                TargetID = target.a.data.id,
                Damage = context.Strength * damageMultiplier,
                Element = asset.Element,
                Attacker = attacker,
                AttackerPowerLevel = context.PowerLevel
            });
            return;
        }

        target.b.getHit(context.Strength * damageMultiplier, pAttacker: attacker);
    }

    private static long GetTargetKey(BaseSimObject target)
    {
        return unchecked((target.getID() << 1) | (target.isActor() ? 0L : 1L));
    }

    private static void InvokeOnEffect(Entity skillContainer, Entity skillEntity, BaseSimObject target)
    {
        if (skillContainer.IsNull) return;
        skillContainer.GetComponent<SkillContainer>().OnEffectObj?.Invoke(skillEntity, target);
    }
}
