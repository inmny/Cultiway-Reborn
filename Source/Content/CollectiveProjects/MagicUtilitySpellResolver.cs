using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Skills;
using Cultiway.Core.Skills;
using UnityEngine;
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

/// <summary>已经通过自身资源、已学关系和世界用途检查的执行候选。</summary>
internal readonly struct MagicUtilitySpellOption
{
    /// <summary>保留真实能力和可选的完整构造足迹。</summary>
    /// <param name="caster">实际持有者。</param><param name="ability">已学能力。</param>
    /// <param name="radius">旧范围或历史空间包围半径。</param><param name="demand">真实单次费用。</param>
    /// <param name="construction">新构造共享计划。</param><param name="preview">本次落点足迹。</param>
    /// <param name="action">当前城市需求对应原理。</param>
    public MagicUtilitySpellOption(
        ActorExtend caster,
        ActiveAbilityHandle ability,
        float radius,
        float demand, CompiledAbility construction = null, ConstructedWorldPreview preview = null, string action = null)
    {
        Caster = caster;
        Ability = ability;
        Radius = Math.Max(0f, radius);
        Demand = Math.Max(0f, demand);
        Construction = construction;
        Preview = preview;
        Action = action;
    }

    /// <summary>已学构造的共享计划，旧法术为空。</summary>
    internal CompiledAbility Construction { get; }

    /// <summary>此次落点完整评价的足迹，旧法术为空。</summary>
    internal ConstructedWorldPreview Preview { get; }

    /// <summary>本次工程要求的实际原理。</summary>
    internal string Action { get; }

    /// <summary>只使用实际能力与持有者作为不同候选，不用人物实体冒充法术编号。</summary>
    internal string Key => Ability.ProviderId + ":" + (Construction == null ? Skill.Id.ToString() : Ability.EntryId);

    /// <summary>旧法术使用格点原点，新构造明确采用地块中心和离地高度。</summary>
    /// <param name="tile">实际落点。</param>
    internal Vector3 TargetPoint(WorldTile tile) =>
        tile.posV3 + (Construction == null ? Vector3.zero : new Vector3(.5f, .5f, 0));

    /// <summary>固定一次落点预估，其他候选不会改写已经选中的足迹。</summary>
    /// <param name="preview">完整数值结果。</param><param name="target">实际落点。</param>
    internal MagicUtilitySpellOption WithPreview(ConstructedWorldPreview preview, WorldTile target)
    {
        Vector3 center = TargetPoint(target);
        float radius = preview.AllTiles.Select(id => Vector2.Distance(
            CityMagicUtilityProjectPlannerSearch.ResolveTile(id).posV3 +
            new Vector3(.5f, .5f, 0), center)).DefaultIfEmpty(0).Max() + .71f;
        return new MagicUtilitySpellOption(Caster, Ability, radius, Demand, Construction, preview, Action);
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
        SkillSemantics.Effect.Rain,
        SkillSemantics.Effect.Fertilize,
    };

    /// <summary>单个持有者按需求轮换已学能力，避免知识很多时永远只查前八项。</summary>
    private static readonly Dictionary<(long Actor, CityMagicUtilityProjectGoal Goal), int> AbilityCursors = new();

    /// <summary>换世界后不沿用人物的旧搜索位置。</summary>
    internal static void Clear() => AbilityCursors.Clear();

    /// <summary>收集城市内至少有一名当前可施放者掌握的不同法术容器。</summary>
    public static void CollectCityOptions(
        City city,
        SemanticAsset requiredEffect,
        bool allowWarriors,
        ICollection<MagicUtilitySpellOption> output)
    {
        if (city == null || requiredEffect == null || output == null) return;
        var seenContainers = new HashSet<(string, long)>();
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
                    !seenContainers.Add((option.Key, option.Construction == null ? 0 : actor.id))) continue;
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
        var key = (actor.Base.id, payload.Goal);
        AbilityCursors.TryGetValue(key, out int start);
        int previews = 0;
        for (int i = 0; i < abilities.Count; i++)
        {
            int index = (start + i) % abilities.Count;
            if (!TryCreateOption(actor, abilities[index], payload.EffectSemantic,
                    out MagicUtilitySpellOption option)) continue;
            if (option.Construction != null)
            {
                if (previews++ >= 8) continue;
                AbilityCursors[key] = (index + 1) % abilities.Count;
            }

            float utility = EvaluateOption(ref option, target, payload, allowed, futureFarmTiles);
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
        ref MagicUtilitySpellOption option,
        WorldTile target,
        CityMagicUtilityProjectPayload payload,
        ISet<int> allowed,
        ISet<int> futureFarmTiles = null)
    {
        if (target == null || payload == null) return 0f;
        if (option.Construction != null)
        {
            if (!ConstructedAbilityService.TryPreviewWorld(option.Caster, option.Construction,
                    option.TargetPoint(target), out var preview)) return 0f;
            option = option.WithPreview(preview, target);
            return CityMagicUtilityProjectRules.EvaluateConstructed(option, payload, allowed, futureFarmTiles);
        }

        if (!CityMagicUtilityProjectRules.IsAreaInsideScope(target.posV3, option.Radius, allowed)) return 0f;

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
            case CityMagicUtilityProjectGoal.CropFertilization:
                return SkillEffectResolver.EvaluateTileUtility(
                    option.Caster,
                    option.Skill,
                    target.posV3,
                    option.Radius,
                    tile => allowed.Contains(tile.tile_id) &&
                            CityMagicUtilityProjectRules.IsFertilizableCrop(tile));
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
            CityMagicUtilityProjectGoal.CropFertilization =>
                utility >= Math.Max(CityMagicUtilityProjectRules.MinimumFertilizableCrops,
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
        if (ConstructedAbilityService.TryWorldProjectAbility(caster, ability, out var plan, out float cost))
        {
            string action = CityMagicUtilityProjectRules.ActionFor(requiredEffect);
            if (!CityMagicUtilityProjectRules.AcceptsConstruction(plan, action)) return false;
            option = new MagicUtilitySpellOption(caster, ability, 0, cost, plan, action: action);
            return true;
        }

        Entity skill = ability.Source;
        if (!caster.HasCultisys<Magic>()) return false;
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
            if (!IsRequiredEffectClosure(requiredEffect, semantic) && semantics.Contains(semantic)) return false;
        }

        option = new MagicUtilitySpellOption(
            caster,
            ability,
            ActiveAbilityService.ResolveEffectRadius(caster, ability),
            SkillCastCost.CalculateStepDemand(skill));
        return option.Radius > 0f;
    }

    /// <summary>
    /// 判断一个互斥效果是否属于所需效果自身的语义闭包，例如“施肥”自然蕴含“成长”。
    /// </summary>
    private static bool IsRequiredEffectClosure(SemanticAsset requiredEffect, SemanticAsset candidate)
    {
        var expansion = ModClass.L.SemanticLibrary.Expand(requiredEffect);
        for (int i = 0; i < expansion.Count; i++)
        {
            if (expansion[i].semantic == candidate) return true;
        }

        return false;
    }

    /// <summary>限制常规工程为成年非士兵；应急净化额外允许士兵参与。</summary>
    private static bool IsEligibleCaster(Actor actor, City city, bool allowWarriors)
    {
        return actor != null && !actor.isRekt() && actor.city == city && actor.isAdult() &&
               (allowWarriors || !actor.isWarrior());
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
        return string.CompareOrdinal(candidate.Key, current.Key) < 0;
    }
}