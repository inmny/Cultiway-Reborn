using System;
using System.Collections.Generic;
using Cultiway.Content.Components.Skill;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Settings;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>把原有功能来源的实际发射转换为普通构造；学习、收费和多次发射仍由原来源负责。</summary>
internal static class UtilityConstructionMigration
{
    /// <summary>已经有完整世界原理的八项旧功能模板，不按画面猜测作用。</summary>
    private static readonly HashSet<string> templates = new(StringComparer.Ordinal)
    {
        "Cultiway.RaiseTerrain", "Cultiway.LowerTerrain", "Cultiway.FillWater", "Cultiway.DrainWater",
        "Cultiway.NatureGrowthField", "Cultiway.CleanLandField", "Cultiway.SummonRainCloud", "Cultiway.Fertilize"
    };

    /// <summary>本批已经映射且不会遗漏附加作用的旧词条。</summary>
    private static readonly HashSet<Type> modifiers = new()
    {
        typeof(RaiseTerrainModifier), typeof(LowerTerrainModifier), typeof(FillWaterModifier),
        typeof(DrainWaterModifier),
        typeof(NatureGrowthModifier), typeof(CleanLandModifier), typeof(SummonRainCloudModifier),
        typeof(FertilizeModifier),
        typeof(Trajectory), typeof(BurstCount), typeof(SalvoCount), typeof(HugeModifier), typeof(HasteModifier),
        typeof(ProficiencyModifier), typeof(ComboModifier), typeof(VolleyModifier), typeof(EmpowerModifier),
        typeof(ChaosModifier), typeof(GravityModifier)
    };

    /// <summary>保留同次落地的地块结果处理窗口。</summary>
    private const float TerrainPulseWindow = .004f;

    internal static bool Supports(SkillEntityAsset asset) => templates.Contains(asset.id);

    /// <summary>只接管能够完整转换的功能步骤；没有新旧两个执行体，也不重复收费。</summary>
    /// <param name="emission">到达实际发射时点的步骤，若有费用仍由原入口负责。</param>
    internal static SkillEmissionResult Emit(SkillEmission emission)
    {
        Entity skill = emission.SkillContainer;
        if (!Supports(skill.GetComponent<SkillContainer>().Asset) ||
            !emission.Sourceless && emission.Source?.isActor() != true) return SkillEmissionResult.Rejected;
        if (!emission.Sourceless && emission.Source.isRekt()) return SkillEmissionResult.Rejected;
        var caster = emission.Sourceless ? null : emission.Source.a.GetExtend();
        AbilityDefinition definition = Convert(emission, caster, out LegacyAreaImpact landing, out LegacyUtilityGravity gravity);
        if (definition == null) return SkillEmissionResult.Rejected;
        Func<Vector3> trackTarget = null;
        Vector3 point = emission.TargetPos;
        if (definition.Binding.Follow && emission.Target != null && !emission.Target.isRekt())
        {
            BaseSimObject followed = emission.Target;
            long id = followed.getID();
            Vector3 fallback = point;
            trackTarget = () => !followed.isRekt() && followed.getID() == id ? followed.GetSimPos() : fallback;
            point = trackTarget();
        }
        var target = new ActiveAbilityTarget(null, point, attackKingdom: emission.AttackKingdom,
            runtimeData: emission.RuntimeData,
            worldScope: emission.RuntimeData.WorldScope);
        var travel = new LegacyUtilityTravel(trackTarget, gravity);
        // 边界失效或人物已无法执行时，这个已受理步骤结束，不能再落回旧执行制造副作用。
        GetHitEvent? source = null;
        if (landing != null)
        {
            var statusSource = new GetHitEvent { AttackerPowerLevel = emission.PowerLevel, AttackType = AttackType.Other };
            if (!emission.Sourceless)
                statusSource.BindAttacker(!emission.SpatialSource.isRekt() ? emission.SpatialSource : caster.Base);
            source = statusSource;
        }
        bool accepted = emission.Sourceless
            ? ConstructedAbilityService.StartWorld(definition, target, emission.SourcePos, source.GetValueOrDefault(),
                sourceInstance: skill.Id, sourceIsWorld: emission.SourceIsWorld, legacyArea: landing,
                utilityTravel: travel) != 0
            : ConstructedAbilityService.StartPrepaid(caster, definition, target, emission.SourcePos,
                emission.Payment, source, sourceInstance: skill.Id, spatialSource: emission.SpatialSource,
                sourceIsWorld: emission.SourceIsWorld, legacyArea: landing, utilityTravel: travel,
                reportPreview: emission.ReportPreview);
        return accepted ? SkillEmissionResult.Accepted : SkillEmissionResult.Rejected;
    }

