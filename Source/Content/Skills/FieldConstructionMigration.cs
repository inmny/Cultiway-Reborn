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

/// <summary>原持续领域使用普通连接、飞行和间隔接触；实际数量上限由共同受理入口维护。</summary>
internal static class FieldConstructionMigration
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
            !IsField(skill.GetComponent<SkillContainer>().Asset))
            return SkillEmissionResult.Unhandled;
        if (emission.Source.isRekt() || !emission.Source.a.isAlive()) return SkillEmissionResult.Rejected;
        if (emission.SpatialSource != null && emission.SpatialSource != emission.Source ||
            emission.AttackKingdom != null && emission.AttackKingdom != emission.Source.kingdom || emission.RuntimeData.HasElementOverride)
            return SkillEmissionResult.Unhandled;
        AbilityDefinition definition = Convert(emission, out GetHitEvent damage);
        if (definition == null) return SkillEmissionResult.Unhandled;
        bool follow = definition.Binding.Follow;
        var target = new ActiveAbilityTarget(follow ? emission.Target : null, emission.TargetPos, attackKingdom: emission.AttackKingdom,
            runtimeData: emission.RuntimeData, worldScope: emission.RuntimeData.WorldScope);
        return ConstructedAbilityService.StartPrepaid(emission.Source.a.GetExtend(), definition, target,
            emission.SourcePos, emission.Payment, damage, sourceInstance: skill.Id) ? SkillEmissionResult.Accepted : SkillEmissionResult.Rejected;
    }

    /// <summary>先核对附加作用，再将实际强度、尺寸、寿命和固定目标展开为可保存资料。</summary>
    /// <param name="emission">原来源的本次发射输入。</param>
    /// <param name="damage">保留原绝对强度及境界的真实伤害。</param>
    private static AbilityDefinition Convert(SkillEmission emission, out GetHitEvent damage)
    {
        damage = default;
        Entity skill = emission.SkillContainer;
        var container = skill.GetComponent<SkillContainer>();
        string trajectory = SkillBlueprintTrajectory.ResolveEffectiveId(skill);
        bool advance = trajectory == SkillTrajectories.FieldAdvance.id;
        bool follow = trajectory == SkillTrajectories.FieldFollowTarget.id && emission.Target?.isActor() == true;
        if (trajectory != SkillTrajectories.FieldAtTarget.id && trajectory != SkillTrajectories.FieldAdvance.id &&
            trajectory != SkillTrajectories.FieldFollowTarget.id) return null;
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
        if (configured && settings.NatureId != null) return null;

        // 固定及跟随场不因加速改变寿命；推进场保留真实出生点、方向和起步至巡航速度。
        float setupScale = ContactEffectMigration.SetupStrengthScale(skill, out float initialAngle, out float speedScale);
        float lifetime = container.Asset.ResolveRuntimeLifetime(skill,
            container.Asset.ImpactProfile.Lifetime * container.Asset.ImpactTuning.LifetimeMultiplier);
        if (configured && settings.Duration > 0f) lifetime = settings.Duration;
        float scale = emission.RuntimeData.ResolveEffectScale() * SkillEffectRadius.ResolveCasterGrowth(emission.Source.a.GetExtend(), skill);
        if (skill.HasComponent<HugeModifier>()) scale *= SkillModifiers.Huge.EffectRadiusMultiplier(skill);
        if (configured) scale *= settings.AreaMultiplier;
        if (scale <= 0f || scale > 100f) return null;
        float strength = setupScale * emission.Strength * emission.RuntimeData.ResolveEffectScale();
        if (configured) strength *= settings.PowerMultiplier;
        if (skill.TryGetComponent(out EmpowerModifier empower)) strength *= 1f + Mathf.Clamp(empower.SetupBonus, 0f, 10f);
        if (skill.TryGetComponent(out VolleyModifier volley)) strength *= Mathf.Clamp(volley.DamageMultiplier, .05f, 2f);
        if (skill.TryGetComponent(out ComboModifier combo)) strength *= Mathf.Clamp(combo.DamageMultiplier, .05f, 2f);
        float damageMultiplier = container.Asset.ImpactProfile.DamageMultiplier * container.Asset.ImpactTuning.DamageMultiplier;
        damage = new GetHitEvent
        {
            Damage = strength, Element = container.Asset.Element, AttackType = AttackType.Other,
            AttackerPowerLevel = emission.PowerLevel, DamageOrigin = emission.RuntimeData.DamageOrigin
        };
        damage.BindAttacker(emission.Source);
        var operations = new List<ConstructionOperation>
        {
            new() { Kind = ConstructionOperationKind.Animate, Clip = "run" }
        };
        if (advance)
        {
            float haste = skill.TryGetComponent(out HasteModifier h) ? Mathf.Clamp(1f + h.SpeedMultiplier, .1f, 10f) : 1f;
            var motion = container.MotionProfile;
            float speed = motion.ResolveSpeed(Vector2.Distance(emission.SourcePos, emission.TargetPos)) * haste * speedScale * .25f;
            float ramp = Mathf.Max(0f, motion.RampDuration), launch = motion.LaunchMultiplier, cruise = motion.CruiseMultiplier;
            Vector3 direction = emission.TargetPos - emission.SourcePos;
            direction = direction.sqrMagnitude > .000001f ? direction.normalized : Vector3.right;
            direction = Quaternion.AngleAxis(emission.InitialAngleOffsetDegrees + initialAngle, Vector3.forward) * direction;
            operations.Add(new ConstructionOperation
            {
                Kind = ConstructionOperationKind.Fly, WorldVelocity = true, InheritVelocity = false,
                Vector = new[] { direction.x * speed * (ramp > .001f ? launch : cruise),
                    direction.y * speed * (ramp > .001f ? launch : cruise), direction.z * speed * (ramp > .001f ? launch : cruise) }
            });
            if (ramp > .001f)
                for (int i = 1; i <= 8; i++)
                {
                    float at = ramp * i / 8f;
                    if (at >= lifetime) break;
                    float remaining = 1f - i / 8f;
                    operations.Add(new ConstructionOperation { Kind = ConstructionOperationKind.ChangeSpeed,
                        At = at, Speed = speed * (cruise + (launch - cruise) * remaining * remaining) });
                }
        }
        bool entropy = container.Asset == SkillEntities.EntropyField;
        bool wind = container.Asset == SkillEntities.Tornado, yin = container.Asset == SkillEntities.YinField;
        return ContactEffectMigration.Bind(new AbilityDefinition
        {
            Id = container.Asset.id, Name = skill.HasName ? skill.Name.value : container.Asset.id.Localize(),
            Lifetime = lifetime, ConcurrentLimit = container.Asset.ImpactProfile.PersistentLimit,
            Resource = "none", Cost = 0f, Cooldown = 0f, Power = 1f, TargetRange = 10000f,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World,
                Target = follow ? ActiveAbilityTargetMode.ObjectOrPoint : ActiveAbilityTargetMode.Point },
            Binding = new ConstructionBinding { Anchor = advance ? AbilityAnchor.Source : AbilityAnchor.Target,
                Follow = follow, ContactAtDestination = follow, Height = advance || follow ? 0f : -emission.TargetPos.z },
            Root = new ConstructionNode
            {
                Id = "field", Form = wind ? "wind_vortex" : yin ? "yin_field" : entropy ? "entropy_field" : container.Asset == SkillEntities.PoisonPool ? "poison_pool" : "poison_mist",
                Matter = wind ? "wind" : yin ? "yin" : entropy ? "entropy" : "poison",
                State = wind || yin || entropy ? "energy" : container.Asset == SkillEntities.PoisonPool ? "liquid" : "gas", Scale = scale,
                Impact = configured && !settings.ContactEnabled ? null : new ConstructionImpact { DamageScale = damageMultiplier },
                Contact = new ConstructionContactPolicy { Mode = ConstructionContactMode.Interval,
                    Interval = container.Asset.ImpactProfile.RepeatHitInterval },
                ContactRole = configured && !settings.ContactEnabled ? ConstructionContactRole.Decoration : ConstructionContactRole.Attack,
                Operations = operations.ToArray()
            }
        }, skill, emission.RuntimeData.ResolveEffectScale(), caster: emission.Source.a.GetExtend());
    }

    /// <summary>只接管五种没有额外人物状态的原领域，不按名字臆造毒状态或解构作用。</summary>
    /// <param name="asset">真实原来源。</param>
    private static bool IsField(SkillEntityAsset asset) => asset == SkillEntities.PoisonPool ||
        asset == SkillEntities.PoisonMist || asset == SkillEntities.EntropyField ||
        asset == SkillEntities.Tornado || asset == SkillEntities.YinField;
}
