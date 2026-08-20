using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Content.SpiritVeins;

/// <summary>从统一平衡表读取风水龙脉的生成、运行和显示参数。</summary>
internal static class SpiritVeinSettings
{
    private const string RelativePath = "Content/SpiritVein.json";

    internal static int DragonVeinMinimum { get; private set; }
    internal static int DragonVeinMaximum { get; private set; }
    internal static int DragonVeinAbsoluteMaximum { get; private set; }
    internal static int MainMinimumLength { get; private set; }
    internal static int BranchMinimumLength { get; private set; }
    internal static int TargetSearchAttempts { get; private set; }
    internal static int SourceDomainRadius { get; private set; }
    internal static int FieldBaseRadius { get; private set; }
    internal static int FieldMaximumRadius { get; private set; }
    internal static float FieldMinimumStrength { get; private set; }
    internal static int SectionTargetLength { get; private set; }
    internal static int SecondaryGroundMaximum { get; private set; }
    internal static int GroundMinimumDistance { get; private set; }
    internal static int MainGroundRadius { get; private set; }
    internal static int SecondaryGroundRadius { get; private set; }
    internal static int CrossingGroundRadius { get; private set; }
    internal static int RerouteDelayYears { get; private set; }
    internal static int TerrainChangeRadius { get; private set; }
    internal static float MaximumCultivationBonus { get; private set; }
    internal static float BackgroundCleanWakan => WorldWakanService.DefaultCleanBackground;
    internal static float MonthlyPollutionSampleRatio { get; private set; }

    private static float dragonSmallCapacity;
    private static float dragonMediumCapacity;
    private static float dragonLargeCapacity;
    private static float dragonAncestralCapacity;
    private static float baseWakanMicro;
    private static float baseWakanSmall;
    private static float baseWakanMedium;
    private static float baseWakanLarge;
    private static float baseWakanAncestral;
    private static float recoveryMainRatio;
    private static float recoveryBranchRatio;
    private static float supplyMainRatio;
    private static float supplyBranchRatio;
    private static float transferRatio;
    private static float terrainHeightPlain;
    private static float terrainHeightHighland;
    private static float terrainHeightMountain;

    internal static void Load()
    {
        string path = Path.Combine(ModClass.I.GetDeclaration().FolderPath, RelativePath);
        if (!File.Exists(path)) throw new FileNotFoundException("缺少风水龙脉平衡数据", path);
        Dictionary<string, float> values =
            JsonConvert.DeserializeObject<Dictionary<string, float>>(File.ReadAllText(path)) ??
            throw new InvalidDataException("风水龙脉平衡数据为空");

        float cleanBackground = Require(values, "world_clean_background", 0f);
        float maximumWakan = Require(values, "world_wakan_maximum", cleanBackground, false);
        WorldWakanService.ConfigureBalance(cleanBackground, maximumWakan);

        DragonVeinMinimum = RequireInt(values, "dragon_vein_minimum", 1);
        DragonVeinMaximum = RequireInt(values, "dragon_vein_maximum", DragonVeinMinimum);
        DragonVeinAbsoluteMaximum = RequireInt(values, "dragon_vein_absolute_maximum", DragonVeinMaximum);
        MainMinimumLength = RequireInt(values, "main_minimum_length", 8);
        BranchMinimumLength = RequireInt(values, "branch_minimum_length", 4);
        TargetSearchAttempts = RequireInt(values, "target_search_attempts", 1);
        SourceDomainRadius = RequireInt(values, "source_domain_radius", 2);
        FieldBaseRadius = RequireInt(values, "field_base_radius", 2);
        FieldMaximumRadius = RequireInt(values, "field_max_radius", FieldBaseRadius);
        FieldMinimumStrength = RequireRange(values, "field_minimum_strength", 0.01f, 0.8f);
        SectionTargetLength = RequireInt(values, "section_target_length", 6);
        SecondaryGroundMaximum = RequireInt(values, "secondary_ground_maximum", 0);
        GroundMinimumDistance = RequireInt(values, "ground_minimum_distance", 4);
        MainGroundRadius = RequireInt(values, "main_ground_radius", 3);
        SecondaryGroundRadius = RequireInt(values, "secondary_ground_radius", 2);
        CrossingGroundRadius = RequireInt(values, "crossing_ground_radius", 2);
        RerouteDelayYears = RequireInt(values, "reroute_delay_years", 1);
        TerrainChangeRadius = RequireInt(values, "terrain_change_radius", 1);
        MaximumCultivationBonus = RequireRange(values, "maximum_cultivation_bonus", 0f, 1f);
        MonthlyPollutionSampleRatio = RequireRange(values, "monthly_pollution_sample_ratio", 0.001f, 1f);

        dragonSmallCapacity = Require(values, "dragon_scale_small_capacity", 0f, false);
        dragonMediumCapacity = Require(values, "dragon_scale_medium_capacity", dragonSmallCapacity, false);
        dragonLargeCapacity = Require(values, "dragon_scale_large_capacity", dragonMediumCapacity, false);
        dragonAncestralCapacity = Require(values, "dragon_scale_ancestral_capacity", dragonLargeCapacity, false);
        baseWakanMicro = Require(values, "base_wakan_micro", 0f, false);
        baseWakanSmall = Require(values, "base_wakan_small", 0f, false);
        baseWakanMedium = Require(values, "base_wakan_medium", 0f, false);
        baseWakanLarge = Require(values, "base_wakan_large", 0f, false);
        baseWakanAncestral = Require(values, "base_wakan_ancestral", 0f, false);
        recoveryMainRatio = RequireRange(values, "monthly_recovery_main_ratio", 0f, 1f, false);
        recoveryBranchRatio = RequireRange(values, "monthly_recovery_branch_ratio", 0f, 1f, false);
        supplyMainRatio = RequireRange(values, "monthly_supply_main_ratio", 0f, 1f, false);
        supplyBranchRatio = RequireRange(values, "monthly_supply_branch_ratio", 0f, 1f, false);
        transferRatio = RequireRange(values, "monthly_transfer_ratio", 0f, 1f, false);
        terrainHeightPlain = Require(values, "terrain_height_plain", 0f);
        terrainHeightHighland = Require(values, "terrain_height_highland", terrainHeightPlain, false);
        terrainHeightMountain = Require(values, "terrain_height_mountain", terrainHeightHighland, false);
    }