    /// <summary>从当前真实容器读取数值，转换结果不保存旧实体或委托。</summary>
    /// <param name="emission">当前实际步骤。</param><param name="caster">实际范围成长的归属，世界发射为空。</param>
    /// <param name="landing">落地词条使用的原中心、范围和强度。</param>
    /// <param name="gravity">原引力词条按实际范围和作用时点展开的数值。</param>
    private static unsafe AbilityDefinition Convert(SkillEmission emission, Core.ActorExtend caster,
        out LegacyAreaImpact landing, out LegacyUtilityGravity gravity)
    {
        landing = null;
        gravity = default;
        Entity skill = emission.SkillContainer;
        var container = skill.GetComponent<SkillContainer>();
        foreach (Type type in skill.GetComponentTypes())
            if (typeof(IModifier).IsAssignableFrom(type) && !modifiers.Contains(type) && !ContactEffectMigration.SupportsUtilityLanding(type))
                return null;
        if (container.OnEffectObj != null && !ContactEffectMigration.MatchesCallbacks(skill)) return null;
        if (container.OnTravel != null && !ContactEffectMigration.MatchesTravelCallbacks(skill)) return null;
        string trajectory = SkillBlueprintTrajectory.ResolveEffectiveId(skill);
        bool follow = trajectory == SkillTrajectories.FieldFollowTarget.id;
        bool advance = trajectory == SkillTrajectories.FieldAdvance.id;
        bool vortex = trajectory == SkillTrajectories.SlowVortex.id;
        bool manifest = trajectory == SkillTrajectories.AppearAtTarget.id || trajectory == SkillTrajectories.GroundEruptAtTarget.id;
        SkillEntityAsset asset = container.Asset;
        bool field = asset == SkillEntities.NatureGrowthField || asset == SkillEntities.CleanLandField;
        if (!manifest && (!field || !follow && !advance && !vortex && trajectory != SkillTrajectories.FieldAtTarget.id))
            return null;
        float radius = SkillEffectRadius.ResolveForCaster(caster, skill,
            asset.ImpactProfile.EffectRadius * asset.ImpactTuning.EffectRadiusMultiplier);
        // 共同地块预算覆盖最多一百万格；超过当前已实现的根尺寸留给旧来源，避免改小原术。
        if (radius > 1000f) return null;
        float lifetime =
            asset.ResolveRuntimeLifetime(skill, asset.ImpactProfile.Lifetime * asset.ImpactTuning.LifetimeMultiplier);
        bool configured = skill.TryGetComponent(out SkillSettings settings);
        if (configured && settings.Duration > 0f) lifetime = settings.Duration;
        bool instantPulse = !field;
        float duration = lifetime;
        float formation = 0f;
        float activeEnd = lifetime;
        if (instantPulse || field)
        {
            SkillEntityAnimation animation = asset.GetAnimation(container.AnimationIndex);
            float frame = container.MotionProfile.FrameInterval;
            formation = ProjectileConstructionMigration.ClipSeconds(animation.Appearance, frame);
            if (formation <= 0f || animation.AppearanceGameplay != SkillAnimationGameplayFlags.None) return null;
            activeEnd = formation + (field ? lifetime : 0f);
            lifetime = activeEnd + ProjectileConstructionMigration.ClipSeconds(animation.Dissipation, frame);
            if (field)
            {
                // 原寿命检查先于周期作用，到期的整点已不属于有效段。
                int preceding = *(int*)&activeEnd - 1;
                activeEnd = *(float*)&preceding;
            }
        }
        var effects = new List<ConstructionWorldEffect>();

        void Add<T>(string action, float interval = 0f, float? duration = null) where T : struct, IModifier
        {
            if (!skill.HasComponent<T>()) return;
            effects.Add(new ConstructionWorldEffect
            {
                Action = action,
                TileCentreOnly = true,
                Duration = duration,
                MaximumTiles = action == "weather.rain" && radius == 0f ? 1 :
                    Mathf.Min(1048576, Mathf.Max(1, MapBox.width * MapBox.height)),
                Contact = instantPulse
                    ? new ConstructionContactPolicy
                    { Mode = ConstructionContactMode.Pulse, Interval = formation, MaximumPerTarget = 1 }
                    : interval > 0f
                        ? new ConstructionContactPolicy
                        { Mode = ConstructionContactMode.Pulse, Interval = interval, PulseOffset = formation }
                        : null
            });
        }

        Add<RaiseTerrainModifier>("terrain.raise");
        Add<LowerTerrainModifier>("terrain.lower");
        Add<FillWaterModifier>("water.fill");
        Add<DrainWaterModifier>("water.drain");
        Add<NatureGrowthModifier>("nature.grow", 1f);
        Add<CleanLandModifier>("land.cleanse", .5f);
        Add<FertilizeModifier>("crop.mature");
        Add<SummonRainCloudModifier>("weather.rain", duration: skill.HasComponent<SummonRainCloudModifier>()
            ? Mathf.Max(4f, skill.GetComponent<SummonRainCloudModifier>().Duration)
            : null);
        if (effects.Count == 0) return null;
        if (skill.TryGetComponent(out GravityModifier pull))
            gravity = new LegacyUtilityGravity(
                SkillEffectRadius.ResolveForCaster(caster, skill, Mathf.Clamp(pull.Radius, .5f, 10f)),
                Mathf.Clamp(pull.Strength * emission.RuntimeData.ResolveEffectScale(), 0f, 10f), formation, activeEnd);
        var operations = new List<ConstructionOperation>();
        float setupScale = ContactEffectMigration.SetupStrengthScale(skill, out float angle, out float speedScale);
        if (advance)
            AddAdvance(operations, emission, formation, duration, angle, speedScale);

        var offset = new[] { .5f, .5f, 0f };
        if (radius <= 0f)
        {
            offset[0] += Mathf.Floor(emission.TargetPos.x) - emission.TargetPos.x;
            offset[1] += Mathf.Floor(emission.TargetPos.y) - emission.TargetPos.y;
        }

        var domains = new List<ConstructionNode>();
        foreach (ConstructionWorldEffect effect in effects)
        {
            float first = effect.Contact == null ? formation : effect.Contact.Interval + effect.Contact.PulseOffset;
            float until = instantPulse ? Mathf.Min(first + TerrainPulseWindow, lifetime) : field ? activeEnd : lifetime;
            if (first > until) continue;
            domains.Add(new ConstructionNode
            {
                Id = "effect" + domains.Count,
                Form = "world_domain",
                Scale = Mathf.Max(.1f, radius),
                Offset = offset,
                ActiveFrom = first,
                ActiveUntil = until,
                Impact = new ConstructionImpact
                {
                    Targets = SkillUseTargetRelation.Friendly,
                    DamageScale = 0f,
                    WorldEffects = new[] { effect }
                }
            });
        }

        // 原落地冲击会单独挑选敌人。持续地块的周期不调用旧命中词条，也不借用地块碰撞面来选人。
        ConstructionImpact landingImpact = null;
        float landingAt = 0f;
        if (instantPulse && container.OnEffectObj != null)
        {
            float effectScale = emission.RuntimeData.ResolveEffectScale();
            landingImpact = ContactEffectMigration.UtilityLanding(skill, effectScale, caster, out UtilityLandingStep[] steps);
            if (landingImpact != null)
            {
                landingAt = formation;
                if (steps.Length > 0)
                {
                    // 先按原占位处理地块，再提交可能改变占位的作用。
                    landingAt += TerrainPulseWindow + .001f;
                    lifetime = Mathf.Max(lifetime, landingAt + .001f);
                }
                // 原逐发强度只给附加词条使用。地块范围不携带这份攻击强度，也不参与法术相撞。
                float strength = emission.Strength * setupScale;
                if (configured) strength *= settings.PowerMultiplier;
                if (skill.TryGetComponent(out EmpowerModifier empower)) strength *= 1f + Mathf.Clamp(empower.SetupBonus, 0f, 10f);
                if (skill.TryGetComponent(out VolleyModifier volley)) strength *= Mathf.Clamp(volley.DamageMultiplier, .05f, 2f);
                if (skill.TryGetComponent(out ComboModifier combo)) strength *= Mathf.Clamp(combo.DamageMultiplier, .05f, 2f);
                var element = landingImpact.Spreads.Length > 0 || skill.HasComponent<ReincarnationTrialModifier>()
                    ? ModClass.I.SkillV3.ResolveGroundEffectElement(skill, emission.RuntimeData) : null;
                landing = LegacyAreaImpact.ForUtility(radius, strength * effectScale, element, steps);
            }
        }

        if (advance || vortex)
            operations.Add(new ConstructionOperation
                { Kind = ConstructionOperationKind.ChangeSpeed, At = formation + duration, Speed = 0f });
        var definition = new AbilityDefinition
        {
            Id = asset.id,
            Name = skill.HasName ? skill.Name.value : asset.id.Localize(),
            Resource = "none",
            Cost = 0f,
            Power = 0f,
            Cooldown = 0f,
            Lifetime = lifetime,
            ConcurrentLimit = asset.ImpactProfile.PersistentLimit,
            TargetRange = 10000f,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.World, Target = ActiveAbilityTargetMode.Point },
            Impact = new ConstructionImpact
            {
                DamageScale = 0f,
                Targets = SkillUseTargetRelation.Friendly
            },
            Binding = new ConstructionBinding
            {
                Anchor = advance ? AbilityAnchor.Source : AbilityAnchor.Target,
                Follow = follow,
                ContactAtDestination = follow
            },
            Root = new ConstructionNode
            {
                Id = "origin",
                Form = "pivot",
                ActiveFrom = landingAt,
                ContactRole = landingImpact == null ? ConstructionContactRole.Decoration : ConstructionContactRole.Attack,
                Impact = landingImpact,
                Children = domains.ToArray(),
                Operations = operations.ToArray()
            }
        };
        if (vortex)
        {
            AbilityDefinition motion = ProjectileConstructionMigration.Motion(
                emission, definition.Root.Form, 1f, true, duration, angle, speedScale, out _);
            if (motion == null) return null;
            foreach (ConstructionOperation operation in motion.Root.Operations) operation.At += formation;
            ProjectileConstructionMigration.BindMotion(definition, motion);
            definition.Lifetime = lifetime;
        }
        return definition;
    }

    /// <summary>展开原领域的二次缓出起步曲线，成形后才开始移动。</summary>
    private static void AddAdvance(List<ConstructionOperation> operations, in SkillEmission emission,
        float formation, float duration, float angle, float speedScale)
    {
        var motion = emission.SkillContainer.GetComponent<SkillContainer>().MotionProfile;
        float speed = motion.ResolveSpeed(Vector2.Distance(emission.SourcePos, emission.TargetPos));
        if (emission.SkillContainer.TryGetComponent(out HasteModifier haste))
            speed *= Mathf.Clamp(1f + haste.SpeedMultiplier, .1f, 10f);
        speed = (speed > 0f ? speed : 28f) * .25f * (speed > 0f ? speedScale : 1f);
        Vector3 heading = emission.TargetPos - emission.SourcePos;
        heading = heading.sqrMagnitude >= .0001f ? heading.normalized : Vector3.right;
        heading = Quaternion.AngleAxis(emission.InitialAngleOffsetDegrees + angle, Vector3.forward) * heading;
        float launch = motion.LaunchMultiplier, cruise = motion.CruiseMultiplier, ramp = motion.RampDuration;
        Vector3 velocity = heading * speed * (ramp > .001f ? launch : cruise);
        operations.Add(new ConstructionOperation
        {
            Kind = ConstructionOperationKind.Fly, At = formation,
            Vector = new[] { velocity.x, velocity.y, velocity.z }, WorldVelocity = true, InheritVelocity = false
        });
        if (ramp <= .001f) return;
        for (int i = 1; i <= 8; i++)
        {
            float at = ramp * i / 8f;
            if (at >= duration) break;
            float remaining = 1f - i / 8f;
            operations.Add(new ConstructionOperation { Kind = ConstructionOperationKind.ChangeSpeed, At = formation + at,
                Speed = speed * (cruise + (launch - cruise) * remaining * remaining) });
        }
    }

    /// <summary>四种瞬时地貌术使用旧落点时机和已变化地块的立体收势。</summary>
    internal static bool HasTerrainPulse(string id) =>
        id is "Cultiway.RaiseTerrain" or "Cultiway.LowerTerrain" or "Cultiway.FillWater" or "Cultiway.DrainWater";
}
