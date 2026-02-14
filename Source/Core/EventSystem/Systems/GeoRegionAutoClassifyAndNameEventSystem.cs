using System.Collections.Generic;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Libraries;

namespace Cultiway.Core.EventSystem.Systems;

/// <summary>
/// GeoRegion 生成后自动分类，并基于原版命名器结果做最小化去重。
/// </summary>
public class GeoRegionAutoClassifyAndNameEventSystem : GenericEventSystem<GeoRegionGeneratedEvent>
{
    protected override int MaxEventsPerUpdate => 4096;
    private int _currentSeedId;
    private readonly HashSet<string> _usedResolvedNames = new();
    private readonly Dictionary<string, int> _baseNameCounters = new();

    protected override void HandleEvent(GeoRegionGeneratedEvent evt)
    {
        if (evt.RegionId == 0) return;
        if (evt.WorldSeedId != _currentSeedId)
        {
            _currentSeedId = evt.WorldSeedId;
            _usedResolvedNames.Clear();
            _baseNameCounters.Clear();
        }

        var region = WorldboxGame.I?.GeoRegions?.get(evt.RegionId);
        if (region == null || region.isRekt()) return;

        var lib = ModClass.L.GeoRegionLibrary;
        var category = ResolveCategory(lib, evt);
        if (category == null) return;

        region.data.Layer = evt.Layer;
        region.data.CategoryId = category.id;
        region.data.CenterX = evt.CenterX;
        region.data.CenterY = evt.CenterY;
        region.data.TileCount = evt.TileCount;

        // 先拿原版命名器名称，再做轻量去重（仅重名时才加方位词，且不使用“部”）。
        var vanillaName = EnsureVanillaName(region);
        var normalizedName = NormalizeDirectionalWord(vanillaName);
        region.data.name = MakeUniqueName(normalizedName, evt);
        region.data.custom_name = false;
    }

    /// <summary>
    /// 获取原版命名器名称；若当前为空则即时生成。
    /// </summary>
    private static string EnsureVanillaName(GeoRegion region)
    {
        if (string.IsNullOrWhiteSpace(region.data.name))
        {
            region.data.name = NameGenerator.getName(
                WorldboxGame.NameGenerators.GeoRegion.id,
                ActorSex.Male,
                true,
                null,
                World.world.map_stats.life_dna);
        }

        return string.IsNullOrWhiteSpace(region.data.name) ? "GeoRegion" : region.data.name.Trim();
    }

    /// <summary>
    /// 统一移除“东部/西部/南部/北部/中部”等写法里的“部”。
    /// </summary>
    private static string NormalizeDirectionalWord(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "GeoRegion";

        return name.Trim()
            .Replace("东北部", "东北")
            .Replace("西北部", "西北")
            .Replace("东南部", "东南")
            .Replace("西南部", "西南")
            .Replace("东部", "东")
            .Replace("西部", "西")
            .Replace("南部", "南")
            .Replace("北部", "北")
            .Replace("中部", "中");
    }

    /// <summary>
    /// 命名去重：优先原名；重名时优先用方位词区分；再冲突才用数字后缀兜底。
    /// </summary>
    private string MakeUniqueName(string baseName, in GeoRegionGeneratedEvent evt)
    {
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "GeoRegion";
        }
        else
        {
            baseName = baseName.Trim();
        }

        if (_usedResolvedNames.Add(baseName))
        {
            IncreaseBaseCounter(baseName);
            return baseName;
        }

        foreach (var candidate in BuildDirectionalCandidates(baseName, evt))
        {
            if (!_usedResolvedNames.Add(candidate)) continue;
            IncreaseBaseCounter(baseName);
            return candidate;
        }

