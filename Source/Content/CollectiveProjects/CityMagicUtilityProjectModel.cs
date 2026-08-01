using System;
using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Core.CollectiveProjects;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3.Effects;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.CollectiveProjects;

/// <summary>首批由城市需求驱动的世界功能法术目标。</summary>
internal enum CityMagicUtilityProjectGoal
{
    /// <summary>清除足以打断普通工作的集中火灾或污染。</summary>
    EmergencyClean,

    /// <summary>在自然工作轮次处理零散地块污染。</summary>
    RoutineClean,

    /// <summary>在木材不足时催生不占用农田的本地植被。</summary>
    NatureGrowth,

    /// <summary>在住房压力下创造连续可建设地块。</summary>
    HousingTerrain,

    /// <summary>在粮食压力下创造连续可耕作地块。</summary>
    FarmTerrain,
}

/// <summary>城市功能法术项目在规划、执行和验收阶段共享的数据。</summary>
internal sealed class CityMagicUtilityProjectPayload
{
    /// <summary>项目所服务的城市需求。</summary>
    public CityMagicUtilityProjectGoal Goal;

    /// <summary>执行法术必须具备的规范效果语义。</summary>
    public SemanticAsset EffectSemantic;

    /// <summary>规划阶段要求的最低地块效用。</summary>
    public float MinimumUtility;

    /// <summary>永久地形项目要求新增的最低有效地块数。</summary>
    public int MinimumNetGain;

    /// <summary>规划法术的作用半径；执行器选用其他版本时会在认领后更新。</summary>
    public float PlannedRadius;

    /// <summary>规划时评估出的收益，用于项目和执行者排序。</summary>
    public float ExpectedUtility;

    /// <summary>执行时按照真实技能半径冻结的地块足迹。</summary>
    public int[] ExecutionFootprintTileIds = Array.Empty<int>();

    /// <summary>地形变化前已经有效的地块，用于验收“没有损失”。</summary>
    public int[] BaselineUsefulTileIds = Array.Empty<int>();

    /// <summary>净化危害值、植被数或有效地块数的执行前基线。</summary>
    public float BaselineMetric;

    /// <summary>只有实际提交施法前成功冻结基线后才允许验收。</summary>
    public bool HasExecutionBaseline;
}

/// <summary>永久地形候选在模拟一步变化后的新增与损失统计。</summary>
internal readonly struct TerrainProjectDelta
{
    public TerrainProjectDelta(int gained, int lost)
    {
        Gained = Math.Max(0, gained);
        Lost = Math.Max(0, lost);
    }

    public int Gained { get; }
    public int Lost { get; }
}

/// <summary>集中维护城市需求阈值、空间保护、地形预测和执行后验收规则。</summary>
internal static class CityMagicUtilityProjectRules
{
    public const float EmergencyCleanThreshold = 8f;
    public const int MinimumGrowthTiles = 8;
    public const int MinimumTerrainGain = 6;

    /// <summary>依据项目快照重新检查城市需求、目标范围和最低收益。</summary>
    public static bool Validate(CollectiveProjectView project)
    {
        if (!TryResolveProject(project, out City city, out WorldTile target,
                out CityMagicUtilityProjectPayload payload)) return false;
        HashSet<int> allowed = CollectAllowedTileIds(city);
        if (!IsAreaInsideScope(target.posV3, payload.PlannedRadius, allowed)) return false;

        return payload.Goal switch
        {
            CityMagicUtilityProjectGoal.EmergencyClean =>
                EvaluateHazard(target.posV3, payload.PlannedRadius, allowed) >= EmergencyCleanThreshold,
            CityMagicUtilityProjectGoal.RoutineClean =>
                EvaluateHazard(target.posV3, payload.PlannedRadius, allowed) is > 0f and < EmergencyCleanThreshold,
            CityMagicUtilityProjectGoal.NatureGrowth =>
                ValidateGrowthPlan(city, target, payload, allowed),
            CityMagicUtilityProjectGoal.HousingTerrain =>
                HasHousingPressure(city) && IsValidTerrainPlan(
                    EvaluatePredictedTerrainDelta(
                        target.posV3,
                        payload.PlannedRadius,
                        payload.EffectSemantic,
                        payload.Goal,
                        allowed),
                    payload.MinimumNetGain),
            CityMagicUtilityProjectGoal.FarmTerrain =>
                HasFarmPressure(city) && IsValidTerrainPlan(
                    EvaluatePredictedTerrainDelta(
                        target.posV3,
                        payload.PlannedRadius,
                        payload.EffectSemantic,
                        payload.Goal,
                        allowed),
                    payload.MinimumNetGain),
            _ => false,
        };
    }

