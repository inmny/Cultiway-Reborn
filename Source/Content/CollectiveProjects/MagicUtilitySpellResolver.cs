using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.CollectiveProjects;

/// <summary>一个已经通过职业、法杖、法力和世界法术语义检查的执行候选。</summary>
internal readonly struct MagicUtilitySpellOption
{
    public MagicUtilitySpellOption(
        ActorExtend caster,
        ActiveAbilityHandle ability,
        float radius,
        float demand)
    {
        Caster = caster;
        Ability = ability;
        Radius = Math.Max(0f, radius);
        Demand = Math.Max(0f, demand);
    }

    public ActorExtend Caster { get; }
    public ActiveAbilityHandle Ability { get; }
    public Entity Skill => Ability.Source;
    public float Radius { get; }
    public float Demand { get; }
}

/// <summary>从统一主动能力入口筛选并评价能够执行组织世界工程的已学 mana 法术。</summary>
internal static class MagicUtilitySpellResolver
{
    private static readonly SemanticAsset[] ExclusiveWorldEffects =
    {
        SkillSemantics.Effect.Cleanse,
        SkillSemantics.Effect.Growth,
        SkillSemantics.Effect.RaiseTerrain,
        SkillSemantics.Effect.LowerTerrain,
        SkillSemantics.Effect.FillWater,
        SkillSemantics.Effect.DrainWater,
    };

    /// <summary>收集城市内至少有一名当前可施放者掌握的不同法术容器。</summary>
    public static void CollectCityOptions(
        City city,
        SemanticAsset requiredEffect,
        bool allowWarriors,
        ICollection<MagicUtilitySpellOption> output)
    {
        if (city == null || requiredEffect == null || output == null) return;
        var seenContainers = new HashSet<int>();
        for (int i = 0; i < city.units.Count; i++)
        {
            Actor actor = city.units[i];
            if (!IsEligibleCaster(actor, city, allowWarriors)) continue;
            ActorExtend actorExtend = actor.GetExtend();
            using var abilities = new ListPool<ActiveAbilityHandle>();
            ActiveAbilityService.Collect(actorExtend, abilities);
            for (int abilityIndex = 0; abilityIndex < abilities.Count; abilityIndex++)
            {
                ActiveAbilityHandle ability = abilities[abilityIndex];
                if (!TryCreateOption(actorExtend, ability, requiredEffect, out MagicUtilitySpellOption option) ||
                    !seenContainers.Add(option.Skill.Id)) continue;
                output.Add(option);
            }
        }
    }

    /// <summary>为一个已认领项目选择角色当前收益最高且最省资源的具体法术版本。</summary>
    public static bool TrySelectForActor(
        ActorExtend actor,
        City city,
        WorldTile target,
        CityMagicUtilityProjectPayload payload,
        ISet<int> allowed,
        out MagicUtilitySpellOption selected,
        out float selectedUtility)
    {
        selected = default;
        selectedUtility = 0f;
        if (!IsEligibleCaster(actor?.Base, city,
                payload.Goal == CityMagicUtilityProjectGoal.EmergencyClean)) return false;

        HashSet<int> futureFarmTiles = payload.Goal == CityMagicUtilityProjectGoal.NatureGrowth
            ? CityMagicUtilityProjectRules.CollectFutureFarmTileIds(city)
            : null;
        using var abilities = new ListPool<ActiveAbilityHandle>();
        ActiveAbilityService.Collect(actor, abilities);
        bool found = false;
        for (int i = 0; i < abilities.Count; i++)
        {
            if (!TryCreateOption(actor, abilities[i], payload.EffectSemantic,
                    out MagicUtilitySpellOption option)) continue;
            float utility = EvaluateOption(option, target, payload, allowed, futureFarmTiles);
            if (!MeetsGoal(payload, utility)) continue;
            if (!found || IsBetter(utility, option, selectedUtility, selected))
            {
                found = true;
                selected = option;
                selectedUtility = utility;
            }
        }
        return found;
    }

    /// <summary>按项目目标用技能真实预检和城市空间保护规则计算一次候选收益。</summary>
    public static float EvaluateOption(
        in MagicUtilitySpellOption option,
        WorldTile target,
        CityMagicUtilityProjectPayload payload,
        ISet<int> allowed,
        ISet<int> futureFarmTiles = null)
    {
        if (target == null || payload == null ||
            !CityMagicUtilityProjectRules.IsAreaInsideScope(target.posV3, option.Radius, allowed)) return 0f;

        switch (payload.Goal)
        {
            case CityMagicUtilityProjectGoal.EmergencyClean:
            case CityMagicUtilityProjectGoal.RoutineClean:
                return SkillEffectResolver.EvaluateTileUtility(
                    option.Caster,
                    option.Skill,
                    target.posV3,
                    option.Radius,
                    tile => allowed.Contains(tile.tile_id));
            case CityMagicUtilityProjectGoal.NatureGrowth:
                if (!CityMagicUtilityProjectRules.IsGrowthAreaSafe(
                        target.posV3,
                        option.Radius,
                        futureFarmTiles)) return 0f;
                return SkillEffectResolver.EvaluateTileUtility(
                    option.Caster,
                    option.Skill,
                    target.posV3,
                    option.Radius,
                    tile => allowed.Contains(tile.tile_id) &&
                            CityMagicUtilityProjectRules.IsGrowthCandidate(tile, futureFarmTiles));
            case CityMagicUtilityProjectGoal.HousingTerrain:
            case CityMagicUtilityProjectGoal.FarmTerrain:
                TerrainProjectDelta delta = CityMagicUtilityProjectRules.EvaluateTerrainDelta(
                    option.Caster,
                    option.Skill,
                    target.posV3,
                    option.Radius,
                    payload.EffectSemantic,
                    payload.Goal,
                    allowed);
                return delta.Lost == 0 ? delta.Gained : 0f;
            default:
                return 0f;
        }
    }

