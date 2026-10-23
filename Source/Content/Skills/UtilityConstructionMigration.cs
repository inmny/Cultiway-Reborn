using System;
using System.Collections.Generic;
using Cultiway.Content.Components.Skill;
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
        typeof(ProficiencyModifier), typeof(ComboModifier), typeof(VolleyModifier), typeof(EmpowerModifier)
    };

    /// <summary>只接管能够完整转换的功能步骤；没有新旧两个执行体，也不重复收费。</summary>
    /// <param name="emission">已经收费并到达实际发射时点的步骤。</param>
    internal static bool Emit(SkillEmission emission)
    {
        Entity skill = emission.SkillContainer;
        if (emission.Sourceless || emission.Source?.isActor() != true ||
            !templates.Contains(skill.GetComponent<SkillContainer>().SkillEntityAssetID)) return false;
        AbilityDefinition definition = Convert(emission);
        if (definition == null) return false;
        var target = new ActiveAbilityTarget(
            definition.Use.Target == ActiveAbilityTargetMode.Point ? null : emission.Target,
            emission.TargetPos, attackKingdom: emission.AttackKingdom, runtimeData: emission.RuntimeData,
            worldScope: emission.RuntimeData.WorldScope);
        // 边界失效或人物已无法执行时，这个已受理步骤结束，不能再落回旧执行制造副作用。
        ConstructedAbilityService.StartPrepaid(emission.Source.a.GetExtend(), definition, target, emission.SourcePos);
        return true;
    }

    /// <summary>从当前真实容器读取数值，转换结果不保存旧实体或委托。</summary>
    /// <param name="emission">当前实际步骤。</param>
    private static AbilityDefinition Convert(SkillEmission emission)
    {
        Entity skill = emission.SkillContainer;
        foreach (Type type in skill.GetComponentTypes())
            if (typeof(IModifier).IsAssignableFrom(type) && !modifiers.Contains(type))
                return null;
        string trajectory = SkillBlueprintTrajectory.ResolveEffectiveId(skill);
        bool follow = trajectory == SkillTrajectories.FieldFollowTarget.id;
        bool advance = trajectory == SkillTrajectories.FieldAdvance.id;
        if (!follow && !advance && trajectory != SkillTrajectories.AppearAtTarget.id &&
            trajectory != SkillTrajectories.GroundEruptAtTarget.id && trajectory != SkillTrajectories.FieldAtTarget.id)
            return null;
        SkillEntityAsset asset = skill.GetComponent<SkillContainer>().Asset;
        float radius = SkillEffectRadius.ResolveForCaster(emission.Source.a.GetExtend(), skill,
            asset.ImpactProfile.EffectRadius * asset.ImpactTuning.EffectRadiusMultiplier);
        // 共同地块预算覆盖最多一百万格；超过当前已实现的根尺寸留给旧来源，避免改小原术。
        if (radius > 1000f) return null;
        float lifetime =
            asset.ResolveRuntimeLifetime(skill, asset.ImpactProfile.Lifetime * asset.ImpactTuning.LifetimeMultiplier);
        if (skill.TryGetComponent(out SkillSettings settings) && settings.Duration > 0f) lifetime = settings.Duration;
        var effects = new List<ConstructionWorldEffect>();

        void Add<T>(string action, float interval = 0f, float? duration = null) where T : struct, IModifier
        {
            if (!skill.HasComponent<T>()) return;
            effects.Add(new ConstructionWorldEffect
            {
                Action = action, TileCentreOnly = true, Duration = duration,
                MaximumTiles = Mathf.Min(1048576, Mathf.Max(1, MapBox.width * MapBox.height)),
                Contact = interval > 0f
                    ? new ConstructionContactPolicy
                        { Mode = ConstructionContactMode.Interval, Interval = interval }
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
        var operations = new List<ConstructionOperation>();
        if (advance)
        {
            float speed = skill.GetComponent<SkillContainer>().MotionProfile.ResolveSpeed(
                Vector2.Distance(emission.SourcePos, emission.TargetPos)) * .25f;
            if (skill.TryGetComponent(out HasteModifier haste))
                speed *= Mathf.Clamp(1f + haste.SpeedMultiplier, .1f, 10f);
            Vector3 direction = emission.TargetPos - emission.SourcePos;
            direction.z = 0f;
            direction = direction.sqrMagnitude > .000001f ? direction.normalized : Vector3.right;
            direction = Quaternion.AngleAxis(emission.InitialAngleOffsetDegrees, Vector3.forward) * direction;
            operations.Add(new ConstructionOperation
            {
                Kind = ConstructionOperationKind.Fly,
                Vector = new[] { direction.x * speed, direction.y * speed, 0f }, WorldVelocity = true,
                InheritVelocity = false
            });
        }

        var offset = new[] { .5f, .5f, 0f };
        if (radius <= 0f)
        {
            offset[0] += Mathf.Floor(emission.TargetPos.x) - emission.TargetPos.x;
            offset[1] += Mathf.Floor(emission.TargetPos.y) - emission.TargetPos.y;
        }

        var domains = new List<ConstructionNode>();
        foreach (ConstructionWorldEffect effect in effects)
        {
            float first = effect.Contact?.Interval ?? 0f;
            if (first > lifetime) continue;
            domains.Add(new ConstructionNode
            {
                Id = "effect" + domains.Count, Form = "world_domain",
                Scale = Mathf.Max(.1f, radius), Offset = offset, ActiveFrom = first,
                Impact = new ConstructionImpact
                {
                    Targets = SkillUseTargetRelation.Friendly, DamageScale = 0f,
                    WorldEffects = new[] { effect }
                }
            });
        }

        bool targetActor = follow && emission.Target?.isActor() == true && !emission.Target.isRekt();
        return new AbilityDefinition
        {
            Id = asset.id, Name = skill.HasName ? skill.Name.value : asset.id.Localize(),
            Resource = "none", Cost = 0f, Power = 0f, Cooldown = 0f, Lifetime = lifetime, TargetRange = 10000f,
            Use = new ConstructionUse
            {
                Channels = ActiveAbilityChannel.World,
                Target = targetActor ? ActiveAbilityTargetMode.ObjectOrPoint : ActiveAbilityTargetMode.Point
            },
            Impact = new ConstructionImpact
            {
                DamageScale = 0f,
                Targets = targetActor && SkillTargetRelationResolver.Matches(SkillUseTargetRelation.Hostile,
                    emission.Source, emission.Target)
                    ? SkillUseTargetRelation.Hostile
                    : SkillUseTargetRelation.Friendly
            },
            Binding = new ConstructionBinding
            {
                Anchor = advance ? AbilityAnchor.Source : AbilityAnchor.Target,
                Follow = follow && targetActor
            },
            Root = new ConstructionNode
            {
                Id = "origin", Form = "pivot", ContactRole = ConstructionContactRole.Decoration,
                Children = domains.ToArray(), Operations = operations.ToArray()
            }
        };
    }
}