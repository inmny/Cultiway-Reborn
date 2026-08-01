using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core.CollectiveProjects;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3.Effects;
using UnityEngine;
using static Cultiway.Content.CollectiveProjects.CityMagicUtilityProjectPlannerSearch;

namespace Cultiway.Content.CollectiveProjects;

/// <summary>城市功能法术工程使用的稳定注册标识。</summary>
internal static class CityMagicUtilityProjectIds
{
    public const string Executor = "cultiway.magic_world_project";
    public const string EmergencyClean = "cultiway.city_magic.emergency_clean";
    public const string RoutineClean = "cultiway.city_magic.routine_clean";
    public const string NatureGrowth = "cultiway.city_magic.nature_growth";
    public const string CropFertilization = "cultiway.city_magic.crop_fertilization";
    public const string HousingTerrain = "cultiway.city_magic.housing_terrain";
    public const string FarmTerrain = "cultiway.city_magic.farm_terrain";
    public const string EmergencyPlanner = "cultiway.city_magic.emergency_planner";
    public const string RoutinePlanner = "cultiway.city_magic.routine_planner";
    public const string PermanentWorldChangeBudget = "cultiway.city_magic.permanent_world_change";
}

/// <summary>高频但分帧扫描城市集中污染，并只发布达到应急阈值的净化项目。</summary>
internal sealed class CityEmergencyMagicProjectPlanner : ICollectiveProjectPlanner
{
    public string Id => CityMagicUtilityProjectIds.EmergencyPlanner;
    public string OwnerProviderId => CityCollectiveProjectOwnerAdapter.ProviderId;
    public double IntervalSeconds => 1d;
    public int OwnersPerUpdate => 4;

    /// <summary>为单个城市选择收益最高的应急净化落点和一个可行法术版本。</summary>
    public void CollectProposals(
        in CollectiveProjectOwnerContext owner,
        ICollection<CollectiveProjectProposal> output)
    {
        if (owner.Owner is not City city) return;
        using var scope = new ListPool<WorldTile>();
        var request = new CollectiveProjectSpatialRequest(CollectiveProjectSpatialRequest.PrimaryAdjacent);
        if (!owner.Adapter.CollectTiles(city, in request, scope)) return;
        HashSet<int> allowed = ToTileIds(scope);

        using var hazardCenters = new ListPool<WorldTile>();
        for (int i = 0; i < scope.Count; i++)
        {
            if (CityMagicUtilityProjectRules.EvaluateHazard(scope[i]) > 0f) hazardCenters.Add(scope[i]);
        }
        if (hazardCenters.Count == 0) return;

        using var options = new ListPool<MagicUtilitySpellOption>();
        MagicUtilitySpellResolver.CollectCityOptions(
            city,
            SkillSemantics.Effect.Cleanse,
            true,
            options);
        var payload = new CityMagicUtilityProjectPayload
        {
            Goal = CityMagicUtilityProjectGoal.EmergencyClean,
            EffectSemantic = SkillSemantics.Effect.Cleanse,
            MinimumUtility = CityMagicUtilityProjectRules.EmergencyCleanThreshold,
        };
        if (!TryFindBestArea(options, hazardCenters, payload, allowed, null,
                out MagicUtilityPlanCandidate best)) return;

        payload.PlannedRadius = best.Option.Radius;
        payload.ExpectedUtility = best.Utility;
        output.Add(new CollectiveProjectProposal
        {
            DefinitionId = CityMagicUtilityProjectIds.EmergencyClean,
            DeduplicationKey = CityMagicUtilityProjectGoal.EmergencyClean.ToString(),
            Owner = owner.Key,
            TargetTileId = best.Target.tile_id,
            Payload = payload,
            Urgency = CollectiveProjectUrgency.Emergency,
            Priority = 100f + best.Utility,
            HistoryTag = "clean.emergency",
        });
    }
}

/// <summary>按月规划常规净化，并在三种永久世界需求中只保留优先级最高的一项。</summary>
internal sealed class CityRoutineMagicProjectPlanner : ICollectiveProjectPlanner
{
    public string Id => CityMagicUtilityProjectIds.RoutinePlanner;
    public string OwnerProviderId => CityCollectiveProjectOwnerAdapter.ProviderId;
    public double IntervalSeconds => TimeScales.SecPerMonth;
    public int OwnersPerUpdate => 2;

