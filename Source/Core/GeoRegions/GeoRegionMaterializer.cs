using System;
using Cultiway.Core.GeoRegions.Partitioning;
using Cultiway.Core.Libraries;
using NeoModLoader.General;

namespace Cultiway.Core.GeoRegions;

/// <summary>
/// 在主线程把地区划分的计算结果变成游戏里的地区对象。
/// 它负责查找分类配置、创建或更新对象以及命名，不参与地图如何划分的计算。
/// </summary>
internal sealed class GeoRegionMaterializer
{
    // 对象管理器和地区分类配置，分别负责创建游戏对象与解释计算结果中的分类编号。
    private readonly GeoRegionManager manager;
    private readonly GeoRegionLibrary library;

    // 本轮共用的命名状态：nameService 生成基础名，namingSession 保证各地区最终不重名。
    private readonly GeoRegionNamingSession namingSession;
    private readonly GeoRegionNameService nameService;

    // 地图尺寸用于根据地区中心位置生成重名时的方位词。
    private readonly int width;
    private readonly int height;

    /// <summary>
    /// 创建一次地区对象转换过程，并绑定当前世界的命名种子和地图尺寸。
    /// </summary>
    internal GeoRegionMaterializer(
        GeoRegionManager manager,
        GeoRegionLibrary library,
        GeoRegionNamingSession namingSession,
        int worldSeedId,
        int width,
        int height)
    {
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        this.library = library ?? throw new ArgumentNullException(nameof(library));
        this.namingSession = namingSession ?? throw new ArgumentNullException(nameof(namingSession));
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        nameService = new GeoRegionNameService(worldSeedId);
        this.width = width;
        this.height = height;
    }

    /// <summary>
    /// 根据一个地区计算结果创建游戏对象，并为它生成不重复的名称。
    /// </summary>
    internal GeoRegion Materialize(GeoRegionDescriptor descriptor)
    {
        return Materialize(descriptor, null);
    }

    /// <summary>
    /// 根据一个地区计算结果创建游戏对象；有保留的玩家自定义名称时优先沿用该名称。
    /// </summary>
    internal GeoRegion Materialize(GeoRegionDescriptor descriptor, string preservedCustomName)
    {
        if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
        GeoRegionAsset category = ResolveCategory(descriptor.CategoryCode);
        ValidateCategoryLayer(category, descriptor);

        GeoRegion region = manager.BuildGeoRegion();
        if (region?.data == null)
        {
            throw new InvalidOperationException("GeoRegion 物化后缺少数据对象");
        }

        ApplyDerivedFields(region, descriptor, category);
        if (!string.IsNullOrWhiteSpace(preservedCustomName))
        {
            region.data.name = preservedCustomName;
            region.data.custom_name = true;
        }
        else
        {
            string generatedName = nameService.Generate(
                descriptor,
                category.GetDisplayName(),
                ResolveBiomeDisplayName(descriptor));
            region.data.name = namingSession.ResolveUniqueName(generatedName, descriptor, width, height);
            region.data.custom_name = false;
        }
        return region;
    }

    /// <summary>
    /// 用新的计算结果更新一个继续使用的旧地区对象；只有自动名称的分类或生物群系变化时才重新命名。
    /// </summary>
    internal void UpdateExisting(GeoRegion region, GeoRegionDescriptor descriptor, GeoRegionAsset category)
    {
        if (region?.data == null) throw new InvalidOperationException("GeoRegion survivor 缺少数据对象");
        if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
        ValidateCategoryLayer(category, descriptor);

        bool categoryChanged = !string.Equals(region.data.CategoryId, category.id, StringComparison.Ordinal);
        string previousNamingBiomeId = region.data.Layer == GeoRegionLayer.Primary
            ? region.data.CoreBiomeId
            : region.data.DominantBiomeId;
        string nextNamingBiomeId = GeoRegionNameService.ResolveNamingBiomeId(descriptor);
        bool namingBiomeChanged = !string.Equals(
            previousNamingBiomeId,
            nextNamingBiomeId,
            StringComparison.Ordinal);
        ApplyDerivedFields(region, descriptor, category);
        if (!region.data.custom_name && (categoryChanged || namingBiomeChanged))
        {
            string generatedName = nameService.Generate(
                descriptor,
                category.GetDisplayName(),
                ResolveBiomeDisplayName(descriptor));
            region.data.name = namingSession.ResolveUniqueName(generatedName, descriptor, width, height);
        }
    }

