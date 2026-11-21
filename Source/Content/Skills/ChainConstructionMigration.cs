using System;
using System.Collections.Generic;
using Cultiway.Content.Components.Skill;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Settings;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>原连锁雷霆展开为有限普通部位，下一段只由真实接触后的附近目标启动。</summary>
internal static class ChainConstructionMigration
{
    /// <summary>已经完整映射的原词条；额外作用不静默丢弃。</summary>
    private static readonly HashSet<Type> supported = new()
    {
        typeof(Trajectory), typeof(HasteModifier), typeof(HugeModifier), typeof(EmpowerModifier),
        typeof(ProficiencyModifier), typeof(BurstCount), typeof(SalvoCount), typeof(ComboModifier), typeof(VolleyModifier)
    };

    /// <summary>仅接管完整可表达的实际发射，保留原逐发支付及受理失败的撤销。</summary>
    /// <param name="emission">已到达真实发射时点的原步骤。</param>
    internal static SkillEmissionResult Emit(SkillEmission emission)
    {
        Entity skill = emission.SkillContainer;
        if (emission.Sourceless || emission.Source?.isActor() != true ||
            skill.GetComponent<SkillContainer>().Asset != SkillEntities.ChainLightning)
            return SkillEmissionResult.Unhandled;
        if (emission.Source.isRekt() || !emission.Source.a.isAlive()) return SkillEmissionResult.Rejected;
        if (emission.SpatialSource != null && !emission.SpatialSource.isActor())
            return SkillEmissionResult.Unhandled;
        AbilityDefinition definition = Convert(emission, out GetHitEvent damage);
        if (definition == null) return SkillEmissionResult.Unhandled;
        var target = new ActiveAbilityTarget(emission.Target, emission.TargetPos, attackKingdom: emission.AttackKingdom,
            runtimeData: emission.RuntimeData, worldScope: emission.RuntimeData.WorldScope);
        return ConstructedAbilityService.StartPrepaid(emission.Source.a.GetExtend(), definition, target,
            emission.SourcePos, emission.Payment, damage, spatialSource: emission.SpatialSource,
            groundContact: ConstructedAbilityService.LegacyContactElement(in emission),
            buildingContact: LegacyBuildingContact.From(in emission))
            ? SkillEmissionResult.Accepted : SkillEmissionResult.Rejected;
    }