    /// <summary>生成常规净化、饥荒施肥和至多一个木材/住房/农田工程提案。</summary>
    public void CollectProposals(
        in CollectiveProjectOwnerContext owner,
        ICollection<CollectiveProjectProposal> output)
    {
        if (owner.Owner is not City city) return;
        using var scope = new ListPool<WorldTile>();
        var request = new CollectiveProjectSpatialRequest(CollectiveProjectSpatialRequest.PrimaryAdjacent);
        if (!owner.Adapter.CollectTiles(city, in request, scope)) return;
        HashSet<int> allowed = ToTileIds(scope);

        TryAddRoutineClean(owner.Key, city, scope, allowed, output);
        TryAddCropFertilization(owner.Key, city, scope, allowed, output);

        PlannedProjectCandidate permanent = default;
        bool foundPermanent = false;
        if (TryPlanGrowth(city, scope, allowed, out PlannedProjectCandidate growth))
            SelectPermanent(growth, ref foundPermanent, ref permanent);
        if (CityMagicUtilityProjectRules.HasHousingPressure(city) &&
            TryPlanTerrain(city, scope, allowed, CityMagicUtilityProjectGoal.HousingTerrain,
                out PlannedProjectCandidate housing))
            SelectPermanent(housing, ref foundPermanent, ref permanent);
        if (CityMagicUtilityProjectRules.HasFarmPressure(city) &&
            TryPlanTerrain(city, scope, allowed, CityMagicUtilityProjectGoal.FarmTerrain,
                out PlannedProjectCandidate farm))
            SelectPermanent(farm, ref foundPermanent, ref permanent);

        if (foundPermanent) output.Add(permanent.ToProposal(owner.Key));
    }

    /// <summary>污染收益低于应急阈值时发布不抢占任务的常规净化项目。</summary>
    private static void TryAddRoutineClean(
        CollectiveProjectOwnerKey owner,
        City city,
        IReadOnlyList<WorldTile> scope,
        HashSet<int> allowed,
        ICollection<CollectiveProjectProposal> output)
    {
        using var centers = new ListPool<WorldTile>();
        for (int i = 0; i < scope.Count; i++)
        {
            if (CityMagicUtilityProjectRules.EvaluateHazard(scope[i]) > 0f) centers.Add(scope[i]);
        }
        if (centers.Count == 0) return;

        using var options = new ListPool<MagicUtilitySpellOption>();
        MagicUtilitySpellResolver.CollectCityOptions(city, SkillSemantics.Effect.Cleanse, false, options);
        var payload = new CityMagicUtilityProjectPayload
        {
            Goal = CityMagicUtilityProjectGoal.RoutineClean,
            EffectSemantic = SkillSemantics.Effect.Cleanse,
            MinimumUtility = 0.01f,
        };
        if (!TryFindBestArea(options, centers, payload, allowed, null,
                out MagicUtilityPlanCandidate best) ||
            best.Utility >= CityMagicUtilityProjectRules.EmergencyCleanThreshold) return;

        payload.PlannedRadius = best.Option.Radius;
        payload.ExpectedUtility = best.Utility;
        output.Add(new CollectiveProjectProposal
        {
            DefinitionId = CityMagicUtilityProjectIds.RoutineClean,
            DeduplicationKey = CityMagicUtilityProjectGoal.RoutineClean.ToString(),
            Owner = owner,
            TargetTileId = best.Target.tile_id,
            Payload = payload,
            Urgency = CollectiveProjectUrgency.Routine,
            Priority = 20f + best.Utility,
            HistoryTag = "clean.routine",
        });
    }

    /// <summary>在城市已经有人挨饿时，为能够覆盖最多未成熟麦田的施肥法术发布项目。</summary>
    private static void TryAddCropFertilization(
        CollectiveProjectOwnerKey owner,
        City city,
        IReadOnlyList<WorldTile> scope,
        HashSet<int> allowed,
        ICollection<CollectiveProjectProposal> output)
    {
        if (!CityMagicUtilityProjectRules.HasFertilizationPressure(city)) return;
        using var centers = new ListPool<WorldTile>();
        for (int i = 0; i < scope.Count; i++)
        {
            if (CityMagicUtilityProjectRules.IsFertilizableCrop(scope[i])) centers.Add(scope[i]);
        }
        if (centers.Count == 0) return;

        using var options = new ListPool<MagicUtilitySpellOption>();
        MagicUtilitySpellResolver.CollectCityOptions(city, SkillSemantics.Effect.Fertilize, false, options);
        var payload = new CityMagicUtilityProjectPayload
        {
            Goal = CityMagicUtilityProjectGoal.CropFertilization,
            EffectSemantic = SkillSemantics.Effect.Fertilize,
            MinimumUtility = CityMagicUtilityProjectRules.MinimumFertilizableCrops,
        };
        if (!TryFindBestArea(options, centers, payload, allowed, null,
                out MagicUtilityPlanCandidate best)) return;

        payload.PlannedRadius = best.Option.Radius;
        payload.ExpectedUtility = best.Utility;
        output.Add(new CollectiveProjectProposal
        {
            DefinitionId = CityMagicUtilityProjectIds.CropFertilization,
            DeduplicationKey = CityMagicUtilityProjectGoal.CropFertilization.ToString(),
            Owner = owner,
            TargetTileId = best.Target.tile_id,
            Payload = payload,
            Urgency = CollectiveProjectUrgency.Routine,
            Priority = 40f + city.status.hungry * 3f + best.Utility,
            HistoryTag = "farm.fertilize",
        });
    }