    internal static DragonVeinScale ResolveDragonScale(float totalCapacity)
    {
        if (totalCapacity >= dragonAncestralCapacity) return DragonVeinScale.Ancestral;
        if (totalCapacity >= dragonLargeCapacity) return DragonVeinScale.Large;
        if (totalCapacity >= dragonMediumCapacity) return DragonVeinScale.Medium;
        if (totalCapacity >= dragonSmallCapacity) return DragonVeinScale.Small;
        return DragonVeinScale.Micro;
    }

    internal static SpiritBranchScale ResolveBranchScale(float totalCapacity)
    {
        if (totalCapacity >= dragonLargeCapacity * 0.45f) return SpiritBranchScale.Large;
        if (totalCapacity >= dragonMediumCapacity * 0.4f) return SpiritBranchScale.Medium;
        if (totalCapacity >= dragonSmallCapacity * 0.4f) return SpiritBranchScale.Small;
        return SpiritBranchScale.Micro;
    }

    internal static GatheringGroundQuality ResolveGroundQuality(float score)
    {
        if (score >= 4.15f) return GatheringGroundQuality.Natural;
        if (score >= 3.45f) return GatheringGroundQuality.Supreme;
        if (score >= 2.75f) return GatheringGroundQuality.Upper;
        if (score >= 2.1f) return GatheringGroundQuality.Middle;
        return GatheringGroundQuality.Lower;
    }

    internal static float ResolveBaseWakan(DragonVeinScale scale)
    {
        return scale switch
        {
            DragonVeinScale.Ancestral => baseWakanAncestral,
            DragonVeinScale.Large => baseWakanLarge,
            DragonVeinScale.Medium => baseWakanMedium,
            DragonVeinScale.Small => baseWakanSmall,
            _ => baseWakanMicro
        };
    }

    internal static float ResolveMonthlyRecovery(bool branch, float capacity)
    {
        return capacity * (branch ? recoveryBranchRatio : recoveryMainRatio);
    }

    internal static float ResolveMonthlySupply(bool branch, float capacity)
    {
        return capacity * (branch ? supplyBranchRatio : supplyMainRatio);
    }

    internal static float ResolveMonthlyTransfer(float capacity)
    {
        return capacity * transferRatio;
    }

    internal static float ResolveManifestationSupplyMultiplier(SpiritEyeManifestation manifestation)
    {
        return manifestation switch
        {
            SpiritEyeManifestation.EarthBreath => 1.2f,
            SpiritEyeManifestation.WindEye => 1.08f,
            SpiritEyeManifestation.FireCave => 1.3f,
            SpiritEyeManifestation.ChaosBreath => 1.22f,
            _ => 1f
        };
    }

    internal static float ResolveManifestationRecoveryMultiplier(SpiritEyeManifestation manifestation)
    {
        return manifestation switch
        {
            SpiritEyeManifestation.StoneMarrow => 0.82f,
            SpiritEyeManifestation.FireCave => 1.25f,
            SpiritEyeManifestation.YangPool => 1.2f,
            SpiritEyeManifestation.ChaosBreath => 0.92f,
            _ => 1f
        };
    }

    internal static float ResolveManifestationCapacityMultiplier(SpiritEyeManifestation manifestation)
    {
        return manifestation switch
        {
            SpiritEyeManifestation.StoneMarrow => 1.35f,
            SpiritEyeManifestation.YinPool => 1.18f,
            SpiritEyeManifestation.WindEye => 0.82f,
            SpiritEyeManifestation.YangPool => 0.88f,
            _ => 1f
        };
    }

    internal static float ResolveTerrainHeight(bool mountain, bool highland, bool water)
    {
        if (water) return 0f;
        if (mountain) return terrainHeightMountain;
        if (highland) return terrainHeightHighland;
        return terrainHeightPlain;
    }

    private static float Require(
        IReadOnlyDictionary<string, float> values,
        string key,
        float minimum,
        bool inclusive = true)
    {
        if (!values.TryGetValue(key, out float value) || float.IsNaN(value) || float.IsInfinity(value) ||
            (inclusive ? value < minimum : value <= minimum))
        {
            throw new InvalidDataException($"风水龙脉平衡参数 {key} 无效");
        }
        return value;
    }

    private static int RequireInt(IReadOnlyDictionary<string, float> values, string key, int minimum)
    {
        float value = Require(values, key, minimum);
        int rounded = Mathf.RoundToInt(value);
        if (!Mathf.Approximately(value, rounded))
            throw new InvalidDataException($"风水龙脉平衡参数 {key} 必须是整数");
        return rounded;
    }

    private static float RequireRange(
        IReadOnlyDictionary<string, float> values,
        string key,
        float minimum,
        float maximum,
        bool inclusiveMinimum = true)
    {
        float value = Require(values, key, minimum, inclusiveMinimum);
        if (value > maximum) throw new InvalidDataException($"风水龙脉平衡参数 {key} 超出允许范围");
        return value;
    }
}
