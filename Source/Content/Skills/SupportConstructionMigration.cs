using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components.Skill;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Settings;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>三类原支持来源转换成普通真实接触，仍使用共享恢复和净化规则。</summary>
internal static class SupportConstructionMigration
{
    /// <summary>本批可完整保留的共用词条，必选支持词条另按来源核对。</summary>
    private static readonly HashSet<Type> supported = new()
    {
        typeof(Trajectory), typeof(HasteModifier), typeof(HugeModifier), typeof(EmpowerModifier),
        typeof(ProficiencyModifier), typeof(BurstCount), typeof(SalvoCount), typeof(ComboModifier), typeof(VolleyModifier)
    };

    /// <summary>只接管已完整映射的来源，受理失败沿用原支付撤销。</summary>
    /// <param name="emission">到达真实发射时点的已学来源。</param>
    internal static SkillEmissionResult Emit(SkillEmission emission)
    {
        var asset = emission.SkillContainer.GetComponent<SkillContainer>().Asset;
        if (emission.Sourceless || emission.Source?.isActor() != true ||
            asset != SkillEntities.HealingLight && asset != SkillEntities.RejuvenationField && asset != SkillEntities.PurificationWave)
            return SkillEmissionResult.Unhandled;
        if (emission.Source.isRekt() || !emission.Source.a.isAlive()) return SkillEmissionResult.Rejected;
        if (emission.SpatialSource != null && emission.SpatialSource != emission.Source ||
            emission.AttackKingdom != null && emission.AttackKingdom != emission.Source.kingdom || emission.RuntimeData.HasElementOverride)
            return SkillEmissionResult.Unhandled;
        AbilityDefinition definition = Convert(emission);
        if (definition == null) return SkillEmissionResult.Unhandled;
        var target = new ActiveAbilityTarget(definition.Use.Target == ActiveAbilityTargetMode.Object ? emission.Target : null,
            emission.TargetPos, attackKingdom: emission.AttackKingdom, runtimeData: emission.RuntimeData, worldScope: emission.RuntimeData.WorldScope);
        // 载体有明确交锋余量，人物伤害始终为零；恢复量独立保存，不拿攻击力驱动治疗。
        var carrier = new GetHitEvent { Damage = 1f, Element = asset.Element, AttackerPowerLevel = emission.PowerLevel,
            AttackType = AttackType.Other, DamageOrigin = emission.RuntimeData.DamageOrigin };
        carrier.BindAttacker(emission.Source);
        return ConstructedAbilityService.StartPrepaid(emission.Source.a.GetExtend(), definition, target,
            emission.SourcePos, emission.Payment, carrier, sourceInstance: emission.SkillContainer.Id)
            ? SkillEmissionResult.Accepted : SkillEmissionResult.Rejected;
    }