        var index = IncreaseBaseCounter(baseName);
        while (true)
        {
            var candidate = $"{baseName}{index}";
            if (_usedResolvedNames.Add(candidate))
            {
                return candidate;
            }
            index++;
        }
    }

    /// <summary>
    /// 生成“北铁山/铁山北/山东/山西”这类候选名，仅用于重名场景。
    /// </summary>
    private static IEnumerable<string> BuildDirectionalCandidates(string baseName, GeoRegionGeneratedEvent evt)
    {
        var directions = CollectDirections(evt);
        var emitted = new HashSet<string>();
        for (var i = 0; i < directions.Count; i++)
        {
            var dir = directions[i];
            if (string.IsNullOrEmpty(dir)) continue;

            var prefix = $"{dir}{baseName}";
            if (prefix != baseName && emitted.Add(prefix))
            {
                yield return prefix;
            }

            var suffix = $"{baseName}{dir}";
            if (suffix != baseName && emitted.Add(suffix))
            {
                yield return suffix;
            }
        }
    }

    /// <summary>
    /// 收集方位词，顺序从更强区分度到更弱区分度。
    /// </summary>
    private static List<string> CollectDirections(in GeoRegionGeneratedEvent evt)
    {
        var list = new List<string>(6);
        var width = evt.Width;
        var height = evt.Height;
        var x = evt.CenterX;
        var y = evt.CenterY;

        if (width <= 0 || height <= 0)
        {
            return list;
        }

        var x1 = width / 3;
        var x2 = width * 2 / 3;
        var y1 = height / 3;
        var y2 = height * 2 / 3;

        var ew = x < x1 ? "西" : x >= x2 ? "东" : string.Empty;
        var ns = y < y1 ? "南" : y >= y2 ? "北" : string.Empty;

        if (!string.IsNullOrEmpty(ew) && !string.IsNullOrEmpty(ns))
        {
            list.Add($"{ew}{ns}");
        }
        if (!string.IsNullOrEmpty(ns))
        {
            list.Add(ns);
        }
        if (!string.IsNullOrEmpty(ew))
        {
            list.Add(ew);
        }

        var axisX = x >= width / 2 ? "东" : "西";
        var axisY = y >= height / 2 ? "北" : "南";
        list.Add(axisY);
        list.Add(axisX);

        return list;
    }

    private int IncreaseBaseCounter(string baseName)
    {
        if (!_baseNameCounters.TryGetValue(baseName, out var count))
        {
            count = 1;
        }
        else
        {
            count++;
        }

        _baseNameCounters[baseName] = count;
        return count;
    }

    private static GeoRegionAsset ResolveCategory(GeoRegionLibrary lib, GeoRegionGeneratedEvent evt)
    {
        switch (evt.Layer)
        {
            case GeoRegionLayer.Primary:
            {
                if (evt.WaterKind != PrimaryWaterKind.None)
                {
                    return lib.ResolvePrimaryWater(evt.WaterKind);
                }

                if (evt.BaseLayerType == TileLayerType.Ocean)
                {
                    return lib.ResolvePrimaryWater(evt.TouchesEdge ? PrimaryWaterKind.Sea : PrimaryWaterKind.Lake);
                }

                if (evt.BaseLayerType is TileLayerType.Lava or TileLayerType.Goo or TileLayerType.Block)
                {
                    return lib.ResolvePrimarySpecial(evt.BaseLayerType);
                }

                if (!string.IsNullOrEmpty(evt.BiomeDominantCategoryId))
                {
                    var cat = lib.get(evt.BiomeDominantCategoryId);
                    if (cat != null) return cat;
                }

                return lib.PrimarySpecial;
            }
            case GeoRegionLayer.Landform:
            {
                if (!string.IsNullOrEmpty(evt.LandformDominantCategoryId))
                {
                    var cat = lib.get(evt.LandformDominantCategoryId);
                    if (cat != null) return cat;
                }
                return lib.LandformPlain;
            }
            case GeoRegionLayer.Landmass:
                return lib.ResolveLandmass(evt.TouchesEdge);
            case GeoRegionLayer.Peninsula:
                return lib.Peninsula;
            case GeoRegionLayer.Strait:
                return lib.Strait;
            case GeoRegionLayer.Archipelago:
                return lib.Archipelago;
            default:
                return null;
        }
    }
}
