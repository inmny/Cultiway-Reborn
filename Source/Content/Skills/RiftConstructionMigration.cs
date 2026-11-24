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

/// <summary>原裂隙在固定落点张开，定时冲面执行一次范围作用，随后闭合。</summary>
internal static class RiftConstructionMigration
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
            skill.GetComponent<SkillContainer>().SkillEntityAssetID != SkillEntities.Rift.id)
            return SkillEmissionResult.Rejected;
        if (emission.Source.isRekt() || !emission.Source.a.isAlive()) return SkillEmissionResult.Rejected;
        if (emission.SpatialSource != null && !emission.SpatialSource.isActor())
            return SkillEmissionResult.Rejected;
        AbilityDefinition definition = Convert(emission, out GetHitEvent damage);
        if (definition == null) return SkillEmissionResult.Rejected;
        var target = new ActiveAbilityTarget(null, emission.TargetPos, attackKingdom: emission.AttackKingdom,
            runtimeData: emission.RuntimeData, worldScope: emission.RuntimeData.WorldScope);
        return ConstructedAbilityService.StartPrepaid(emission.Source.a.GetExtend(), definition, target,
            emission.SourcePos, emission.Payment, damage, spatialSource: emission.SpatialSource,
            legacyArea: LegacyAreaImpact.From(in emission, "root/burst"),
            reportPreview: emission.ReportPreview) ? SkillEmissionResult.Accepted : SkillEmissionResult.Rejected;
    }

    /// <summary>先核对附加作用，再将实际强度、尺寸、寿命和固定目标展开为可保存资料。</summary>
    /// <param name="emission">原来源的本次发射输入。</param>
    /// <param name="damage">保留原绝对强度及境界的真实伤害。</param>
    private static AbilityDefinition Convert(SkillEmission emission, out GetHitEvent damage)
    {
        damage = default;
        Entity skill = emission.SkillContainer;
        var container = skill.GetComponent<SkillContainer>();
        if (container.AnimationIndex != 0 ||
            SkillBlueprintTrajectory.ResolveEffectiveId(skill) != SkillTrajectories.AppearAtTarget.id) return null;
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

        // 原地显现没有飞行，故加速词条仍不额外缩短寿命或改变伤害时点。
        float setupScale = ContactEffectMigration.SetupStrengthScale(skill, out _, out _);
        // 旧位置冲击在成形结束后立刻请求回收，保留出现与消散的实际帧长。
        SkillEntityAnimation animation = container.Asset.GetAnimation(container.AnimationIndex);
        float frame = container.MotionProfile.FrameInterval;
        float strike = ProjectileConstructionMigration.ClipSeconds(animation.Appearance, frame);
        float closing = ProjectileConstructionMigration.ClipSeconds(animation.Dissipation, frame);
        float lifetime = strike + closing;
        float scale = emission.RuntimeData.ResolveEffectScale() * SkillEffectRadius.ResolveCasterGrowth(emission.Source.a.GetExtend(), skill);
        if (skill.HasComponent<HugeModifier>()) scale *= SkillModifiers.Huge.EffectRadiusMultiplier(skill);
        if (configured) scale *= settings.AreaMultiplier;
        float impactScale = scale * container.Asset.ImpactProfile.EffectRadius * container.Asset.ImpactTuning.EffectRadiusMultiplier / 2f;
        if (scale <= 0f || scale > 100f || impactScale <= 0f || impactScale > 100f) return null;
        float strength = setupScale * emission.Strength * emission.RuntimeData.ResolveEffectScale();
        if (configured) strength *= settings.PowerMultiplier;
        if (skill.TryGetComponent(out EmpowerModifier empower)) strength *= 1f + Mathf.Clamp(empower.SetupBonus, 0f, 10f);
        if (skill.TryGetComponent(out VolleyModifier volley)) strength *= Mathf.Clamp(volley.DamageMultiplier, .05f, 2f);
        if (skill.TryGetComponent(out ComboModifier combo)) strength *= Mathf.Clamp(combo.DamageMultiplier, .05f, 2f);
        strength *= container.Asset.ImpactProfile.DamageMultiplier * container.Asset.ImpactTuning.DamageMultiplier;
        damage = new GetHitEvent
        {
            Damage = strength, Element = NatureConstructionMigration.Element(skill, emission.RuntimeData), AttackType = AttackType.Other,
            AttackerPowerLevel = emission.PowerLevel, DamageOrigin = emission.RuntimeData.DamageOrigin
        };
        damage.BindAttacker(emission.Source);
        var definition = ContactEffectMigration.Bind(new AbilityDefinition
        {
            Id = container.Asset.id, Name = skill.HasName ? skill.Name.value : container.Asset.id.Localize(),
            Lifetime = lifetime, Resource = "none", Cost = 0f, Cooldown = 0f, Power = 1f, TargetRange = 10000f,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World,
                Target = ActiveAbilityTargetMode.Point },
            // 点输入的高度包含原发射抬高量；减去它后，以共用地形采样得到的实际地表为根。
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Target, Height = -emission.TargetPos.z },
            Root = new ConstructionNode
            {
                Id = "origin", Form = "pivot", Matter = "entropy", ContactRole = ConstructionContactRole.Decoration,
                Children = new[]
                {
                    new ConstructionNode
                    {
                        Id = "opening", Form = "rift_opening", Matter = "entropy", Scale = scale,
                        ContactRole = ConstructionContactRole.Decoration,
                        Operations = new[]
                        {
                            new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = strike },
                            new ConstructionOperation { Kind = ConstructionOperationKind.Animate, Clip = "cast", Seconds = lifetime }
                        }
                    },
                    new ConstructionNode
                    {
                        Id = "burst", Form = "rift_burst", Matter = "entropy", Scale = impactScale,
                        AppearAt = strike, ActiveFrom = strike, ActiveUntil = strike + .004f,
                        ContactActorCenter = true,
                        Contact = new ConstructionContactPolicy
                        {
                            Mode = ConstructionContactMode.Pulse, Interval = strike, MaximumPerTarget = 1
                        },
                        ContactRole = configured && !settings.ContactEnabled ? ConstructionContactRole.Decoration : ConstructionContactRole.Attack,
                        Operations = new[]
                        {
                            new ConstructionOperation
                            {
                                Kind = ConstructionOperationKind.Animate, Clip = "dissolve", At = strike, Seconds = closing
                            }
                        }
                    }
                }
            },
            // 闭合时连同整体体量收尽，避免留下高度不变的细长黑线；接触此时已经停止。
            Steps = new[]
            {
                new ConstructionStep
                {
                    Id = "close", Operation = new ConstructionOperation
                    {
                        Kind = ConstructionOperationKind.Grow, At = strike, Seconds = closing, TargetScale = 0f
                    },
                    Inputs = new Dictionary<string, ConstructionOutputReference>
                    { ["part"] = new ConstructionOutputReference { Node = "root/opening" } }
                }
            }
        }, skill, emission.RuntimeData.ResolveEffectScale(), contactForceScale: 1f / (container.Asset.ImpactProfile.DamageMultiplier * container.Asset.ImpactTuning.DamageMultiplier), caster: emission.Source.a.GetExtend(), runtimeData: emission.RuntimeData);
        if (definition.Root.RadialMotion != null) definition.Root.RadialMotion.StartAt = strike;
        return definition;
    }
}