    /// <summary>
    /// 把计算结果中的分类编号转换为游戏实际使用的地区分类配置。
    /// </summary>
    internal GeoRegionAsset ResolveCategory(GeoRegionCategoryCode code)
    {
        GeoRegionAsset asset = code switch
        {
            GeoRegionCategoryCode.PrimarySea => library.PrimarySea,
            GeoRegionCategoryCode.PrimaryLake => library.PrimaryLake,
            GeoRegionCategoryCode.PrimaryRiver => library.PrimaryRiver,
            GeoRegionCategoryCode.PrimaryLava => library.PrimaryLava,
            GeoRegionCategoryCode.PrimaryGoo => library.PrimaryGoo,
            GeoRegionCategoryCode.PrimaryMountains => library.PrimaryMountains,
            GeoRegionCategoryCode.PrimaryGrassland => library.PrimaryGrassland,
            GeoRegionCategoryCode.PrimaryForest => library.PrimaryForest,
            GeoRegionCategoryCode.PrimaryJungle => library.PrimaryJungle,
            GeoRegionCategoryCode.PrimarySwamp => library.PrimarySwamp,
            GeoRegionCategoryCode.PrimaryDesert => library.PrimaryDesert,
            GeoRegionCategoryCode.PrimaryBeach => library.PrimaryBeach,
            GeoRegionCategoryCode.PrimaryTundra => library.PrimaryTundra,
            GeoRegionCategoryCode.PrimaryHighlands => library.PrimaryHighlands,
            GeoRegionCategoryCode.PrimaryWasteland => library.PrimaryWasteland,
            GeoRegionCategoryCode.PrimarySpecial => library.PrimarySpecial,
            GeoRegionCategoryCode.LandformPlain => library.LandformPlain,
            GeoRegionCategoryCode.LandformMountain => library.LandformMountain,
            GeoRegionCategoryCode.LandformCanyon => library.LandformCanyon,
            GeoRegionCategoryCode.LandformBasin => library.LandformBasin,
            GeoRegionCategoryCode.LandmassIsland => library.LandmassIsland,
            GeoRegionCategoryCode.LandmassContinent => library.LandmassContinent,
            GeoRegionCategoryCode.LandmassMainland => library.LandmassMainland,
            GeoRegionCategoryCode.Peninsula => library.Peninsula,
            GeoRegionCategoryCode.Strait => library.Strait,
            GeoRegionCategoryCode.Archipelago => library.Archipelago,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "未知 GeoRegion 分类编码")
        };

        return asset ?? throw new InvalidOperationException($"GeoRegion 分类资产缺失: code={code}");
    }

    /// <summary>
    /// 确认分类配置和计算结果属于同一个地区层级。
    /// </summary>
    private static void ValidateCategoryLayer(GeoRegionAsset category, GeoRegionDescriptor descriptor)
    {
        if (category.Layer != descriptor.Layer)
        {
            throw new InvalidOperationException(
                $"GeoRegion descriptor 分类层级不一致: category={category.id}/{category.Layer}, descriptor={descriptor.Layer}");
        }
    }

    /// <summary>
    /// 查找命名所用生物群系的本地化显示名；找不到资产或译文时退回其编号。
    /// </summary>
    private static string ResolveBiomeDisplayName(GeoRegionDescriptor descriptor)
    {
        string biomeId = GeoRegionNameService.ResolveNamingBiomeId(descriptor);
        if (string.IsNullOrEmpty(biomeId)) return string.Empty;

        BiomeAsset biome = AssetManager.biome_library?.get(biomeId);
        if (biome == null) return biomeId;
        if (!string.IsNullOrEmpty(biome.localized_key) && LM.Has(biome.localized_key))
        {
            return LM.Get(biome.localized_key);
        }
        if (LM.Has(biome.id)) return LM.Get(biome.id);
        return biomeId;
    }

    /// <summary>
    /// 把层级、分类、中心、面积和生物群系组成等计算字段写入游戏地区对象。
    /// </summary>
    private static void ApplyDerivedFields(
        GeoRegion region,
        GeoRegionDescriptor descriptor,
        GeoRegionAsset category)
    {
        region.data.Layer = descriptor.Layer;
        region.data.CategoryId = category.id;
        region.data.CenterX = descriptor.CenterX;
        region.data.CenterY = descriptor.CenterY;
        region.data.TileCount = descriptor.TileCount;
        region.data.CoreTileCount = descriptor.CoreTileCount;
        region.data.IsMixed = descriptor.IsMixed;
        region.data.TopologyExempt = descriptor.TopologyExempt;
        region.data.DominantPrimaryCode = (int)descriptor.DominantPrimaryCode;
        region.data.DominantLandformCode = (int)descriptor.DominantLandformCode;
        region.data.CoreBiomeId = descriptor.CoreBiomeId;
        region.data.DominantBiomeId = descriptor.DominantBiomeId;
        region.data.BiomeCompositionCount = descriptor.BiomeCompositionCount;
        region.data.RawCompositionCount = descriptor.RawCompositionCount;
        region.data.Purity = descriptor.Purity;
    }
}