    /// <summary>核对真实词条及委托，再展开运动、尺寸、时点与独立恢复资料。</summary>
    /// <param name="emission">原来源本次发射输入。</param>
    private static AbilityDefinition Convert(SkillEmission emission)
    {
        Entity skill = emission.SkillContainer;
        var container = skill.GetComponent<SkillContainer>();
        var asset = container.Asset;
        bool healing = asset == SkillEntities.HealingLight, rejuvenation = asset == SkillEntities.RejuvenationField;
        string trajectory = SkillBlueprintTrajectory.ResolveEffectiveId(skill);
        if (trajectory != (healing ? SkillTrajectories.TowardsTarget.id : rejuvenation ? SkillTrajectories.FieldAtTarget.id : SkillTrajectories.AppearAtTarget.id) ||
            healing && emission.Target?.isActor() != true) return null;
        Type required = healing ? typeof(HealingModifier) : rejuvenation ? typeof(RejuvenationModifier) : typeof(PurificationModifier);
        SkillModifierAsset modifierAsset = healing ? SkillModifiers.Healing : rejuvenation ? SkillModifiers.Rejuvenation : SkillModifiers.Purification;
        var callbacks = new List<Delegate>();
        bool found = false;
        foreach (Type type in skill.GetComponentTypes())
        {
            if (!typeof(IModifier).IsAssignableFrom(type)) continue;
            if (type != required && !supported.Contains(type)) return null;
            if (type == required) found = true;
            var modifier = (IModifier)skill.GetComponent(type);
            if (modifier.ModifierAsset.OnSetup != null) callbacks.AddRange(modifier.ModifierAsset.OnSetup.GetInvocationList());
        }
        Delegate[] setup = container.OnSetup?.GetInvocationList() ?? Array.Empty<Delegate>();
        if (!found || setup.Length != callbacks.Count || setup.Any(callback => !callbacks.Contains(callback)) ||
            container.OnTravel != null || container.OnEffectObj != null || asset.Effects.Count != 0 ||
            container.EffectPipeline == null || !container.EffectPipeline.Effects.SequenceEqual(modifierAsset.Effects)) return null;
        bool configured = skill.TryGetComponent(out SkillSettings settings);
        if (configured && settings.NatureId != null) return null;
        float lifetime = asset.ResolveRuntimeLifetime(skill, asset.ImpactProfile.Lifetime * asset.ImpactTuning.LifetimeMultiplier);
        if (configured && settings.Duration > 0f) lifetime = settings.Duration;
        float effectScale = emission.RuntimeData.ResolveEffectScale();
        float scale = effectScale * SkillEffectRadius.ResolveCasterGrowth(emission.Source.a.GetExtend(), skill);
        if (skill.HasComponent<HugeModifier>()) scale *= SkillModifiers.Huge.EffectRadiusMultiplier(skill);
        if (configured) scale *= settings.AreaMultiplier;
        if (scale <= 0f || scale > 100f || lifetime <= 0f || lifetime > 3600f) return null;
        var restoration = new ConstructionRestoration();
        if (healing) restoration.Health = skill.GetComponent<HealingModifier>().Amount * effectScale;
        else if (rejuvenation) restoration.HealthPerSecond = skill.GetComponent<RejuvenationModifier>().HealPerSecond * effectScale;
        else restoration.CleanseCount = skill.GetComponent<PurificationModifier>().MaxStatuses;
        var operations = new List<ConstructionOperation> { new() { Kind = ConstructionOperationKind.Animate, Clip = "run", Seconds = healing || rejuvenation ? 0f : lifetime } };
        // 整体展开使用普通生长步骤，网格和接触端点共享尺寸，不向骨架动作写入未支持的缩放。
        if (!healing && !rejuvenation)
            operations.Add(new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = lifetime });
        if (healing)
        {
            var motion = container.MotionProfile;
            float haste = skill.TryGetComponent(out HasteModifier h) ? Mathf.Clamp(1f + h.SpeedMultiplier, .1f, 10f) : 1f;
            float speed = motion.ResolveSpeed(Vector2.Distance(emission.SourcePos, emission.TargetPos)) * haste;
            float ramp = Mathf.Max(0f, motion.RampDuration);
            Vector3 direction = emission.TargetPos - emission.SourcePos;
            direction = Quaternion.AngleAxis(emission.InitialAngleOffsetDegrees, Vector3.forward) * (direction.sqrMagnitude > .000001f ? direction.normalized : Vector3.right);
            operations.Add(new ConstructionOperation
            {
                Kind = ConstructionOperationKind.Fly, WorldVelocity = true, InheritVelocity = false,
                Vector = XYZ(direction * speed * (ramp > .001f ? motion.LaunchMultiplier : motion.CruiseMultiplier)),
                TurnRate = motion.TurnRate * haste,
                Goal = new ConstructionFlightGoal { Follow = true, Radius = .01f, Arrival = ConstructionFlightArrival.End }
            });
            if (ramp > .001f)
                for (int i = 1; i <= 8; i++)
                {
                    float at = ramp * i / 8f;
                    if (at >= lifetime) break;
                    float remaining = 1f - i / 8f;
                    operations.Add(new ConstructionOperation { Kind = ConstructionOperationKind.ChangeSpeed, At = at,
                        Speed = speed * (motion.CruiseMultiplier + (motion.LaunchMultiplier - motion.CruiseMultiplier) * remaining * remaining) });
                }
        }
        return new AbilityDefinition
        {
            Id = asset.id, Name = skill.HasName ? skill.Name.value : asset.id.Localize(), Lifetime = lifetime,
            ConcurrentLimit = asset.ImpactProfile.PersistentLimit, Resource = "none", Cost = 0f, Cooldown = 0f, Power = 1f, TargetRange = 10000f,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World,
                Target = healing ? ActiveAbilityTargetMode.Object : ActiveAbilityTargetMode.Point },
            Binding = new ConstructionBinding { Anchor = healing ? AbilityAnchor.Source : AbilityAnchor.Target,
                Height = healing ? 0f : -emission.TargetPos.z },
            Impact = new ConstructionImpact { Targets = SkillUseTargetRelation.Friendly, SelectedTargetOnly = healing, DamageScale = 0f, Restoration = restoration },
            Root = new ConstructionNode
            {
                Id = "support", Form = healing ? "healing_seed" : rejuvenation ? "rejuvenation_bloom" : "purification_wave",
                Matter = "vitality", State = "energy", Scale = scale,
                ContactRole = configured && !settings.ContactEnabled ? ConstructionContactRole.Decoration : ConstructionContactRole.Attack,
                Contact = rejuvenation ? new ConstructionContactPolicy { Mode = ConstructionContactMode.Pulse, Interval = 1f } : null,
                OnContact = healing ? ConstructionContactResponse.End : ConstructionContactResponse.Continue, Operations = operations.ToArray()
            }
        };
    }

    /// <summary>输出普通三轴参数。</summary><param name="value">真实向量。</param>
    private static float[] XYZ(Vector3 value) => new[] { value.x, value.y, value.z };
}
