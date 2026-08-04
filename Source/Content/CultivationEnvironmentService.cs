using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>为环境修炼寻找并评分附近可达的修炼地点。</summary>
public static class CultivationEnvironmentService
{
    /// <summary>环境修炼不会驱使角色进行远距离朝圣的最大搜索半径。</summary>
    public const int SearchRadius = 20;

    /// <summary>取得角色当前方式的最佳附近地点；没有合格地点时回退到当前地块。</summary>
    public static WorldTile ResolveSite(ActorExtend actor, CultivateMethodAsset method)
    {
        if (actor?.Base?.current_tile == null || method?.EnvironmentRule == null)
            return actor?.Base?.current_tile;

        WorldTile best = FindBestSite(actor, method.EnvironmentRule, out _);
        return best ?? actor.Base.current_tile;
    }

    /// <summary>为功法生成计算附近最佳环境质量，不改变角色当前寻路目标。</summary>
    public static float ResolveBestNearbyQuality(ActorExtend actor, CultivationEnvironmentRule rule)
    {
        if (actor?.Base?.current_tile == null || rule == null) return 0f;
        FindBestSite(actor, rule, out float quality);
        return quality;
    }

    /// <summary>在半径内按环境质量优先、距离次优的规则选择地点。</summary>
    private static WorldTile FindBestSite(
        ActorExtend actor,
        CultivationEnvironmentRule rule,
        out float bestQuality)
    {
        WorldTile origin = actor.Base.current_tile;
        WorldTile best = null;
        bestQuality = 0f;
        float bestScore = float.MinValue;
        int radiusSquared = SearchRadius * SearchRadius;

        for (var dx = -SearchRadius; dx <= SearchRadius; dx++)
        for (var dy = -SearchRadius; dy <= SearchRadius; dy++)
        {
            int distanceSquared = dx * dx + dy * dy;
            if (distanceSquared > radiusSquared) continue;
            int x = origin.x + dx;
            int y = origin.y + dy;
            if (x < 0 || y < 0 || x >= MapBox.width || y >= MapBox.height) continue;

            WorldTile candidate = World.world.GetTileSimple(x, y);
            if (!IsCandidateUsable(actor, rule, candidate) || !IsCurrentOrAdjacentRegion(origin, candidate))
                continue;

            float quality = rule.ResolveQuality(actor, candidate);
            if (quality <= 0f) continue;
            float score = quality * 10f - Mathf.Sqrt(distanceSquared) * 0.1f;
            if (score <= bestScore) continue;
            best = candidate;
            bestQuality = quality;
            bestScore = score;
        }

        return best;
    }

    /// <summary>检查地块是否满足露天要求及该方式声明的移动和危险约束。</summary>
    private static bool IsCandidateUsable(
        ActorExtend actor,
        CultivationEnvironmentRule rule,
        WorldTile tile)
    {
        if (tile?.Type == null) return false;
        if (rule.PreferOutdoors && tile.hasBuilding()) return false;
        if (tile.IsWater() && !rule.WalkOnWater) return false;
        if (tile.Type.block && !rule.WalkOnBlocks) return false;
        if (tile.Type.lava && actor.Base.asset.die_in_lava && !actor.Base.isImmuneToFire()) return false;
        if (tile.Type.damage_units && !rule.AllowDamagingTerrain) return false;
        if (tile.isOnFire() && !actor.Base.isImmuneToFire() && !rule.AllowDamagingTerrain) return false;
        return true;
    }

    /// <summary>限制搜索在当前地理区域和直接相邻区域内。</summary>
    private static bool IsCurrentOrAdjacentRegion(WorldTile origin, WorldTile candidate)
    {
        MapRegion originRegion = origin.region;
        MapRegion candidateRegion = candidate.region;
        if (originRegion == null || candidateRegion == null || originRegion == candidateRegion) return true;
        return originRegion.neighbours.Contains(candidateRegion);
    }

}