    /// <summary>根据提交行动前冻结的真实足迹，判断本次世界变化是否达标。</summary>
    public static bool Verify(CollectiveProjectView project)
    {
        if (!TryResolveProject(project, out _, out _, out CityMagicUtilityProjectPayload payload) ||
            !payload.HasExecutionBaseline || payload.ExecutionFootprintTileIds.Length == 0) return false;

        switch (payload.Goal)
        {
            case CityMagicUtilityProjectGoal.EmergencyClean:
            case CityMagicUtilityProjectGoal.RoutineClean:
                return EvaluateHazard(payload.ExecutionFootprintTileIds) < payload.BaselineMetric;
            case CityMagicUtilityProjectGoal.NatureGrowth:
                return CountFlora(payload.ExecutionFootprintTileIds) > payload.BaselineMetric;
            case CityMagicUtilityProjectGoal.HousingTerrain:
            case CityMagicUtilityProjectGoal.FarmTerrain:
                for (int i = 0; i < payload.BaselineUsefulTileIds.Length; i++)
                {
                    WorldTile tile = ResolveTile(payload.BaselineUsefulTileIds[i]);
                    if (!IsUsefulTile(tile, tile?.Type, payload.Goal)) return false;
                }
                int useful = CountUsefulTiles(payload.ExecutionFootprintTileIds, payload.Goal);
                return useful >= payload.BaselineMetric + Math.Max(MinimumTerrainGain, payload.MinimumNetGain);
            default:
                return false;
        }
    }

    /// <summary>在实际施法扣费前冻结足迹与验收基线，避免用预测结果代替世界结算。</summary>
    public static bool CaptureExecutionBaseline(
        CollectiveProjectView project,
        float actualRadius)
    {
        if (!TryResolveProject(project, out City city, out WorldTile target,
                out CityMagicUtilityProjectPayload payload)) return false;
        payload.HasExecutionBaseline = false;
        payload.ExecutionFootprintTileIds = Array.Empty<int>();
        payload.BaselineUsefulTileIds = Array.Empty<int>();
        payload.BaselineMetric = 0f;
        HashSet<int> allowed = CollectAllowedTileIds(city);
        using var area = new ListPool<WorldTile>();
        SkillEffectResolver.CollectAreaTiles(target.posV3, actualRadius, area);
        if (area.Count == 0) return false;
        for (int i = 0; i < area.Count; i++)
        {
            if (!allowed.Contains(area[i].tile_id)) return false;
        }
        if (payload.Goal == CityMagicUtilityProjectGoal.NatureGrowth &&
            !IsGrowthAreaSafe(
                target.posV3,
                actualRadius,
                CollectFutureFarmTileIds(city))) return false;

        payload.ExecutionFootprintTileIds = new int[area.Count];
        for (int i = 0; i < area.Count; i++) payload.ExecutionFootprintTileIds[i] = area[i].tile_id;
        payload.PlannedRadius = actualRadius;
        switch (payload.Goal)
        {
            case CityMagicUtilityProjectGoal.EmergencyClean:
            case CityMagicUtilityProjectGoal.RoutineClean:
                payload.BaselineMetric = EvaluateHazard(payload.ExecutionFootprintTileIds);
                break;
            case CityMagicUtilityProjectGoal.NatureGrowth:
                payload.BaselineMetric = CountFlora(payload.ExecutionFootprintTileIds);
                break;
            case CityMagicUtilityProjectGoal.HousingTerrain:
            case CityMagicUtilityProjectGoal.FarmTerrain:
                var useful = new List<int>();
                for (int i = 0; i < area.Count; i++)
                {
                    WorldTile tile = area[i];
                    if (IsUsefulTile(tile, tile.Type, payload.Goal)) useful.Add(tile.tile_id);
                }
                payload.BaselineUsefulTileIds = useful.ToArray();
                payload.BaselineMetric = useful.Count;
                break;
            default:
                return false;
        }

        payload.HasExecutionBaseline = true;
        return true;
    }

    /// <summary>住房为空或无家可归人口出现时视为住房压力。</summary>
    public static bool HasHousingPressure(City city)
    {
        if (city == null) return false;
        int population = Math.Max(0, city.status.population);
        int reserve = Math.Max(2, (int)Math.Ceiling(population * 0.05f));
        return city.status.homeless > 0 || city.status.housing_free <= reserve;
    }