    /// <summary>在木材不足且至少能覆盖八个非农田地块时规划自然催生。</summary>
    private static bool TryPlanGrowth(
        City city,
        IReadOnlyList<WorldTile> scope,
        HashSet<int> allowed,
        out PlannedProjectCandidate planned)
    {
        planned = default;
        if (!CityMagicUtilityProjectRules.HasGrowthPressure(city)) return false;
        HashSet<int> futureFarmTiles = CityMagicUtilityProjectRules.CollectFutureFarmTileIds(city);
        using var centers = new ListPool<WorldTile>();
        for (int i = 0; i < scope.Count; i++)
        {
            if (CityMagicUtilityProjectRules.IsGrowthCandidate(scope[i], futureFarmTiles)) centers.Add(scope[i]);
        }
        using var options = new ListPool<MagicUtilitySpellOption>();
        MagicUtilitySpellResolver.CollectCityOptions(city, SkillSemantics.Effect.Growth, false, options);
        var payload = new CityMagicUtilityProjectPayload
        {
            Goal = CityMagicUtilityProjectGoal.NatureGrowth,
            EffectSemantic = SkillSemantics.Effect.Growth,
            MinimumUtility = CityMagicUtilityProjectRules.MinimumGrowthTiles,
        };
        if (!TryFindBestArea(options, centers, payload, allowed, futureFarmTiles,
                out MagicUtilityPlanCandidate best)) return false;

        payload.PlannedRadius = best.Option.Radius;
        payload.ExpectedUtility = best.Utility;
        int requiredWood = Math.Max(20, Math.Max(0, city.status.population) * 2);
        float shortageRatio = requiredWood <= 0
            ? 0f
            : Mathf.Clamp01((requiredWood - city.amount_wood) / (float)requiredWood);
        planned = new PlannedProjectCandidate(
            CityMagicUtilityProjectIds.NatureGrowth,
            CityMagicUtilityProjectGoal.NatureGrowth,
            best.Target,
            payload,
            30f + shortageRatio * 20f + best.Utility * 0.25f,
            "world.growth");
        return true;
    }

