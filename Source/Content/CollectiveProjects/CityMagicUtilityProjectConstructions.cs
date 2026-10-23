using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Semantics;
using Cultiway.Core.Skills;

namespace Cultiway.Content.CollectiveProjects;

/// <summary>新构造仍使用原城市需求、保护和验收，范围读取实际足迹。</summary>
internal static partial class CityMagicUtilityProjectRules
{
    /// <summary>城市当前已经支持评价的原理；不按整招名称或材料猜测用途。</summary>
    /// <param name="semantic">已有城市需求。</param>
    internal static string ActionFor(SemanticAsset semantic)
    {
        if (semantic == SkillSemantics.Effect.Cleanse) return "land.cleanse";
        if (semantic == SkillSemantics.Effect.Growth) return "nature.grow";
        if (semantic == SkillSemantics.Effect.Fertilize) return "crop.mature";
        if (semantic == SkillSemantics.Effect.RaiseTerrain) return "terrain.raise";
        if (semantic == SkillSemantics.Effect.LowerTerrain) return "terrain.lower";
        if (semantic == SkillSemantics.Effect.DrainWater) return "water.drain";
        return null;
    }

    /// <summary>检查完整直接地块作用，不把未经评价的攻击或其他原理当作没有副作用。</summary>
    /// <param name="plan">纯世界施放计划。</param><param name="required">本次需求。</param>
    internal static bool AcceptsConstruction(CompiledAbility plan, string required)
    {
        if (required == null) return false;
        bool found = false;
        foreach (CompiledForm node in plan.Nodes)
        {
            if (node.ContactRole == ConstructionContactRole.Decoration) continue;
            foreach (var effect in (node.Impact ?? plan.Impact).WorldEffects)
            {
                string action = effect.Action.Id;
                if (action != required && action is not ("land.cleanse" or "crop.mature")) return false;
                if (action is "terrain.raise" or "terrain.lower" or "water.drain")
                    if (effect.Contact.Mode != ConstructionContactMode.Once && effect.Contact.MaximumPerTarget != 1)
                        return false;
                found |= action == required;
            }
        }

        return found;
    }

    /// <summary>评价全部作用足迹的边界，但只用本次需求对应原理计算收益。</summary>
    /// <param name="option">完整预估。</param><param name="payload">城市需求。</param>
    /// <param name="allowed">合法空间。</param><param name="farms">实际与预留农田。</param>
    internal static float EvaluateConstructed(in MagicUtilitySpellOption option, CityMagicUtilityProjectPayload payload,
        ISet<int> allowed, ISet<int> farms)
    {
        if (option.Preview.AllTiles.Any(id => !allowed.Contains(id)) ||
            !option.Preview.Tiles.TryGetValue(option.Action, out var tiles)) return 0;
        return EvaluateExact(payload, tiles.ToArray(), farms);
    }

    /// <summary>按照已保存的具体足迹检查需求仍成立，不用包围圆替代刃面或路径。</summary>
    /// <param name="city">当前城市。</param><param name="payload">计划资料。</param>
    /// <param name="allowed">当前合法空间。</param>
    private static bool ValidateConstructed(City city, CityMagicUtilityProjectPayload payload, ISet<int> allowed)
    {
        if (payload.PlannedActionTileIds.Length == 0 ||
            payload.PlannedFootprintTileIds.Any(id => !allowed.Contains(id)))
            return false;
        bool pressure = payload.Goal switch
        {
            CityMagicUtilityProjectGoal.NatureGrowth => HasGrowthPressure(city),
            CityMagicUtilityProjectGoal.CropFertilization => HasFertilizationPressure(city),
            CityMagicUtilityProjectGoal.HousingTerrain => HasHousingPressure(city),
            CityMagicUtilityProjectGoal.FarmTerrain => HasFarmPressure(city),
            _ => true
        };
        return pressure && MagicUtilitySpellResolver.MeetsGoal(payload,
            EvaluateExact(payload, payload.PlannedActionTileIds,
                payload.Goal == CityMagicUtilityProjectGoal.NatureGrowth ? CollectFutureFarmTileIds(city) : null));
    }

    /// <summary>与原规则共用真实收益及地形的一步预测。</summary>
    /// <param name="payload">需求及阈值。</param><param name="ids">该原理的足迹。</param>
    /// <param name="farms">需要保护的农田。</param>
    private static float EvaluateExact(CityMagicUtilityProjectPayload payload, int[] ids, ISet<int> farms)
    {
        switch (payload.Goal)
        {
            case CityMagicUtilityProjectGoal.EmergencyClean:
            case CityMagicUtilityProjectGoal.RoutineClean:
                return EvaluateHazard(ids);
            case CityMagicUtilityProjectGoal.CropFertilization:
                return CountFertilizableCrops(ids);
            case CityMagicUtilityProjectGoal.NatureGrowth:
                return ids.Count(id => IsGrowthCandidate(ResolveTile(id), farms));
            case CityMagicUtilityProjectGoal.HousingTerrain:
            case CityMagicUtilityProjectGoal.FarmTerrain:
                int gained = 0;
                foreach (int id in ids)
                {
                    WorldTile tile = ResolveTile(id);
                    if (!CanApplyTerrainSemantic(tile, payload.EffectSemantic)) continue;
                    bool before = IsUsefulTile(tile, tile.Type, payload.Goal);
                    bool after = IsUsefulTile(tile, PredictTerrainType(tile, payload.EffectSemantic), payload.Goal);
                    if (before && !after || tile.Type.farm_field) return 0;
                    if (!before && after) gained++;
                }

                return gained;
            default: return 0;
        }
    }
}