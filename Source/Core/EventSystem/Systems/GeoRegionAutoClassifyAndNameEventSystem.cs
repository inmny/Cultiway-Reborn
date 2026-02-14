using System.Collections.Generic;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Libraries;

namespace Cultiway.Core.EventSystem.Systems;

/// <summary>
/// GeoRegion 生成后自动分类与命名（确定性 + 可调参模板）。
/// </summary>
public class GeoRegionAutoClassifyAndNameEventSystem : GenericEventSystem<GeoRegionGeneratedEvent>
{
    protected override int MaxEventsPerUpdate => 4096;

    private int _currentSeedId;
    private readonly Dictionary<string, int> _usedNameCounts = new();

    protected override void HandleEvent(GeoRegionGeneratedEvent evt)
    {
        if (evt.RegionId == 0) return;

        if (evt.WorldSeedId != _currentSeedId)
        {
            _currentSeedId = evt.WorldSeedId;
            _usedNameCounts.Clear();
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

        var dir = GetDir(evt.CenterX, evt.CenterY, evt.Width, evt.Height);
        var biomeName = GetAssetDisplayName(lib, evt.BiomeDominantCategoryId);
        var landformName = GetAssetDisplayName(lib, evt.LandformDominantCategoryId);
        var typeName = category.DisplayName ?? string.Empty;

        var template = category.Naming?.Template;
        if (string.IsNullOrEmpty(template))
        {
            template = "{Dir}{Type}";
        }

        var baseName = template
            .Replace("{Dir}", dir)
            .Replace("{Biome}", biomeName)
            .Replace("{Landform}", landformName)
            .Replace("{Type}", typeName)
            .Trim();

        if (string.IsNullOrEmpty(baseName))
        {
            baseName = typeName;
        }

        var finalName = MakeUnique(baseName);
        region.data.name = finalName;
        region.data.custom_name = true;
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

    private static string GetAssetDisplayName(GeoRegionLibrary lib, string categoryId)
    {
        if (string.IsNullOrEmpty(categoryId)) return string.Empty;
        var asset = lib.get(categoryId);
        return asset?.DisplayName ?? string.Empty;
    }

    private static string GetDir(int centerX, int centerY, int width, int height)
    {
        if (width <= 0 || height <= 0) return string.Empty;

        var x1 = width / 3;
        var x2 = width * 2 / 3;
        var y1 = height / 3;
        var y2 = height * 2 / 3;

        var ew = centerX < x1 ? "西" : centerX >= x2 ? "东" : string.Empty;
        var ns = centerY < y1 ? "南" : centerY >= y2 ? "北" : string.Empty;

        if (string.IsNullOrEmpty(ew) && string.IsNullOrEmpty(ns)) return string.Empty;
        if (string.IsNullOrEmpty(ew)) return $"{ns}部";
        if (string.IsNullOrEmpty(ns)) return $"{ew}部";
        return $"{ew}{ns}部";
    }

    private string MakeUnique(string baseName)
    {
        if (string.IsNullOrEmpty(baseName))
        {
            baseName = "GeoRegion";
        }

        if (!_usedNameCounts.TryGetValue(baseName, out var count))
        {
            _usedNameCounts[baseName] = 1;
            return baseName;
        }

        count++;
        _usedNameCounts[baseName] = count;
        return $"{baseName}·{count}";
    }
}