    /// <summary>饥饿出现或可用农田候选低于人口需求时视为农业压力。</summary>
    public static bool HasFarmPressure(City city)
    {
        if (city == null) return false;
        city.calculated_place_for_farms.checkAddRemove();
        int required = Math.Max(6, Math.Max(0, city.status.population) / 5);
        return city.status.hungry > 0 || city.calculated_place_for_farms.Count < required;
    }

    /// <summary>木材库存低于基础储备或人口两倍时允许规划催生植被。</summary>
    public static bool HasGrowthPressure(City city)
    {
        return city != null && city.amount_wood < Math.Max(20, Math.Max(0, city.status.population) * 2);
    }

    /// <summary>按火焰、热量、烧焦、冻结和荒地权重计算一个地块的净化收益。</summary>
    public static float EvaluateHazard(WorldTile tile)
    {
        if (tile == null || tile.Type.lava) return 0f;
        float utility = 0f;
        if (tile.isOnFire()) utility += 8f;
        if (tile.heat > 0) utility += 4f;
        if (tile.burned_stages > 0) utility += 3f;
        if (tile.isTemporaryFrozen()) utility += 2f;
        if (!tile.hasBuilding() && !tile.Type.road && tile.top_type?.wasteland == true) utility += 1f;
        return utility;
    }

    /// <summary>累计指定圆形范围内属于合法空间的危害值。</summary>
    public static float EvaluateHazard(Vector3 center, float radius, ISet<int> allowed)
    {
        using var area = new ListPool<WorldTile>();
        SkillEffectResolver.CollectAreaTiles(center, radius, area);
        float utility = 0f;
        for (int i = 0; i < area.Count; i++)
        {
            WorldTile tile = area[i];
            if (allowed == null || allowed.Contains(tile.tile_id)) utility += EvaluateHazard(tile);
        }
        return utility;
    }

    /// <summary>累计执行足迹当前的危害值。</summary>
    public static float EvaluateHazard(IReadOnlyList<int> tileIds)
    {
        float utility = 0f;
        for (int i = 0; i < tileIds.Count; i++) utility += EvaluateHazard(ResolveTile(tileIds[i]));
        return utility;
    }

    /// <summary>判断地块是否可用于催生，同时保护现有与预留农田。</summary>
    public static bool IsGrowthCandidate(WorldTile tile, ISet<int> futureFarmTileIds)
    {
        return tile != null && !tile.hasBuilding() && !tile.Type.road && !tile.Type.liquid &&
               !tile.Type.lava && !tile.Type.farm_field && tile.getBiome() != null &&
               (futureFarmTileIds == null || !futureFarmTileIds.Contains(tile.tile_id));
    }

    /// <summary>统计圆形范围内可用于催生的地块数量。</summary>
    public static int CountGrowthCandidates(
        Vector3 center,
        float radius,
        ISet<int> allowed,
        ISet<int> futureFarmTileIds)
    {
        using var area = new ListPool<WorldTile>();
        SkillEffectResolver.CollectAreaTiles(center, radius, area);
        int count = 0;
        for (int i = 0; i < area.Count; i++)
        {
            WorldTile tile = area[i];
            if (allowed.Contains(tile.tile_id) && IsGrowthCandidate(tile, futureFarmTileIds)) count++;
        }
        return count;
    }

    /// <summary>确保催生法术的真实圆形足迹不会触及任何仍可被生长效果占用的预留农田。</summary>
    public static bool IsGrowthAreaSafe(
        Vector3 center,
        float radius,
        ISet<int> futureFarmTileIds)
    {
        if (futureFarmTileIds == null || futureFarmTileIds.Count == 0) return true;
        using var area = new ListPool<WorldTile>();
        SkillEffectResolver.CollectAreaTiles(center, radius, area);
        for (int i = 0; i < area.Count; i++)
        {
            WorldTile tile = area[i];
            if (futureFarmTileIds.Contains(tile.tile_id) && IsGrowthCandidate(tile, null)) return false;
        }
        return true;
    }