    /// <summary>在抬升、降低和排水法术中寻找零损失且净增六格的最佳地形方案。</summary>
    private static bool TryPlanTerrain(
        City city,
        IReadOnlyList<WorldTile> scope,
        HashSet<int> allowed,
        CityMagicUtilityProjectGoal goal,
        out PlannedProjectCandidate planned)
    {
        planned = default;
        MagicUtilityPlanCandidate best = default;
        SemanticAsset bestSemantic = null;
        bool found = false;
        SemanticAsset[] semantics =
        {
            SkillSemantics.Effect.RaiseTerrain,
            SkillSemantics.Effect.LowerTerrain,
            SkillSemantics.Effect.DrainWater,
        };
        for (int semanticIndex = 0; semanticIndex < semantics.Length; semanticIndex++)
        {
            SemanticAsset semantic = semantics[semanticIndex];
            using var options = new ListPool<MagicUtilitySpellOption>();
            MagicUtilitySpellResolver.CollectCityOptions(city, semantic, false, options);
            for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
            {
                MagicUtilitySpellOption option = options[optionIndex];
                var centerIds = new HashSet<int>();
                using var nearby = new ListPool<WorldTile>();
                for (int tileIndex = 0; tileIndex < scope.Count; tileIndex++)
                {
                    WorldTile tile = scope[tileIndex];
                    if (!CityMagicUtilityProjectRules.WouldTerrainSemanticImprove(tile, semantic, goal)) continue;
                    nearby.Clear();
                    SkillEffectResolver.CollectAreaTiles(tile.posV3, option.Radius, nearby);
                    for (int nearbyIndex = 0; nearbyIndex < nearby.Count; nearbyIndex++)
                    {
                        WorldTile center = nearby[nearbyIndex];
                        if (allowed.Contains(center.tile_id)) centerIds.Add(center.tile_id);
                    }
                }

                var payload = new CityMagicUtilityProjectPayload
                {
                    Goal = goal,
                    EffectSemantic = semantic,
                    MinimumUtility = CityMagicUtilityProjectRules.MinimumTerrainGain,
                    MinimumNetGain = CityMagicUtilityProjectRules.MinimumTerrainGain,
                    PlannedRadius = option.Radius,
                };
                foreach (int centerId in centerIds)
                {
                    WorldTile center = ResolveTile(centerId);
                    if (center == null) continue;
                    float utility = MagicUtilitySpellResolver.EvaluateOption(
                        option,
                        center,
                        payload,
                        allowed);
                    if (!MagicUtilitySpellResolver.MeetsGoal(payload, utility)) continue;
                    var candidate = new MagicUtilityPlanCandidate(option, center, utility);
                    if (!found || IsBetter(candidate, best))
                    {
                        found = true;
                        best = candidate;
                        bestSemantic = semantic;
                    }
                }
            }
        }
        if (!found) return false;

        var selectedPayload = new CityMagicUtilityProjectPayload
        {
            Goal = goal,
            EffectSemantic = bestSemantic,
            MinimumUtility = CityMagicUtilityProjectRules.MinimumTerrainGain,
            MinimumNetGain = CityMagicUtilityProjectRules.MinimumTerrainGain,
            PlannedRadius = best.Option.Radius,
            ExpectedUtility = best.Utility,
        };
        bool housing = goal == CityMagicUtilityProjectGoal.HousingTerrain;
        float pressure = housing
            ? city.status.homeless * 5f + Math.Max(0, 2 - city.status.housing_free) * 2f
            : city.status.hungry * 5f + Math.Max(0, Math.Max(6, city.status.population / 5) -
                                                  city.calculated_place_for_farms.Count) * 2f;
        planned = new PlannedProjectCandidate(
            housing ? CityMagicUtilityProjectIds.HousingTerrain : CityMagicUtilityProjectIds.FarmTerrain,
            goal,
            best.Target,
            selectedPayload,
            50f + pressure + best.Utility,
            ResolveTerrainHistoryTag(bestSemantic),
            ResolveInverseTerrainTags(bestSemantic),
            TimeScales.SecPerYear * 5d,
            Math.Max(1f, best.Option.Radius * 2f));
        return true;
    }

    /// <summary>选择优先级最高的永久项目，并用收益和落点 ID 打破平局。</summary>
    private static void SelectPermanent(
        in PlannedProjectCandidate candidate,
        ref bool found,
        ref PlannedProjectCandidate selected)
    {
        if (found && !candidate.IsBetterThan(selected)) return;
        found = true;
        selected = candidate;
    }
}

/// <summary>城市规划器共享的候选搜索与确定性排序函数。</summary>
internal static class CityMagicUtilityProjectPlannerSearch
{
    /// <summary>从一组落点和法术版本中选择收益最高、消耗最低的候选。</summary>
    public static bool TryFindBestArea(
        IReadOnlyList<MagicUtilitySpellOption> options,
        IReadOnlyList<WorldTile> centers,
        CityMagicUtilityProjectPayload payload,
        ISet<int> allowed,
        ISet<int> futureFarmTiles,
        out MagicUtilityPlanCandidate best)
    {
        best = default;
        bool found = false;
        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            MagicUtilitySpellOption option = options[optionIndex];
            for (int centerIndex = 0; centerIndex < centers.Count; centerIndex++)
            {
                WorldTile center = centers[centerIndex];
                float utility = MagicUtilitySpellResolver.EvaluateOption(
                    option,
                    center,
                    payload,
                    allowed,
                    futureFarmTiles);
                if (!MagicUtilitySpellResolver.MeetsGoal(payload, utility)) continue;
                var candidate = new MagicUtilityPlanCandidate(option, center, utility);
                if (!found || IsBetter(candidate, best))
                {
                    found = true;
                    best = candidate;
                }
            }
        }
        return found;
    }

    /// <summary>按收益、资源需求、容器 ID 和落点 ID 形成稳定顺序。</summary>
    public static bool IsBetter(in MagicUtilityPlanCandidate candidate, in MagicUtilityPlanCandidate current)
    {
        if (!Mathf.Approximately(candidate.Utility, current.Utility))
            return candidate.Utility > current.Utility;
        if (!Mathf.Approximately(candidate.Option.Demand, current.Option.Demand))
            return candidate.Option.Demand < current.Option.Demand;
        if (candidate.Option.Skill.Id != current.Option.Skill.Id)
            return candidate.Option.Skill.Id < current.Option.Skill.Id;
        return candidate.Target.tile_id < current.Target.tile_id;
    }

    /// <summary>把地块列表转换为稳定 ID 集合。</summary>
    public static HashSet<int> ToTileIds(IReadOnlyList<WorldTile> tiles)
    {
        var result = new HashSet<int>();
        for (int i = 0; i < tiles.Count; i++) result.Add(tiles[i].tile_id);
        return result;
    }

    /// <summary>返回永久地形操作对应的历史标签。</summary>
    public static string ResolveTerrainHistoryTag(SemanticAsset semantic)
    {
        if (semantic == SkillSemantics.Effect.RaiseTerrain) return "terrain.raise";
        if (semantic == SkillSemantics.Effect.LowerTerrain) return "terrain.lower";
        return "terrain.drain";
    }

    /// <summary>返回五年内不能在相邻足迹反向执行的历史标签。</summary>
    public static string[] ResolveInverseTerrainTags(SemanticAsset semantic)
    {
        return semantic == SkillSemantics.Effect.RaiseTerrain
            ? new[] { "terrain.lower", "terrain.drain" }
            : new[] { "terrain.raise" };
    }

    /// <summary>按世界稳定 ID 解析规划候选地块。</summary>
    public static WorldTile ResolveTile(int tileId)
    {
        WorldTile[] tiles = World.world?.tiles_list;
        return tiles != null && tileId >= 0 && tileId < tiles.Length ? tiles[tileId] : null;
    }
}