    /// <summary>判断一个具体收益是否达到项目目标，并阻止常规净化吞掉应急需求。</summary>
    public static bool MeetsGoal(CityMagicUtilityProjectPayload payload, float utility)
    {
        if (payload == null) return false;
        return payload.Goal switch
        {
            CityMagicUtilityProjectGoal.EmergencyClean =>
                utility >= Math.Max(CityMagicUtilityProjectRules.EmergencyCleanThreshold,
                    payload.MinimumUtility),
            CityMagicUtilityProjectGoal.RoutineClean =>
                utility >= Math.Max(0.01f, payload.MinimumUtility) &&
                utility < CityMagicUtilityProjectRules.EmergencyCleanThreshold,
            CityMagicUtilityProjectGoal.NatureGrowth =>
                utility >= Math.Max(CityMagicUtilityProjectRules.MinimumGrowthTiles,
                    payload.MinimumUtility),
            CityMagicUtilityProjectGoal.HousingTerrain or CityMagicUtilityProjectGoal.FarmTerrain =>
                utility >= Math.Max(CityMagicUtilityProjectRules.MinimumTerrainGain,
                    payload.MinimumNetGain),
            _ => false,
        };
    }

    /// <summary>构造单个主动能力候选，并拒绝注水及混合多个世界改造语义的法术。</summary>
    private static bool TryCreateOption(
        ActorExtend caster,
        ActiveAbilityHandle ability,
        SemanticAsset requiredEffect,
        out MagicUtilitySpellOption option)
    {
        option = default;
        Entity skill = ability.Source;
        if (caster == null || skill.IsNull || !skill.HasComponent<SkillContainer>() ||
            ability.ProviderId != LearnedSkillActiveAbilityProvider.ProviderId ||
            (ActiveAbilityService.GetChannels(caster, ability) & ActiveAbilityChannel.World) == 0 ||
            !SkillCastResourceResolver.UsesResource(skill, SkillCastResources.Mana) ||
            !ActiveAbilityService.CanPrepare(caster, ability, null)) return false;

        SkillContainer container = skill.GetComponent<SkillContainer>();
        if (container.Asset.UseProfile.TargetRelation != SkillUseTargetRelation.WorldTile ||
            container.EffectPipeline == null || !container.EffectPipeline.HasTileEffects) return false;

        HashSet<SemanticAsset> semantics = SkillSemanticCollector.NewSet();
        SkillSemanticCollector.CollectAssetSemantics(container.Asset, semantics);
        SkillSemanticCollector.CollectModifierSemantics(skill, semantics);
        SkillSemanticCollector.CollectTrajectorySemantics(container.Asset, skill, semantics);
        if (!semantics.Contains(requiredEffect) || semantics.Contains(SkillSemantics.Effect.FillWater)) return false;
        for (int i = 0; i < ExclusiveWorldEffects.Length; i++)
        {
            SemanticAsset semantic = ExclusiveWorldEffects[i];
            if (semantic != requiredEffect && semantics.Contains(semantic)) return false;
        }

        option = new MagicUtilitySpellOption(
            caster,
            ability,
            ActiveAbilityService.ResolveEffectRadius(caster, ability),
            SkillCastCost.CalculateStepDemand(skill));
        return option.Radius > 0f;
    }

    /// <summary>限制常规工程为成年非士兵；应急净化额外允许士兵参与。</summary>
    private static bool IsEligibleCaster(Actor actor, City city, bool allowWarriors)
    {
        return actor != null && !actor.isRekt() && actor.city == city && actor.isAdult() &&
               actor.GetExtend().HasCultisys<Magic>() && (allowWarriors || !actor.isWarrior());
    }

    /// <summary>按收益降序、单发消耗升序、容器 ID 升序形成确定性候选排序。</summary>
    private static bool IsBetter(
        float utility,
        in MagicUtilitySpellOption candidate,
        float currentUtility,
        in MagicUtilitySpellOption current)
    {
        if (!UnityEngine.Mathf.Approximately(utility, currentUtility)) return utility > currentUtility;
        if (!UnityEngine.Mathf.Approximately(candidate.Demand, current.Demand))
            return candidate.Demand < current.Demand;
        return candidate.Skill.Id < current.Skill.Id;
    }
}
