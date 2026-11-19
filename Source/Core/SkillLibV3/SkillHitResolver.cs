using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Visuals;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public static class SkillHitResolver
{
    /// <summary>实际范围作用已经成立的位置和半径；仅用于结果表现，不再次结算。</summary>
    public static event System.Action<Entity, Vector3, float> AreaResolved;
    /// <summary>一次真实对象接触的结果通知；订阅者只能在离开当前查询后安排额外作用。</summary>
    public static event System.Action<Entity, BaseSimObject> ContactResolved;
    /// <summary>屏障真实吸收了来袭伤害，仅供显示与调试读取。</summary>
    public static event System.Action<Entity, Vector2, float> BarrierContactResolved;

    /// <summary>转发已经发生的屏障接触，不在通知内再次结算。</summary>
    /// <param name="barrier">实际屏障。</param><param name="point">实际拦截处。</param><param name="damage">吸收的来袭强度。</param>
    internal static void NotifyBarrierContact(Entity barrier, Vector2 point, float damage) => BarrierContactResolved?.Invoke(barrier, point, damage);

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
            SkillWorldVisualService.BeginArea(skillEntity, position, effectRadius);
            AreaResolved?.Invoke(skillEntity, position, effectRadius);
            if (asset.WorldVisualProfile?.SuppressDefaultGroundImpact != true)
            {
                SkillGroundFx.OnImpact(position, vfxElement, effectRadius, isArea: true,
                    sourceObj: context.SourceObj);
            }

            foreach (var obj in SkillUtils.IterEnemyInSphere(position, effectRadius, context.SourceObj,
                         context.ResolveAttackKingdom()))
            {
                HitTarget(asset, ref context, skillContainer, skillEntity, obj, playImpact: true);
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
        bool applyGroundFx = true, bool applyStructuredEffects = true)
    {
        if (target == null || target.isRekt()) return;

        var vfxElement = skillEntity.GetComponent<SkillEntity>().VfxElement;
        if (applyGroundFx && asset.WorldVisualProfile?.SuppressDefaultGroundImpact != true)
        {
            SkillGroundFx.OnImpact(target.GetSimPos(), vfxElement, 0, isArea: false, sourceObj: context.SourceObj);
        }
        ApplyDamage(asset, ref context, target, damageMultiplier, playImpact ? vfxElement.ImpactSound : null);
        InvokeOnEffect(skillContainer, skillEntity, target);
        if (applyStructuredEffects) SkillEffectResolver.ResolveDirectImpact(skillEntity, target);
        NotifyContact(skillEntity,target);
    }

    /// <summary>普通命中与身体接触共用的结果通知，不额外造成一次伤害。</summary>
    public static void NotifyContact(Entity skillEntity, BaseSimObject target)
    {
        ContactResolved?.Invoke(skillEntity,target);
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
        float effectRadius = SkillEffectRadius.Effect(skillEntity);
        // 明确抵达地面的轨迹才放落地声。空中展开仍可伤到人，由实际受击发声。
        WorldTile tile = SkillGroundFx.GetTile(position);
        bool landed = skillEntity.HasComponent<SkillPositionImpactState>() &&
                      skillEntity.GetComponent<SkillPositionImpactState>().Requested && tile != null &&
                      position.z <= .55f;
        if (landed && TryBeginImpactFeedback(skillEntity, vfxElement))
            SpatialCombatFeedback.Sound(vfxElement.ImpactSound, position);
        SkillWorldVisualService.BeginArea(skillEntity, position, effectRadius);
        AreaResolved?.Invoke(skillEntity, position, effectRadius);
        if (asset.WorldVisualProfile?.SuppressDefaultGroundImpact != true)
        {
            SkillGroundFx.OnImpact(position, vfxElement, effectRadius, isArea: true, sourceObj: context.SourceObj);
        }

        SkillEffectResolver.ResolveAreaImpact(skillEntity, position, effectRadius);

        foreach (BaseSimObject obj in SkillUtils.IterEnemyInSphere(
                     position, effectRadius, context.SourceObj, context.ResolveAttackKingdom()))
        {
            HitTarget(asset, ref context, skillContainer, skillEntity, obj, !landed,
                profile.DamageMultiplier * asset.ImpactTuning.DamageMultiplier,
                applyGroundFx: false,
                applyStructuredEffects: false);
        }
    }

    private static void ResolveChain(SkillEntityAsset asset, SkillImpactProfileAsset profile,
        ref SkillContext context, Entity skillContainer, Entity skillEntity, BaseSimObject firstTarget)
    {
        var visited = new HashSet<long>();
        BaseSimObject current = firstTarget;
        BaseSimObject spatialSource = context.ResolveSpatialSource();
        Vector3 previousPosition = spatialSource?.GetSimPos() ?? skillEntity.GetComponent<Position>().value;
        float damageMultiplier = profile.DamageMultiplier * asset.ImpactTuning.DamageMultiplier;
        float jumpRadius = SkillEffectRadius.Resolve(skillEntity, profile.JumpRadius);
        for (int i = 0; i < profile.MaxTargets && current != null && !current.isRekt(); i++)
        {
            SpawnChainLink(skillEntity, previousPosition, current.GetSimPos());
            visited.Add(GetTargetKey(current));
            HitTarget(asset, ref context, skillContainer, skillEntity, current, true, damageMultiplier);

            BaseSimObject next = null;
            float nearestSqrDistance = float.MaxValue;
            Vector2 currentPosition = current.current_position;
            foreach (BaseSimObject candidate in SkillUtils.IterEnemyInSphere(
                         currentPosition, jumpRadius, context.SourceObj, context.ResolveAttackKingdom()))
            {
                if (candidate == null || candidate.isRekt() || visited.Contains(GetTargetKey(candidate))) continue;
                float sqrDistance = (candidate.current_position - currentPosition).sqrMagnitude;
                if (sqrDistance >= nearestSqrDistance) continue;
                nearestSqrDistance = sqrDistance;
                next = candidate;
            }

            previousPosition = current.GetSimPos();
            current = next;
            damageMultiplier *= profile.JumpDamageFalloff;
        }
    }

    /// <summary>为本次实际连锁连接两端，表现不创建额外攻击实体。</summary>
    /// <param name="skillEntity">实际连锁技能。</param><param name="start">上个接触点。</param><param name="end">本次目标。</param>
    private static void SpawnChainLink(Entity skillEntity, Vector3 start, Vector3 end)
    {
        Color color = skillEntity.GetComponent<AnimTint>().Value;
        Cultiway.Core.Visuals.SpatialVisualEffects.Link(start, end, color, .035f, .14f);
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
        float damageMultiplier, string contactSound)
    {
        float damage = context.Strength * context.EffectScale * damageMultiplier;
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
