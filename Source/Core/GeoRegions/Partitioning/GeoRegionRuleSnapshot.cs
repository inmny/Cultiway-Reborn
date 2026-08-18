using System;
using System.Collections.Generic;
using System.Globalization;

namespace Cultiway.Core.GeoRegions.Partitioning;

/// <summary>
/// 某次分区计算使用的全部规则副本。它只保存简单数据，不引用会在游戏运行中变化的资源对象。
/// </summary>
internal sealed class GeoRegionRuleSnapshot
{
    /// <summary>按最终地区类别编号保存规则，便于直接查找。</summary>
    private readonly GeoRegionCategoryRule[] categoryRules;
    /// <summary>按主要地表类别编号保存规则。</summary>
    private readonly GeoRegionCategoryRule[] primaryRules;
    /// <summary>按陆地外形类别编号保存规则。</summary>
    private readonly GeoRegionCategoryRule[] landformRulesByCode;
    /// <summary>按优先级从高到低排列的陆地外形规则。</summary>
    private readonly GeoRegionCategoryRule[] orderedLandformRules;
    /// <summary>记录每个常规生物群系应归入的主要地表类别。</summary>
    private readonly Dictionary<string, GeoRegionPrimaryCategoryCode> primaryCodeByBiomeId;
    /// <summary>创建快照时已知的全部生物群系标识，按文字顺序排列。</summary>
    private readonly string[] knownBiomeIds;
    /// <summary>把每个生物群系标识换成稳定整数，供格子数据记录身份。</summary>
    private readonly Dictionary<string, int> biomeIdentityCodeById;
    /// <summary>未归入常规地表类别的特殊生物群系标识。</summary>
    private readonly string[] specialBiomeIds;
    /// <summary>为每个特殊生物群系分配独立连接编号，防止不同群系连成同一区域。</summary>
    private readonly Dictionary<string, int> specialBiomeSignatureById;

    /// <summary>创建规则快照，检查类别是否齐全，并建立各类快速查找表。</summary>
    internal GeoRegionRuleSnapshot(
        int worldSeedId,
        int width,
        int height,
        int revision,
        IList<GeoRegionCategoryRule> rules,
        IList<string> biomeIds,
        GeoRegionPartitionParameters parameters)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
        if (rules == null) throw new ArgumentNullException(nameof(rules));
        if (biomeIds == null) throw new ArgumentNullException(nameof(biomeIds));