    /// <summary>用实际技能预检模拟一次永久地形变化的净新增和损失。</summary>
    public static TerrainProjectDelta EvaluateTerrainDelta(
        ActorExtend caster,
        Entity skill,
        Vector3 center,
        float radius,
        SemanticAsset effectSemantic,
        CityMagicUtilityProjectGoal goal,
        ISet<int> allowed)
    {
        using var preview = new ListPool<SkillTilePreviewEntry>();
        SkillEffectResolver.CollectTilePreview(caster, skill, center, radius, preview);
        int gained = 0;
        int lost = 0;
        for (int i = 0; i < preview.Count; i++)
        {
            SkillTilePreviewEntry entry = preview[i];
            WorldTile tile = entry.Tile;
            if (!entry.Applicable || !allowed.Contains(tile.tile_id)) continue;
            TileType predicted = PredictTerrainType(tile, effectSemantic);
            bool before = IsUsefulTile(tile, tile.Type, goal);
            bool after = IsUsefulTile(tile, predicted, goal);
            if (!before && after) gained++;
            if (before && !after || tile.Type.farm_field) lost++;
        }
        return new TerrainProjectDelta(gained, lost);
    }

    /// <summary>不依赖具体法术容器，按首批地形法术的真实一步变化预测项目是否仍成立。</summary>
    public static TerrainProjectDelta EvaluatePredictedTerrainDelta(
        Vector3 center,
        float radius,
        SemanticAsset effectSemantic,
        CityMagicUtilityProjectGoal goal,
        ISet<int> allowed)
    {
        using var area = new ListPool<WorldTile>();
        SkillEffectResolver.CollectAreaTiles(center, radius, area);
        int gained = 0;
        int lost = 0;
        for (int i = 0; i < area.Count; i++)
        {
            WorldTile tile = area[i];
            if (!allowed.Contains(tile.tile_id) || !CanApplyTerrainSemantic(tile, effectSemantic)) continue;
            TileType predicted = PredictTerrainType(tile, effectSemantic);
            bool before = IsUsefulTile(tile, tile.Type, goal);
            bool after = IsUsefulTile(tile, predicted, goal);
            if (!before && after) gained++;
            if (before && !after || tile.Type.farm_field) lost++;
        }
        return new TerrainProjectDelta(gained, lost);
    }

    /// <summary>快速判断某一地块执行指定一步地形变化后是否会服务当前城市目标。</summary>
    public static bool WouldTerrainSemanticImprove(
        WorldTile tile,
        SemanticAsset effectSemantic,
        CityMagicUtilityProjectGoal goal)
    {
        if (!CanApplyTerrainSemantic(tile, effectSemantic)) return false;
        TileType predicted = PredictTerrainType(tile, effectSemantic);
        return !IsUsefulTile(tile, tile.Type, goal) && IsUsefulTile(tile, predicted, goal);
    }

    /// <summary>判断功能法术的整个离散足迹是否都位于本城或直接相邻无主范围。</summary>
    public static bool IsAreaInsideScope(Vector3 center, float radius, ISet<int> allowed)
    {
        using var area = new ListPool<WorldTile>();
        SkillEffectResolver.CollectAreaTiles(center, radius, area);
        if (area.Count == 0) return false;
        for (int i = 0; i < area.Count; i++)
        {
            if (!allowed.Contains(area[i].tile_id)) return false;
        }
        return true;
    }

    /// <summary>建立当前城市可以由组织工程修改的地块 ID 集合。</summary>
    public static HashSet<int> CollectAllowedTileIds(City city)
    {
        using var tiles = new ListPool<WorldTile>();
        CityCollectiveProjectOwnerAdapter.CollectScopeTiles(city, true, tiles);
        var result = new HashSet<int>();
        for (int i = 0; i < tiles.Count; i++) result.Add(tiles[i].tile_id);
        return result;
    }

    /// <summary>建立现有农田和原版城市农田候选的保护集合。</summary>
    public static HashSet<int> CollectFutureFarmTileIds(City city)
    {
        var result = new HashSet<int>();
        if (city == null) return result;
        city.calculated_place_for_farms.checkAddRemove();
        city.calculated_farm_fields.checkAddRemove();
        foreach (WorldTile tile in city.calculated_place_for_farms) result.Add(tile.tile_id);
        foreach (WorldTile tile in city.calculated_farm_fields) result.Add(tile.tile_id);
        return result;
    }

    /// <summary>解析项目所属城市、目标地块与类型化 payload。</summary>
    internal static bool TryResolveProject(
        CollectiveProjectView project,
        out City city,
        out WorldTile target,
        out CityMagicUtilityProjectPayload payload)
    {
        city = null;
        target = null;
        payload = project.Payload as CityMagicUtilityProjectPayload;
        if (payload == null || project.Owner.ProviderId != CityCollectiveProjectOwnerAdapter.ProviderId)
            return false;
        city = World.world?.cities?.get(project.Owner.OwnerId);
        target = ResolveTile(project.TargetTileId);
        return city != null && city.isAlive() && target != null;
    }