/// <summary>单个具体法术版本在一个落点上的规划收益。</summary>
internal readonly struct MagicUtilityPlanCandidate
{
    public MagicUtilityPlanCandidate(
        MagicUtilitySpellOption option,
        WorldTile target,
        float utility)
    {
        Option = option;
        Target = target;
        Utility = utility;
    }

    public MagicUtilitySpellOption Option { get; }
    public WorldTile Target { get; }
    public float Utility { get; }
}

/// <summary>常规规划器提交前保存的一项完整永久工程。</summary>
internal readonly struct PlannedProjectCandidate
{
    public PlannedProjectCandidate(
        string definitionId,
        CityMagicUtilityProjectGoal goal,
        WorldTile target,
        CityMagicUtilityProjectPayload payload,
        float priority,
        string historyTag,
        string[] conflictingHistoryTags = null,
        double conflictWindowSeconds = 0d,
        float conflictRadius = 0f)
    {
        DefinitionId = definitionId;
        Goal = goal;
        Target = target;
        Payload = payload;
        Priority = priority;
        HistoryTag = historyTag;
        ConflictingHistoryTags = conflictingHistoryTags ?? Array.Empty<string>();
        ConflictWindowSeconds = conflictWindowSeconds;
        ConflictRadius = conflictRadius;
    }

    public string DefinitionId { get; }
    public CityMagicUtilityProjectGoal Goal { get; }
    public WorldTile Target { get; }
    public CityMagicUtilityProjectPayload Payload { get; }
    public float Priority { get; }
    public string HistoryTag { get; }
    public string[] ConflictingHistoryTags { get; }
    public double ConflictWindowSeconds { get; }
    public float ConflictRadius { get; }

    /// <summary>转换为通用工程服务接受的提案。</summary>
    public CollectiveProjectProposal ToProposal(CollectiveProjectOwnerKey owner)
    {
        return new CollectiveProjectProposal
        {
            DefinitionId = DefinitionId,
            DeduplicationKey = Goal.ToString(),
            Owner = owner,
            TargetTileId = Target.tile_id,
            Payload = Payload,
            Urgency = CollectiveProjectUrgency.Routine,
            Priority = Priority,
            HistoryTag = HistoryTag,
            ConflictingHistoryTags = ConflictingHistoryTags,
            ConflictWindowSeconds = ConflictWindowSeconds,
            ConflictRadius = ConflictRadius,
        };
    }

    /// <summary>按项目优先级、预期收益和落点 ID 比较两个永久需求。</summary>
    public bool IsBetterThan(in PlannedProjectCandidate other)
    {
        if (!Mathf.Approximately(Priority, other.Priority)) return Priority > other.Priority;
        if (!Mathf.Approximately(Payload.ExpectedUtility, other.Payload.ExpectedUtility))
            return Payload.ExpectedUtility > other.Payload.ExpectedUtility;
        return Target.tile_id < other.Target.tile_id;
    }
}