        WorldSeedId = worldSeedId;
        Width = width;
        Height = height;
        Revision = revision;
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));

        categoryRules = new GeoRegionCategoryRule[(int)GeoRegionCategoryCode.Archipelago + 1];
        primaryRules = new GeoRegionCategoryRule[GeoRegionPartitionCodec.PrimaryCodeCount];
        landformRulesByCode = new GeoRegionCategoryRule[GeoRegionPartitionCodec.LandformCodeCount];
        var landformList = new List<GeoRegionCategoryRule>(GeoRegionPartitionCodec.LandformCodeCount - 1);
        var ids = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < rules.Count; i++)
        {
            GeoRegionCategoryRule rule = rules[i] ??
                                         throw new InvalidOperationException($"GeoRegion 规则快照包含空规则: index={i}");
            int categoryIndex = (int)rule.CategoryCode;
            if ((uint)categoryIndex >= (uint)categoryRules.Length || categoryRules[categoryIndex] != null)
            {
                throw new InvalidOperationException($"GeoRegion 规则分类编码重复或越界: code={rule.CategoryCode}");
            }

            if (!ids.Add(rule.Id))
            {
                throw new InvalidOperationException($"GeoRegion 规则分类 id 重复: id={rule.Id}");
            }

            categoryRules[categoryIndex] = rule;
            if (rule.PrimaryCode != GeoRegionPrimaryCategoryCode.None)
            {
                int primaryIndex = (int)rule.PrimaryCode;
                if ((uint)primaryIndex >= (uint)primaryRules.Length || primaryRules[primaryIndex] != null)
                {
                    throw new InvalidOperationException($"GeoRegion Primary 编码重复或越界: code={rule.PrimaryCode}");
                }

                primaryRules[primaryIndex] = rule;
            }

            if (rule.LandformCode != GeoRegionLandformCode.None)
            {
                int landformIndex = (int)rule.LandformCode;
                if ((uint)landformIndex >= (uint)landformRulesByCode.Length || landformRulesByCode[landformIndex] != null)
                {
                    throw new InvalidOperationException($"GeoRegion Landform 编码重复或越界: code={rule.LandformCode}");
                }

                landformRulesByCode[landformIndex] = rule;
                landformList.Add(rule);
            }
        }

        for (int i = 1; i < categoryRules.Length; i++)
        {
            if (categoryRules[i] == null)
            {
                throw new InvalidOperationException($"GeoRegion 规则快照缺少统一分类: code={(GeoRegionCategoryCode)i}");
            }
        }

        for (int i = 1; i < primaryRules.Length; i++)
        {
            if (primaryRules[i] == null)
            {
                throw new InvalidOperationException($"GeoRegion 规则快照缺少 Primary 分类: code={(GeoRegionPrimaryCategoryCode)i}");
            }
        }

        for (int i = 1; i < landformRulesByCode.Length; i++)
        {
            if (landformRulesByCode[i] == null)
            {
                throw new InvalidOperationException($"GeoRegion 规则快照缺少 Landform 分类: code={(GeoRegionLandformCode)i}");
            }
        }

        landformList.Sort((left, right) =>
        {
            int priorityComparison = right.Priority.CompareTo(left.Priority);
            return priorityComparison != 0
                ? priorityComparison
                : left.CategoryCode.CompareTo(right.CategoryCode);
        });
        orderedLandformRules = landformList.ToArray();
        primaryCodeByBiomeId = BuildPrimaryBiomeMap();
        knownBiomeIds = NormalizeBiomeIds(biomeIds);
        biomeIdentityCodeById = BuildBiomeIdentityCodeMap(knownBiomeIds);
        specialBiomeIds = BuildSpecialBiomeIds(knownBiomeIds);
        specialBiomeSignatureById = BuildSpecialBiomeSignatureMap(specialBiomeIds);
        RuleFingerprint = CalculateRuleFingerprint();
    }

    /// <summary>把所有会影响分区结果的规则值算成一个编号，用于判断两份规则是否完全一致。</summary>
    private ulong CalculateRuleFingerprint()
    {
        ulong hash = 14695981039346656037UL;
        for (int i = 1; i < categoryRules.Length; i++)
        {
            GeoRegionCategoryRule rule = categoryRules[i];
            AddFingerprintString(ref hash, rule.Id);
            AddFingerprintInt(ref hash, (int)rule.Layer);
            AddFingerprintInt(ref hash, (int)rule.CategoryCode);
            AddFingerprintInt(ref hash, (int)rule.PrimaryCode);
            AddFingerprintInt(ref hash, (int)rule.LandformCode);
            AddFingerprintInt(ref hash, rule.Priority);
            AddFingerprintInt(ref hash, rule.MinTiles);
            AddFingerprintInt(ref hash, rule.MaxTiles);
            AddFingerprintNullableBool(ref hash, rule.RequireOceanMaterial);
            AddFingerprintNullableBool(ref hash, rule.RequireFillablePit);
            AddFingerprintNullableBool(ref hash, rule.RequireLava);
            AddFingerprintNullableBool(ref hash, rule.RequireGoo);
            AddFingerprintNullableBool(ref hash, rule.RequireMountain);
            AddFingerprintInt(ref hash, rule.MinNeighborWater);
            AddFingerprintInt(ref hash, rule.MaxDistanceToWater);
            AddFingerprintInt(ref hash, rule.MinNeighborBlock);
            AddFingerprintInt(ref hash, rule.MinNeighborPit);
            AddFingerprintInt(ref hash, rule.RequireOppositeBlockPair ? 1 : 0);
            AddFingerprintInt(ref hash, rule.MaxThickness);
            AddFingerprintString(ref hash, rule.MinCoastRatio.ToString("R", CultureInfo.InvariantCulture));
            AddFingerprintString(ref hash, rule.MaxNeckRatio.ToString("R", CultureInfo.InvariantCulture));
            AddFingerprintInt(ref hash, rule.MaxHalfWidth);
            AddFingerprintInt(ref hash, rule.MinExits);
            AddFingerprintString(ref hash, rule.MinAspectRatio.ToString("R", CultureInfo.InvariantCulture));
            AddFingerprintInt(ref hash, rule.IslandMaxTiles);
            AddFingerprintInt(ref hash, rule.MaxGap);
            AddFingerprintInt(ref hash, rule.MinIslands);
            AddFingerprintInt(ref hash, rule.MinTotalTiles);
            AddFingerprintStrings(ref hash, rule.CopyBiomeIds());
            AddFingerprintStrings(ref hash, rule.CopyTileTypeIds());
            GeoRegionTerrainLayer[] rawLayerTypes = rule.CopyLayerTypes();
            string[] layerTypes = new string[rawLayerTypes.Length];
            for (int layerIndex = 0; layerIndex < rawLayerTypes.Length; layerIndex++)
            {
                layerTypes[layerIndex] = ((int)rawLayerTypes[layerIndex]).ToString(CultureInfo.InvariantCulture);
            }
            AddFingerprintStrings(ref hash, layerTypes);
        }

        AddFingerprintString(ref hash, Parameters.LargeWaterSqrtScale.ToString("R", CultureInfo.InvariantCulture));
        AddFingerprintString(ref hash, Parameters.ClosedWaterSqrtScale.ToString("R", CultureInfo.InvariantCulture));
        AddFingerprintStrings(ref hash, (string[])knownBiomeIds.Clone());
        AddFingerprintInt(ref hash, Parameters.LargeWaterSplitDivisor);
        AddFingerprintInt(ref hash, Parameters.WaterSplitJitterRadius);
        AddFingerprintInt(ref hash, Parameters.LargeWaterForcedSplitMultiplier);
        AddFingerprintInt(ref hash, Parameters.ClosedWaterDirectFloor);
        AddFingerprintInt(ref hash, Parameters.ClosedWaterDirectLakeMultiplier);
        return hash;
    }

    /// <summary>把一个整数按固定字节顺序加入规则编号。</summary>
    private static void AddFingerprintInt(ref ulong hash, int value)
    {
        unchecked
        {
            uint encoded = (uint)value;
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(encoded >> shift);
                hash *= 1099511628211UL;
            }
        }
    }

    /// <summary>把“不限、必须否、必须是”三种布尔条件加入规则编号。</summary>
    private static void AddFingerprintNullableBool(ref ulong hash, bool? value)
    {
        AddFingerprintInt(ref hash, value.HasValue ? value.Value ? 2 : 1 : 0);
    }

    /// <summary>把字符串内容和长度加入规则编号。</summary>
    private static void AddFingerprintString(ref ulong hash, string value)
    {
        string text = value ?? string.Empty;
        for (int i = 0; i < text.Length; i++)
        {
            unchecked
            {
                hash ^= text[i];
                hash *= 1099511628211UL;
            }
        }
        AddFingerprintInt(ref hash, text.Length);
    }

    /// <summary>排序后把整组字符串加入规则编号，使原列表顺序不影响结果。</summary>
    private static void AddFingerprintStrings(ref ulong hash, string[] values)
    {
        if (values == null || values.Length == 0)
        {
            AddFingerprintInt(ref hash, 0);
            return;
        }
        Array.Sort(values, StringComparer.Ordinal);
        AddFingerprintInt(ref hash, values.Length);
        for (int i = 0; i < values.Length; i++) AddFingerprintString(ref hash, values[i]);
    }

    /// <summary>沿用相同规则，为另一个世界标识、地图尺寸或数据版本创建快照。</summary>
    internal GeoRegionRuleSnapshot CopyForIdentity(
        int worldSeedId,
        int width,
        int height,
        int revision)
    {
        var rules = new List<GeoRegionCategoryRule>(categoryRules.Length - 1);
        for (int i = 1; i < categoryRules.Length; i++) rules.Add(categoryRules[i]);
        return new GeoRegionRuleSnapshot(
            worldSeedId,
            width,
            height,
            revision,
            rules,
            knownBiomeIds,
            Parameters);
    }

    /// <summary>这份规则所属世界的种子标识。</summary>
    internal int WorldSeedId { get; }
    /// <summary>规则对应地图的横向格子数。</summary>
    internal int Width { get; }
    /// <summary>规则对应地图的纵向格子数。</summary>
    internal int Height { get; }
    /// <summary>规则对应的数据版本。</summary>
    internal int Revision { get; }
    /// <summary>由全部规则内容算出的编号；内容相同则编号相同。</summary>
    internal ulong RuleFingerprint { get; }
    /// <summary>本次计算使用的大片水域拆分参数。</summary>
    internal GeoRegionPartitionParameters Parameters { get; }

    /// <summary>海洋主要地表规则。</summary>
    internal GeoRegionCategoryRule PrimarySea => GetCategoryRule(GeoRegionCategoryCode.PrimarySea);
    /// <summary>湖泊主要地表规则。</summary>
    internal GeoRegionCategoryRule PrimaryLake => GetCategoryRule(GeoRegionCategoryCode.PrimaryLake);
    /// <summary>河流主要地表规则。</summary>
    internal GeoRegionCategoryRule PrimaryRiver => GetCategoryRule(GeoRegionCategoryCode.PrimaryRiver);
    /// <summary>熔岩主要地表规则。</summary>
    internal GeoRegionCategoryRule PrimaryLava => GetCategoryRule(GeoRegionCategoryCode.PrimaryLava);
    /// <summary>黏液主要地表规则。</summary>
    internal GeoRegionCategoryRule PrimaryGoo => GetCategoryRule(GeoRegionCategoryCode.PrimaryGoo);
    /// <summary>山地主要地表规则。</summary>
    internal GeoRegionCategoryRule PrimaryMountains => GetCategoryRule(GeoRegionCategoryCode.PrimaryMountains);
    /// <summary>海滩主要地表规则。</summary>
    internal GeoRegionCategoryRule PrimaryBeach => GetCategoryRule(GeoRegionCategoryCode.PrimaryBeach);
    /// <summary>特殊生物群系的主要地表规则。</summary>
    internal GeoRegionCategoryRule PrimarySpecial => GetCategoryRule(GeoRegionCategoryCode.PrimarySpecial);
    /// <summary>平原外形规则。</summary>
    internal GeoRegionCategoryRule LandformPlain => GetCategoryRule(GeoRegionCategoryCode.LandformPlain);
    /// <summary>山地外形规则。</summary>
    internal GeoRegionCategoryRule LandformMountain => GetCategoryRule(GeoRegionCategoryCode.LandformMountain);
    /// <summary>峡谷外形规则。</summary>
    internal GeoRegionCategoryRule LandformCanyon => GetCategoryRule(GeoRegionCategoryCode.LandformCanyon);
    /// <summary>盆地外形规则。</summary>
    internal GeoRegionCategoryRule LandformBasin => GetCategoryRule(GeoRegionCategoryCode.LandformBasin);
    /// <summary>岛屿大小规则。</summary>
    internal GeoRegionCategoryRule LandmassIsland => GetCategoryRule(GeoRegionCategoryCode.LandmassIsland);
    /// <summary>大陆大小规则。</summary>
    internal GeoRegionCategoryRule LandmassContinent => GetCategoryRule(GeoRegionCategoryCode.LandmassContinent);
    /// <summary>主大陆大小规则。</summary>
    internal GeoRegionCategoryRule LandmassMainland => GetCategoryRule(GeoRegionCategoryCode.LandmassMainland);
    /// <summary>半岛形状规则。</summary>
    internal GeoRegionCategoryRule Peninsula => GetCategoryRule(GeoRegionCategoryCode.Peninsula);
    /// <summary>海峡形状规则。</summary>
    internal GeoRegionCategoryRule Strait => GetCategoryRule(GeoRegionCategoryCode.Strait);
    /// <summary>群岛组成规则。</summary>
    internal GeoRegionCategoryRule Archipelago => GetCategoryRule(GeoRegionCategoryCode.Archipelago);

    /// <summary>根据生物群系标识找到常规主要地表类别；未登记的归入特殊类别。</summary>
    internal GeoRegionPrimaryCategoryCode ResolvePrimaryBiomeCode(string biomeId)
    {
        return !string.IsNullOrEmpty(biomeId) && primaryCodeByBiomeId.TryGetValue(biomeId, out GeoRegionPrimaryCategoryCode code)
            ? code
            : GeoRegionPrimaryCategoryCode.Special;
    }

    /// <summary>取得生物群系的稳定整数编号；空标识返回零，未知标识会报错。</summary>
    internal int ResolveBiomeIdentityCode(string biomeId)
    {
        if (string.IsNullOrEmpty(biomeId)) return 0;
        if (biomeIdentityCodeById.TryGetValue(biomeId, out int code)) return code;
        throw new InvalidOperationException(
            $"GeoRegion terrain 引用了规则快照之外的 biome: id={biomeId}");
    }

    /// <summary>取得陆地格的连接编号；不同特殊生物群系会得到不同编号。</summary>
    internal int ResolvePrimaryGroundSignature(
        GeoRegionPrimaryCategoryCode primaryCode,
        string biomeId)
    {
        if (primaryCode != GeoRegionPrimaryCategoryCode.Special || string.IsNullOrEmpty(biomeId))
        {
            return GeoRegionPartitionCodec.EncodeGroundSignature(primaryCode);
        }

        if (specialBiomeSignatureById.TryGetValue(biomeId, out int signature)) return signature;
        throw new InvalidOperationException(
            $"GeoRegion Special biome 未进入规则快照签名表: id={biomeId}");
    }

    /// <summary>判断连接编号是否属于某个已知的特殊生物群系。</summary>
    internal bool IsPrimarySpecialBiomeSignature(int signature)
    {
        int index = signature - GeoRegionPartitionCodec.PrimarySpecialBiomeSignatureOffset;
        return (uint)index < (uint)specialBiomeIds.Length;
    }

    /// <summary>从特殊连接编号还原生物群系标识；无效编号返回空字符串。</summary>
    internal string ResolvePrimarySpecialBiomeId(int signature)
    {
        int index = signature - GeoRegionPartitionCodec.PrimarySpecialBiomeSignatureOffset;
        return (uint)index < (uint)specialBiomeIds.Length ? specialBiomeIds[index] : string.Empty;
    }

    /// <summary>根据某格及周围情况选择主要陆地规则，符合海滩条件时优先使用海滩。</summary>
    internal GeoRegionCategoryRule ResolvePrimaryLand(in GeoRegionTerrainRuleContext context)
    {
        GeoRegionCategoryRule beach = PrimaryBeach;
        if (beach != null && MatchPrimaryBeachRule(beach, context))
        {
            return beach;
        }

        return GetPrimaryRule(context.Cell.PrimaryBiomeCode);
    }

    /// <summary>按优先级寻找该格符合的陆地外形规则；都不符合时使用平原。</summary>
    internal GeoRegionCategoryRule ResolveLandform(in GeoRegionTerrainRuleContext context)
    {
        for (int i = 0; i < orderedLandformRules.Length; i++)
        {
            GeoRegionCategoryRule rule = orderedLandformRules[i];
            if (MatchLandformRule(rule, context)) return rule;
        }

        return LandformPlain;
    }

    /// <summary>根据相连陆地的格子数量判断它是岛屿、大陆还是主大陆。</summary>
    internal GeoRegionCategoryRule ResolveLandmass(int tileCount)
    {
        if (tileCount >= LandmassMainland.MinTiles) return LandmassMainland;
        if (tileCount >= LandmassContinent.MinTiles) return LandmassContinent;
        return LandmassIsland;
    }

    /// <summary>按最终地区类别编号取规则；编号无效时返回空。</summary>
    internal GeoRegionCategoryRule GetCategoryRule(GeoRegionCategoryCode code)
    {
        int index = (int)code;
        return (uint)index < (uint)categoryRules.Length ? categoryRules[index] : null;
    }

    /// <summary>按主要地表类别取规则；编号无效或缺失时使用特殊类别规则。</summary>
    internal GeoRegionCategoryRule GetPrimaryRule(GeoRegionPrimaryCategoryCode code)
    {
        int index = (int)code;
        return (uint)index < (uint)primaryRules.Length
            ? primaryRules[index] ?? PrimarySpecial
            : PrimarySpecial;
    }

    /// <summary>按陆地外形类别取规则；编号无效或缺失时使用平原规则。</summary>
    internal GeoRegionCategoryRule GetLandformRule(GeoRegionLandformCode code)
    {
        int index = (int)code;
        return (uint)index < (uint)landformRulesByCode.Length
            ? landformRulesByCode[index] ?? LandformPlain
            : LandformPlain;
    }

    /// <summary>汇总各常规主要地表规则中的生物群系，建立标识到类别的查找表。</summary>
    private Dictionary<string, GeoRegionPrimaryCategoryCode> BuildPrimaryBiomeMap()
    {
        var result = new Dictionary<string, GeoRegionPrimaryCategoryCode>(StringComparer.Ordinal);
        AddBiomeRules(result, GeoRegionPrimaryCategoryCode.Grassland);
        AddBiomeRules(result, GeoRegionPrimaryCategoryCode.Forest);
        AddBiomeRules(result, GeoRegionPrimaryCategoryCode.Jungle);
        AddBiomeRules(result, GeoRegionPrimaryCategoryCode.Swamp);
        AddBiomeRules(result, GeoRegionPrimaryCategoryCode.Desert);
        AddBiomeRules(result, GeoRegionPrimaryCategoryCode.Tundra);
        AddBiomeRules(result, GeoRegionPrimaryCategoryCode.Highlands);
        AddBiomeRules(result, GeoRegionPrimaryCategoryCode.Wasteland);
        return result;
    }

    /// <summary>去掉空白和重复的生物群系标识，并按文字顺序排列。</summary>
    private static string[] NormalizeBiomeIds(IList<string> biomeIds)
    {
        var unique = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < biomeIds.Count; i++)
        {
            string biomeId = biomeIds[i];
            if (!string.IsNullOrWhiteSpace(biomeId)) unique.Add(biomeId.Trim());
        }
        var result = new List<string>(unique);
        result.Sort(StringComparer.Ordinal);
        return result.ToArray();
    }

    /// <summary>找出没有归入任何常规主要地表类别的生物群系。</summary>
    private string[] BuildSpecialBiomeIds(string[] biomeIds)
    {
        var result = new List<string>();
        for (int i = 0; i < biomeIds.Length; i++)
        {
            string biomeId = biomeIds[i];
            if (ResolvePrimaryBiomeCode(biomeId) == GeoRegionPrimaryCategoryCode.Special)
            {
                result.Add(biomeId);
            }
        }
        return result.ToArray();
    }

    /// <summary>按排序位置给每个生物群系分配从一开始的稳定整数编号。</summary>
    private static Dictionary<string, int> BuildBiomeIdentityCodeMap(string[] biomeIds)
    {
        var result = new Dictionary<string, int>(biomeIds.Length, StringComparer.Ordinal);
        for (int i = 0; i < biomeIds.Length; i++) result.Add(biomeIds[i], checked(i + 1));
        return result;
    }

    /// <summary>为每个特殊生物群系分配不会与普通陆地类别冲突的连接编号。</summary>
    private static Dictionary<string, int> BuildSpecialBiomeSignatureMap(string[] biomeIds)
    {
        var result = new Dictionary<string, int>(biomeIds.Length, StringComparer.Ordinal);
        for (int i = 0; i < biomeIds.Length; i++)
        {
            result.Add(
                biomeIds[i],
                checked(GeoRegionPartitionCodec.PrimarySpecialBiomeSignatureOffset + i));
        }
        return result;
    }

    /// <summary>把一个主要地表规则列出的生物群系加入类别查找表。</summary>
    private void AddBiomeRules(
        Dictionary<string, GeoRegionPrimaryCategoryCode> target,
        GeoRegionPrimaryCategoryCode code)
    {
        string[] biomeIds = GetPrimaryRule(code).CopyBiomeIds();
        for (int i = 0; i < biomeIds.Length; i++)
        {
            string biomeId = biomeIds[i];
            if (!string.IsNullOrEmpty(biomeId)) target[biomeId] = code;
        }
    }

    /// <summary>检查某格的材质、原始层、生物群系和周围情况是否符合陆地外形规则。</summary>
    private static bool MatchLandformRule(
        GeoRegionCategoryRule rule,
        in GeoRegionTerrainRuleContext context)
    {
        GeoRegionTerrainCell cell = context.Cell;
        return rule.MatchesTileType(cell.TileTypeId) &&
               rule.MatchesLayer(cell.Layer) &&
               rule.MatchesBiome(cell.BiomeId) &&
               MatchCommonFlags(rule, context, true);
    }

    /// <summary>检查某格是否满足海滩材质、离水距离和邻水数量等要求。</summary>
    private static bool MatchPrimaryBeachRule(
        GeoRegionCategoryRule rule,
        in GeoRegionTerrainRuleContext context)
    {
        GeoRegionTerrainCell cell = context.Cell;
        if (!rule.MatchesLayer(cell.Layer)) return false;

        if (rule.HasBiomeRestriction || rule.HasTileTypeRestriction)
        {
            bool matchesMaterial =
                (rule.HasBiomeRestriction && rule.MatchesBiome(cell.BiomeId)) ||
                (rule.HasTileTypeRestriction && rule.MatchesTileType(cell.TileTypeId));
            if (!matchesMaterial || !cell.IsBeachMaterial) return false;
        }

        if (!MatchCommonFlags(rule, context, false)) return false;
        if (rule.MaxDistanceToWater >= 0 &&
            (context.DistanceToWater < 0 || context.DistanceToWater > rule.MaxDistanceToWater))
        {
            return false;
        }

        if (rule.MinNeighborWater > 0 && context.NeighborWaterCount < rule.MinNeighborWater)
        {
            int diagonalWater = Math.Max(0, context.NeighborWater8Count - context.NeighborWaterCount);
            int compensatedWater = context.NeighborWaterCount + (diagonalWater >= 2 ? 1 : 0);
            if (compensatedWater < rule.MinNeighborWater) return false;
        }

        return true;
    }

    /// <summary>检查海洋、坑、熔岩、黏液、山体及周围格子等通用条件。</summary>
    private static bool MatchCommonFlags(
        GeoRegionCategoryRule rule,
        in GeoRegionTerrainRuleContext context,
        bool includeWaterRequirement)
    {
        GeoRegionTerrainCell cell = context.Cell;
        if (rule.RequireOceanMaterial.HasValue && cell.IsOceanMaterial != rule.RequireOceanMaterial.Value) return false;
        if (rule.RequireFillablePit.HasValue && cell.IsFillablePit != rule.RequireFillablePit.Value) return false;
        if (rule.RequireLava.HasValue && cell.IsLava != rule.RequireLava.Value) return false;
        if (rule.RequireGoo.HasValue && cell.IsGoo != rule.RequireGoo.Value) return false;
        if (rule.RequireMountain.HasValue && cell.IsMountain != rule.RequireMountain.Value) return false;
        if (includeWaterRequirement && rule.MinNeighborWater > 0 && context.NeighborWaterCount < rule.MinNeighborWater) return false;
        if (rule.MinNeighborBlock > 0 && context.NeighborBlockCount < rule.MinNeighborBlock) return false;
        if (rule.MinNeighborPit > 0 && context.NeighborPitCount < rule.MinNeighborPit) return false;
        return !rule.RequireOppositeBlockPair || context.HasOppositeBlockPair;
    }
}
