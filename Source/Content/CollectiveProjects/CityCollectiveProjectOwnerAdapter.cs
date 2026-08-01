using System.Collections.Generic;
using Cultiway.Core.CollectiveProjects;

namespace Cultiway.Content.CollectiveProjects;

/// <summary>把原版城市的成员与领地边界适配为通用集体工程所有者。</summary>
internal sealed class CityCollectiveProjectOwnerAdapter : ICollectiveProjectOwnerAdapter
{
    public const string ProviderId = "worldbox.city";

    public string Id => ProviderId;

    /// <summary>枚举当前世界仍由城市管理器持有的全部城市。</summary>
    public IEnumerable<NanoObject> EnumerateOwners()
    {
        if (World.world?.cities == null) yield break;
        foreach (City city in World.world.cities) yield return city;
    }

    /// <summary>按世界内城市 ID 重新解析所有者。</summary>
    public bool TryResolve(long ownerId, out NanoObject owner)
    {
        City city = World.world?.cities?.get(ownerId);
        owner = city;
        return city != null;
    }

    /// <summary>枚举原版城市成员列表，不复制角色对象。</summary>
    public IEnumerable<Actor> EnumerateMembers(NanoObject owner)
    {
        return owner is City city ? city.units : System.Array.Empty<Actor>();
    }

    /// <summary>以角色当前城市关系作为成员归属的唯一依据。</summary>
    public bool IsMember(NanoObject owner, Actor actor)
    {
        return owner is City city && actor != null && actor.city == city;
    }

    /// <summary>城市成员暂不区分职位亲和，具体执行能力由工程执行器评分。</summary>
    public float ResolveMemberAffinity(NanoObject owner, Actor actor)
    {
        return IsMember(owner, actor) ? 1f : 0f;
    }

    /// <summary>
    /// 收集城市自有区域，或自有区域加直接相邻的无主/本城区域；任何外国城市区域都不会进入结果。
    /// </summary>
    public bool CollectTiles(
        NanoObject owner,
        in CollectiveProjectSpatialRequest request,
        ICollection<WorldTile> output)
    {
        if (owner is not City city || output == null) return false;
        if (request.ScopeId != CollectiveProjectSpatialRequest.Primary &&
            request.ScopeId != CollectiveProjectSpatialRequest.PrimaryAdjacent) return false;

        return CollectScopeTiles(
            city,
            request.ScopeId == CollectiveProjectSpatialRequest.PrimaryAdjacent,
            output);
    }

    /// <summary>供城市规划与执行阶段按同一边界规则重建合法地块集合。</summary>
    internal static bool CollectScopeTiles(
        City city,
        bool includeAdjacent,
        ICollection<WorldTile> output)
    {
        if (city == null || output == null) return false;
        var seen = new HashSet<int>();
        AddZones(city.zones, city, output, seen);
        if (includeAdjacent) AddZones(city.neighbour_zones, city, output, seen);
        return seen.Count > 0;
    }

    /// <summary>把合法区域中的地块去重写入调用方集合。</summary>
    private static void AddZones(
        IEnumerable<TileZone> zones,
        City owner,
        ICollection<WorldTile> output,
        ISet<int> seen)
    {
        foreach (TileZone zone in zones)
        {
            if (zone == null || zone.city != null && zone.city != owner) continue;
            WorldTile[] tiles = zone.tiles;
            for (int i = 0; i < tiles.Length; i++)
            {
                WorldTile tile = tiles[i];
                if (tile != null && seen.Add(tile.tile_id)) output.Add(tile);
            }
        }
    }
}