    /// <summary>核对附加作用后，将原跳转上限有限展开为真实接触开始的普通部位。</summary>
    /// <param name="emission">原来源的本次发射输入。</param>
    /// <param name="damage">保留原绝对强度及境界的真实伤害。</param>
    private static AbilityDefinition Convert(SkillEmission emission, out GetHitEvent damage)
    {
        damage = default;
        Entity skill = emission.SkillContainer;
        var container = skill.GetComponent<SkillContainer>();
        if (container.AnimationIndex != 0) return null;
        string trajectory = SkillBlueprintTrajectory.ResolveEffectiveId(skill);
        if (trajectory != SkillTrajectories.ChainTargets.id) return null;
        var callbacks = new List<Delegate>();
        foreach (Type type in skill.GetComponentTypes())
        {
            if (!typeof(IModifier).IsAssignableFrom(type)) continue;
            if (!supported.Contains(type) && !ContactEffectMigration.Supports(type)) return null;
            var modifier = (IModifier)skill.GetComponent(type);
            if (modifier.ModifierAsset.OnSetup != null) callbacks.AddRange(modifier.ModifierAsset.OnSetup.GetInvocationList());
        }
        Delegate[] setup = container.OnSetup?.GetInvocationList() ?? Array.Empty<Delegate>();
        if (setup.Length != callbacks.Count) return null;
        foreach (Delegate callback in setup) if (!callbacks.Contains(callback)) return null;
        if (container.Asset.Effects.Count != 0 || container.EffectPipeline?.Effects.Count > 0 ||
            !ContactEffectMigration.MatchesTravelCallbacks(skill) || !ContactEffectMigration.MatchesCallbacks(skill)) return null;
        bool configured = skill.TryGetComponent(out SkillSettings settings);
        if (!NatureConstructionMigration.Supports(skill)) return null;

        // 原连锁命中立即结束旧实体，连接只显示0.14秒；显式持续时间仍采用已保存设置。
        float setupScale = ContactEffectMigration.SetupStrengthScale(skill, out _, out _);
        float lifetime = configured && settings.Duration > 0f ? settings.Duration : .14f;
        var animation = container.Asset.GetAnimation(container.AnimationIndex);
        float interval = container.MotionProfile.FrameInterval;
        float appearance = ProjectileConstructionMigration.ClipSeconds(animation.Appearance, interval);
        float scale = emission.RuntimeData.ResolveEffectScale() * SkillEffectRadius.ResolveCasterGrowth(emission.Source.a.GetExtend(), skill);
        if (skill.HasComponent<HugeModifier>()) scale *= SkillModifiers.Huge.EffectRadiusMultiplier(skill);
        if (configured) scale *= settings.AreaMultiplier;
        if (scale <= 0f || scale > 100f) return null;
        float strength = setupScale * emission.Strength * emission.RuntimeData.ResolveEffectScale();
        if (configured) strength *= settings.PowerMultiplier;
        if (skill.TryGetComponent(out EmpowerModifier empower)) strength *= 1f + Mathf.Clamp(empower.SetupBonus, 0f, 10f);
        if (skill.TryGetComponent(out VolleyModifier volley)) strength *= Mathf.Clamp(volley.DamageMultiplier, .05f, 2f);
        strength *= container.Asset.ImpactProfile.DamageMultiplier * container.Asset.ImpactTuning.DamageMultiplier;
        damage = new GetHitEvent
        {
            Damage = strength, Element = NatureConstructionMigration.Element(skill, emission.RuntimeData), AttackType = AttackType.Other,
            AttackerPowerLevel = emission.PowerLevel, DamageOrigin = emission.RuntimeData.DamageOrigin
        };
        damage.BindAttacker(emission.Source);
        var profile = container.Asset.ImpactProfile;
        float radius = SkillEffectRadius.ResolveForCaster(emission.Source.a.GetExtend(), skill, profile.JumpRadius) * emission.RuntimeData.ResolveEffectScale();
        bool enabled = !configured || settings.ContactEnabled;
        float recovery = enabled ? ProjectileConstructionMigration.ClipSeconds(animation.Dissipation, interval) : 0f;
        if (appearance + lifetime + recovery > 3600f) return null;
        ConstructionNode root = null, previous = null;
        for (int i = 0; i < (enabled ? profile.MaxTargets : 1); i++)
        {
            var part = new ConstructionNode
            {
                Id = "link" + i, Form = "thunder_channel", Matter = "lightning", State = "energy", Scale = scale,
                ContactGroup = "chain", ContactRole = enabled ? ConstructionContactRole.Attack : ConstructionContactRole.Decoration,
                ActiveFrom = i == 0 ? appearance : 0f,
                DisappearAt = i == 0 ? appearance + lifetime : lifetime,
                Impact = enabled ? new ConstructionImpact { SelectedTargetOnly = emission.Target != null || i > 0 } : null,
                Activation = i == 0 ? null : new ConstructionActivation
                {
                    Target = new ConstructionContactTarget { Radius = radius, ForceMultiplier = profile.JumpDamageFalloff }
                },
                Operations = new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Ray,
                    Ray = new ConstructionRay { Length = 10000f, FollowTarget = false, TurnRate = 0f } } }
            };
            if (previous != null) previous.Children = new[] { part };
            else root = part;
            previous = part;
        }
        if (appearance > 0f)
            root.Operations = new[] { root.Operations[0],
                new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = appearance } };
        if (recovery > 0f)
        {
            var release = new ConstructionNode
            {
                Id = "release", Form = "lightning_contact", Matter = "lightning", State = "energy",
                Scale = scale, Volume = 0f, DisappearAt = recovery, ContactRole = ConstructionContactRole.Decoration,
                Activation = new ConstructionActivation
                {
                    Trigger = ConstructionChangeTrigger.ActorContact, Placement = ConstructionEventPlacement.Event
                },
                Operations = new[] { new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Animate, Clip = "release", Seconds = recovery
                } }
            };
            var branches = root.Children ?? Array.Empty<ConstructionNode>();
            root.Children = new List<ConstructionNode>(branches) { release }.ToArray();
        }
        return ContactEffectMigration.Bind(new AbilityDefinition
        {
            Id = container.Asset.id, Name = skill.HasName ? skill.Name.value : container.Asset.id.Localize(),
            Lifetime = appearance + lifetime + recovery, Resource = "none", Cost = 0f, Cooldown = 0f, Power = 1f, TargetRange = 10000f,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World,
                Target = ActiveAbilityTargetMode.ObjectOrPoint },
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Source, Follow = false }, Root = root
        }, skill, emission.RuntimeData.ResolveEffectScale(), contactForceScale: 1f / (container.Asset.ImpactProfile.DamageMultiplier * container.Asset.ImpactTuning.DamageMultiplier), caster: emission.Source.a.GetExtend(), runtimeData: emission.RuntimeData);
    }
}