    /// <summary>永久地形计划必须达到新增目标，并且不能损失任何既有有效地块。</summary>
    private static bool IsValidTerrainPlan(in TerrainProjectDelta delta, int minimumNetGain)
    {
        return delta.Lost == 0 &&
               delta.Gained >= Math.Max(MinimumTerrainGain, minimumNetGain);
    }

    /// <summary>统一检查木材需求、预留农田保护和最低可催生地块数。</summary>
    private static bool ValidateGrowthPlan(
        City city,
        WorldTile target,
        CityMagicUtilityProjectPayload payload,
        ISet<int> allowed)
    {
        if (!HasGrowthPressure(city)) return false;
        HashSet<int> futureFarmTileIds = CollectFutureFarmTileIds(city);
        return IsGrowthAreaSafe(target.posV3, payload.PlannedRadius, futureFarmTileIds) &&
               CountGrowthCandidates(
                   target.posV3,
                   payload.PlannedRadius,
                   allowed,
                   futureFarmTileIds) >= MinimumGrowthTiles;
    }

    /// <summary>按语义预测地块执行一次抬升、降低或排水后的主地形。</summary>
    private static TileType PredictTerrainType(WorldTile tile, SemanticAsset effectSemantic)
    {
        if (tile?.main_type == null) return null;
        if (effectSemantic == SkillSemantics.Effect.RaiseTerrain) return tile.main_type.increase_to;
        if (effectSemantic == SkillSemantics.Effect.LowerTerrain) return tile.main_type.decrease_to;
        if (effectSemantic == SkillSemantics.Effect.DrainWater)
            return tile.top_type != null ? tile.main_type : tile.Type.decrease_to;
        return null;
    }

    /// <summary>镜像首批永久地形词条的预检条件，用于无容器的项目续存校验。</summary>
    private static bool CanApplyTerrainSemantic(WorldTile tile, SemanticAsset effectSemantic)
    {
        if (tile == null || tile.hasBuilding() || tile.Type.road || tile.Type.lava) return false;
        if (effectSemantic == SkillSemantics.Effect.RaiseTerrain) return tile.main_type?.increase_to != null;
        if (effectSemantic == SkillSemantics.Effect.LowerTerrain) return tile.main_type?.decrease_to != null;
        if (effectSemantic == SkillSemantics.Effect.DrainWater)
        {
            TileType predicted = PredictTerrainType(tile, effectSemantic);
            return tile.Type.ocean && !tile.Type.lava && predicted != null && !predicted.ocean;
        }
        return false;
    }

    /// <summary>按住房或农业目标判断一个当前/预测地形是否提供有效空地。</summary>
    private static bool IsUsefulTile(
        WorldTile tile,
        TileTypeBase type,
        CityMagicUtilityProjectGoal goal)
    {
        if (tile == null || type == null || tile.hasBuilding() || type.road || type.liquid || type.lava)
            return false;
        return goal == CityMagicUtilityProjectGoal.FarmTerrain
            ? type.can_be_farm || type.farm_field
            : type.can_build_on;
    }

    /// <summary>统计足迹中当前仍然有效的住房/农田空地。</summary>
    private static int CountUsefulTiles(
        IReadOnlyList<int> tileIds,
        CityMagicUtilityProjectGoal goal)
    {
        int count = 0;
        for (int i = 0; i < tileIds.Count; i++)
        {
            WorldTile tile = ResolveTile(tileIds[i]);
            if (IsUsefulTile(tile, tile?.Type, goal)) count++;
        }
        return count;
    }

    /// <summary>统计足迹中当前存在的植被建筑数。</summary>
    private static int CountFlora(IReadOnlyList<int> tileIds)
    {
        int count = 0;
        for (int i = 0; i < tileIds.Count; i++)
        {
            Building building = ResolveTile(tileIds[i])?.building;
            if (building?.asset?.flora == true) count++;
        }
        return count;
    }

    /// <summary>按世界地块稳定 ID 解析地块。</summary>
    private static WorldTile ResolveTile(int tileId)
    {
        WorldTile[] tiles = World.world?.tiles_list;
        return tiles != null && tileId >= 0 && tileId < tiles.Length ? tiles[tileId] : null;
    }
}
