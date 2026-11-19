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
using strings;

namespace Cultiway.Content.Skills;

/// <summary>六类原支持来源转换成普通真实接触，仍使用共享恢复和净化规则。</summary>
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
        Entity skill = emission.SkillContainer;
        var asset = skill.GetComponent<SkillContainer>().Asset;
        if (emission.Sourceless || emission.Source?.isActor() != true ||
            asset != SkillEntities.HealingLight && asset != SkillEntities.RejuvenationField && asset != SkillEntities.PurificationWave &&
            asset != SkillEntities.BattleBlessing && asset != SkillEntities.GuardBlessing && asset != SkillEntities.HasteBlessing)
            return SkillEmissionResult.Unhandled;
        if (emission.Source.isRekt() || !emission.Source.a.isAlive()) return SkillEmissionResult.Rejected;
        if (emission.SpatialSource != null && !emission.SpatialSource.isActor())
            return SkillEmissionResult.Unhandled;
        AbilityDefinition definition = Convert(emission);
        if (definition == null) return SkillEmissionResult.Unhandled;
        var target = new ActiveAbilityTarget(definition.Use.Target == ActiveAbilityTargetMode.Object ? emission.Target : null,
            emission.TargetPos, attackKingdom: emission.AttackKingdom, runtimeData: emission.RuntimeData, worldScope: emission.RuntimeData.WorldScope);
        // 载体有明确交锋余量，人物伤害始终为零；恢复量独立保存，不拿攻击力驱动治疗。
        var carrier = new GetHitEvent { Damage = 1f, Element = NatureConstructionMigration.Element(skill, emission.RuntimeData), AttackerPowerLevel = emission.PowerLevel,
            AttackType = AttackType.Other, DamageOrigin = emission.RuntimeData.DamageOrigin };
        carrier.BindAttacker(emission.Source);
        return ConstructedAbilityService.StartPrepaid(emission.Source.a.GetExtend(), definition, target,
            emission.SourcePos, emission.Payment, carrier, sourceInstance: emission.SkillContainer.Id, spatialSource: emission.SpatialSource)
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
        bool blessing = asset == SkillEntities.BattleBlessing || asset == SkillEntities.GuardBlessing || asset == SkillEntities.HasteBlessing;
        bool targeted = healing || blessing;
        string trajectory = SkillBlueprintTrajectory.ResolveEffectiveId(skill);
        if (!targeted && trajectory != (rejuvenation ? SkillTrajectories.FieldAtTarget.id : SkillTrajectories.AppearAtTarget.id) ||
            targeted && emission.Target?.isActor() != true) return null;
        (Type required, SkillModifierAsset modifierAsset, string form) = healing ? (typeof(HealingModifier), SkillModifiers.Healing, "healing_seed") :
            rejuvenation ? (typeof(RejuvenationModifier), SkillModifiers.Rejuvenation, "rejuvenation_bloom") :
            asset == SkillEntities.PurificationWave ? (typeof(PurificationModifier), SkillModifiers.Purification, "purification_wave") :
            asset == SkillEntities.BattleBlessing ? (typeof(BattleBlessingModifier), SkillModifiers.BattleBlessing, "battle_knot") :
            asset == SkillEntities.GuardBlessing ? (typeof(GuardBlessingModifier), SkillModifiers.GuardBlessing, "guard_seal") :
            (typeof(HasteBlessingModifier), SkillModifiers.HasteBlessing, "haste_vanes");
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
        if (!NatureConstructionMigration.Supports(skill)) return null;
        float lifetime = asset.ResolveRuntimeLifetime(skill, asset.ImpactProfile.Lifetime * asset.ImpactTuning.LifetimeMultiplier);
        if (configured && settings.Duration > 0f) lifetime = settings.Duration;
        float effectScale = emission.RuntimeData.ResolveEffectScale();
        float scale = effectScale * SkillEffectRadius.ResolveCasterGrowth(emission.Source.a.GetExtend(), skill);
        if (skill.HasComponent<HugeModifier>()) scale *= SkillModifiers.Huge.EffectRadiusMultiplier(skill);
        if (configured) scale *= settings.AreaMultiplier;
        if (scale <= 0f || scale > 100f || lifetime <= 0f || lifetime > 3600f) return null;
        var restoration = blessing ? null : new ConstructionRestoration();
        if (healing) restoration.Health = skill.GetComponent<HealingModifier>().Amount * effectScale;
        else if (rejuvenation) restoration.HealthPerSecond = skill.GetComponent<RejuvenationModifier>().HealPerSecond * effectScale;
        else if (!blessing) restoration.CleanseCount = skill.GetComponent<PurificationModifier>().MaxStatuses;
        var operations = new List<ConstructionOperation> { new() { Kind = ConstructionOperationKind.Animate, Clip = "run", Seconds = targeted || rejuvenation ? 0f : lifetime } };
        // 整体展开使用普通生长步骤，网格和接触端点共享尺寸，不向骨架动作写入未支持的缩放。
        if (!targeted && !rejuvenation)
            operations.Add(new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = lifetime });
        if (targeted && trajectory == SkillTrajectories.TowardsTarget.id)
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
        var definition = new AbilityDefinition
        {
            Id = asset.id, Name = skill.HasName ? skill.Name.value : asset.id.Localize(), Lifetime = lifetime,
            ConcurrentLimit = asset.ImpactProfile.PersistentLimit, Resource = "none", Cost = 0f, Cooldown = 0f, Power = 1f, TargetRange = 10000f,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World,
                Target = targeted ? ActiveAbilityTargetMode.Object : ActiveAbilityTargetMode.Point },
            Binding = new ConstructionBinding { Anchor = targeted ? AbilityAnchor.Source : AbilityAnchor.Target,
                Height = targeted ? 0f : -emission.TargetPos.z },
            Impact = new ConstructionImpact { Targets = SkillUseTargetRelation.Friendly, SelectedTargetOnly = targeted, DamageScale = 0f, Restoration = restoration,
                Statuses = blessing ? new[] { Blessing(skill, effectScale) } : null,
                WorldEffects = rejuvenation ? new[]
                {
                    new ConstructionWorldEffect
                    {
                        Action = LivingPlantRestoration.ActionId, MaximumTiles = 16,
                        Contact = new ConstructionContactPolicy
                        { Mode = ConstructionContactMode.Pulse, Interval = 1f, MaximumPerTarget = 4, MaximumTotal = 32 }
                    }
                } : null },
            Root = new ConstructionNode
            {
                Id = "support", Form = form,
                Matter = "vitality", State = "energy", Scale = scale,
                ContactRole = configured && !settings.ContactEnabled ? ConstructionContactRole.Decoration : ConstructionContactRole.Attack,
                Contact = rejuvenation ? new ConstructionContactPolicy { Mode = ConstructionContactMode.Pulse, Interval = 1f } : null,
                OnContact = targeted ? ConstructionContactResponse.End : ConstructionContactResponse.Continue, Operations = operations.ToArray()
            }
        };
        if (targeted && trajectory != SkillTrajectories.TowardsTarget.id)
        {
            AbilityDefinition motion = ProjectileConstructionMigration.Motion(emission, form, scale, false, lifetime, 0f, 1f, out _);
            if (motion == null) return null;
            ProjectileConstructionMigration.BindMotion(definition, motion);
        }
        return NatureConstructionMigration.Bind(MediumContactMigration.Bind(definition, skill, runtimeData: emission.RuntimeData), skill, emission.RuntimeData);
    }

    /// <summary>将原祝福词条展开为普通属性状态，时长不乘效果或伤害倍率。</summary>
    /// <param name="skill">已核对必选词条的原来源。</param><param name="scale">实际效果倍率。</param>
    private static ConstructionStatus Blessing(Entity skill, float scale)
    {
        if (skill.TryGetComponent(out BattleBlessingModifier battle))
        {
            float value = battle.DamageBonus * scale;
            return new ConstructionStatus { Id = StatusEffects.BattleBlessing.id, Duration = battle.Duration, Potency = value,
                Stats = new Dictionary<string, float> { [S.multiplier_damage] = value } };
        }
        if (skill.TryGetComponent(out GuardBlessingModifier guard))
        {
            float value = guard.ArmorBonus * scale;
            return new ConstructionStatus { Id = StatusEffects.GuardBlessing.id, Duration = guard.Duration, Potency = value,
                Stats = new Dictionary<string, float> { [S.armor] = value } };
        }
        HasteBlessingModifier haste = skill.GetComponent<HasteBlessingModifier>();
        float move = haste.MoveSpeedBonus * scale, attack = haste.AttackSpeedBonus * scale;
        return new ConstructionStatus { Id = StatusEffects.HasteBlessing.id, Duration = haste.Duration, Potency = move + attack,
            Stats = new Dictionary<string, float> { [S.multiplier_speed] = move, [S.multiplier_attack_speed] = attack } };
    }

    /// <summary>输出普通三轴参数。</summary><param name="value">真实向量。</param>
    private static float[] XYZ(Vector3 value) => new[] { value.x, value.y, value.z };
}
