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

/// <summary>原光束使用普通来源连接和射线伸展；固定或随动端点不再由旧执行推进。</summary>
internal static class BeamConstructionMigration
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
            !IsBeam(skill.GetComponent<SkillContainer>().Asset))
            return SkillEmissionResult.Unhandled;
        if (emission.Source.isRekt() || !emission.Source.a.isAlive()) return SkillEmissionResult.Rejected;
        if (emission.SpatialSource != null && !emission.SpatialSource.isActor())
            return SkillEmissionResult.Unhandled;
        AbilityDefinition definition = Convert(emission, out GetHitEvent damage);
        if (definition == null) return SkillEmissionResult.Unhandled;
        var target = new ActiveAbilityTarget(emission.Target, emission.TargetPos, attackKingdom: emission.AttackKingdom,
            runtimeData: emission.RuntimeData, worldScope: emission.RuntimeData.WorldScope);
        return ConstructedAbilityService.StartPrepaid(emission.Source.a.GetExtend(), definition, target,
            emission.SourcePos, emission.Payment, damage, spatialSource: emission.SpatialSource) ? SkillEmissionResult.Accepted : SkillEmissionResult.Rejected;
    }

    /// <summary>先核对附加作用，再将实际强度、尺寸、寿命和端点更新展开为可保存资料。</summary>
    /// <param name="emission">原来源的本次发射输入。</param>
    /// <param name="damage">保留原绝对强度及境界的真实伤害。</param>
    private static unsafe AbilityDefinition Convert(SkillEmission emission, out GetHitEvent damage)
    {
        damage = default;
        Entity skill = emission.SkillContainer;
        var container = skill.GetComponent<SkillContainer>();
        if (container.AnimationIndex != 0) return null;
        string trajectory = SkillBlueprintTrajectory.ResolveEffectiveId(skill);
        bool follow = trajectory == SkillTrajectories.TrackingBeam.id;
        if (!follow && trajectory != SkillTrajectories.InstantBeam.id) return null;
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

        // 光束没有弹丸飞行；尺寸、实际强度、持续时间与再次接触间隔各自保留。
        float setupScale = ContactEffectMigration.SetupStrengthScale(skill, out _, out _);
        float lifetime = container.Asset.ResolveRuntimeLifetime(skill,
            container.Asset.ImpactProfile.Lifetime * container.Asset.ImpactTuning.LifetimeMultiplier);
        if (configured && settings.Duration > 0f) lifetime = settings.Duration;
        var animation = container.Asset.GetAnimation(container.AnimationIndex);
        float interval = container.MotionProfile.FrameInterval;
        float appearance = ProjectileConstructionMigration.ClipSeconds(animation.Appearance, interval);
        float recovery = ProjectileConstructionMigration.ClipSeconds(animation.Dissipation, interval);
        float activeEnd = appearance + lifetime;
        if (activeEnd + recovery > 3600f) return null;
        float repeatInterval = container.Asset.ImpactProfile.RepeatHitInterval;
        float lastActive = activeEnd;
        if (repeatInterval > 0f && activeEnd > appearance)
        {
            // 周期光束在退出时点已经收势。接触窗口包含末值，故用该时点之前最后一个可表示时间。
            int bits = *(int*)&activeEnd - 1;
            lastActive = *(float*)&bits;
        }
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
        var operations = new List<ConstructionOperation>
        {
            new() { Kind = ConstructionOperationKind.Ray,
                Ray = new ConstructionRay { Length = 10000f, FollowTarget = follow, TurnRate = 0f, FadeOut = recovery } }
        };
        if (appearance > 0f) operations.Add(new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = appearance });
        return ContactEffectMigration.Bind(new AbilityDefinition
        {
            Id = container.Asset.id, Name = skill.HasName ? skill.Name.value : container.Asset.id.Localize(),
            Lifetime = activeEnd + recovery, Resource = "none", Cost = 0f, Cooldown = 0f, Power = 1f, TargetRange = 10000f,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World,
                Target = ActiveAbilityTargetMode.ObjectOrPoint },
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Source, Follow = follow },
            Steps = recovery > 0f ? new[]
            {
                new ConstructionStep
                {
                    Id = "release",
                    Operation = new ConstructionOperation { Kind = ConstructionOperationKind.Stop, At = activeEnd },
                    Inputs = new() { ["part"] = new() { Node = "root" } }
                }
            } : Array.Empty<ConstructionStep>(),
            Root = new ConstructionNode
            {
                Id = "channel", Form = container.Asset == SkillEntities.SolarBeam ? "solar_ray_luminous" : "spirit_ray_luminous",
                Matter = container.Asset == SkillEntities.SolarBeam ? "yang" : "spirit", State = "energy", Scale = scale,
                ActiveFrom = appearance, ActiveUntil = lastActive,
                Contact = repeatInterval > 0f
                    ? new ConstructionContactPolicy { Mode = ConstructionContactMode.Interval, Interval = repeatInterval }
                    : new ConstructionContactPolicy { Mode = ConstructionContactMode.Once },
                ContactRole = configured && !settings.ContactEnabled ? ConstructionContactRole.Decoration : ConstructionContactRole.Attack,
                Operations = operations.ToArray()
            }
        }, skill, emission.RuntimeData.ResolveEffectScale(), contactForceScale: 1f / (container.Asset.ImpactProfile.DamageMultiplier * container.Asset.ImpactTuning.DamageMultiplier), caster: emission.Source.a.GetExtend(), runtimeData: emission.RuntimeData);
    }

    /// <summary>只接管本批已经保留全部实际作用的两个原光束。</summary>
    /// <param name="asset">原来源资产。</param>
    private static bool IsBeam(SkillEntityAsset asset) => asset == SkillEntities.SpiritBeam || asset == SkillEntities.SolarBeam;
}
