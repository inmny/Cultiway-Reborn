using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core;

namespace Cultiway.Content.SpiritVeins;

/// <summary>按地区、地势、元素与显化为本次世界运行中的风水龙脉生成稳定名称。</summary>
internal sealed class SpiritVeinNameService
{
    private static readonly string[] VeinModifiers = { "苍梧", "玄川", "云岫", "青屏", "赤霞", "寒岳", "归藏", "落星" };
    private static readonly string[] BranchModifiers = { "听松", "回云", "照水", "藏风", "镇岳", "抱月", "青崖", "寒潭" };
    private static readonly string[] GroundModifiers = { "落霞", "藏风", "抱月", "回澜", "栖云", "镇川", "会元", "归潮" };
    private static readonly string[] CrossingModifiers = { "双龙抱珠", "会元", "交泰", "双阙", "合真" };
    private readonly HashSet<string> usedNames = new();
    private readonly int worldSeedId;
    private readonly int width;
    private readonly int height;

    internal SpiritVeinNameService(int worldSeedId, int width, int height)
    {
        this.worldSeedId = worldSeedId;
        this.width = width;
        this.height = height;
    }

    internal void AssignNames(
        SpiritVeinGenerationResult result,
        SpiritVeinTerrainSnapshot terrain)
    {
        ReserveExistingNames(result);
        for (int i = 0; i < result.Veins.Count; i++)
        {
            SpiritVeinDraft vein = result.Veins[i];
            vein.SourceRegionName = PickRegionName(terrain[vein.SourceCenterTileId]);
            vein.OutletRegionName = PickRegionName(terrain[vein.OutletTileId]);
            if (!string.IsNullOrWhiteSpace(vein.Name)) continue;
            string place = TrimRegionSuffix(vein.SourceRegionName);
            if (string.IsNullOrEmpty(place)) place = VeinModifiers[StableIndex(vein.Id, VeinModifiers.Length)];
            string element = ResolveDominantElementWord(vein.Composition);
            string suffix = vein.Scale == DragonVeinScale.Ancestral
                ? "祖脉"
                : vein.Scale == DragonVeinScale.Large && StableIndex(vein.Id + 19, 3) == 0
                    ? "龙脉"
                    : string.IsNullOrEmpty(element) || place.EndsWith(element, StringComparison.Ordinal)
                        ? "地脉"
                        : element + "脉";
            vein.Name = ResolveUnique(place + suffix, vein.SourceCenterTileId);
        }

        for (int i = 0; i < result.Branches.Count; i++)
        {
            SpiritVeinBranch branch = result.Branches[i];
            if (!string.IsNullOrWhiteSpace(branch.Name)) continue;
            string place = TrimRegionSuffix(PickRegionName(terrain[branch.SourceCenterTileId]));
            if (string.IsNullOrEmpty(place))
                place = BranchModifiers[StableIndex(branch.Id + branch.VeinId * 31, BranchModifiers.Length)];
            branch.Name = ResolveUnique(place + "支龙", branch.SourceCenterTileId);
        }

        for (int i = 0; i < result.Grounds.Count; i++)
        {
            GatheringGround ground = result.Grounds[i];
            ground.RegionName = PickRegionName(terrain[ground.CenterTileId]);
            if (!string.IsNullOrWhiteSpace(ground.Name)) continue;
            string place = TrimRegionSuffix(ground.RegionName);
            if (ground.Kind == GatheringGroundKind.Crossing)
            {
                place = CrossingModifiers[StableIndex(ground.Id + 71, CrossingModifiers.Length)];
            }
            else if (string.IsNullOrEmpty(place))
            {
                place = GroundModifiers[StableIndex(ground.Id + ground.PrimaryVeinId * 43, GroundModifiers.Length)];
            }

            string suffix = ground.Kind switch
            {
                GatheringGroundKind.Main => "明堂",
                GatheringGroundKind.Crossing => "地",
                GatheringGroundKind.Remnant => "残穴",
                _ => ground.Quality >= GatheringGroundQuality.Upper ? "福地" : "灵地"
            };
            ground.Name = ResolveUnique(place + suffix, ground.CenterTileId);
        }

        for (int i = 0; i < result.Eyes.Count; i++)
        {
            SpiritVeinEye eye = result.Eyes[i];
            if (!string.IsNullOrWhiteSpace(eye.Name)) continue;
            GatheringGround ground = FindGround(result.Grounds, eye.GroundId);
            string place = ground == null ? string.Empty : TrimGroundSuffix(ground.Name);
            if (string.IsNullOrEmpty(place))
                place = GroundModifiers[StableIndex(eye.Id + eye.VeinId * 59, GroundModifiers.Length)];
            eye.Name = ResolveUnique(place + ResolveManifestationSuffix(eye.Manifestation), eye.TileId);
        }
    }

    internal static string ResolveDominantElementWord(ElementComposition composition)
    {
        int bestIndex = 0;
        float bestValue = composition[0];
        for (int i = 1; i < ElementIndex.Count; i++)
        {
            if (composition[i] <= bestValue) continue;
            bestIndex = i;
            bestValue = composition[i];
        }
        return bestIndex switch
        {
            ElementIndex.Iron => "金",
            ElementIndex.Wood => "木",
            ElementIndex.Water => "水",
            ElementIndex.Fire => "火",
            ElementIndex.Earth => "土",
            ElementIndex.Neg => "阴",
            ElementIndex.Pos => "阳",
            ElementIndex.Entropy => "混沌",
            _ => string.Empty
        };
    }

    private void ReserveExistingNames(SpiritVeinGenerationResult result)
    {
        for (int i = 0; i < result.Veins.Count; i++) Reserve(result.Veins[i].Name);
        for (int i = 0; i < result.Branches.Count; i++) Reserve(result.Branches[i].Name);
        for (int i = 0; i < result.Grounds.Count; i++) Reserve(result.Grounds[i].Name);
        for (int i = 0; i < result.Eyes.Count; i++) Reserve(result.Eyes[i].Name);
    }

    private void Reserve(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) usedNames.Add(value.Trim());
    }

    private string ResolveUnique(string baseName, int tileId)
    {
        string name = string.IsNullOrWhiteSpace(baseName) ? "无名灵地" : baseName.Trim();
        if (usedNames.Add(name)) return name;
        int x = tileId % width;
        int y = tileId / width;
        string horizontal = x < width / 3 ? "西" : x >= width * 2 / 3 ? "东" : string.Empty;
        string vertical = y < height / 3 ? "南" : y >= height * 2 / 3 ? "北" : string.Empty;
        string direction = horizontal + vertical;
        if (!string.IsNullOrEmpty(direction) && usedNames.Add(direction + name)) return direction + name;
        if (!string.IsNullOrEmpty(horizontal) && usedNames.Add(horizontal + name)) return horizontal + name;
        if (!string.IsNullOrEmpty(vertical) && usedNames.Add(vertical + name)) return vertical + name;
        int index = 2;
        while (!usedNames.Add(name + index)) index++;
        return name + index;
    }

    private int StableIndex(int value, int count)
    {
        unchecked
        {
            uint mixed = (uint)worldSeedId * 1664525u + (uint)value * 1013904223u;
            mixed ^= mixed >> 16;
            return count <= 0 ? 0 : (int)(mixed % (uint)count);
        }
    }

    private static string PickRegionName(SpiritVeinTerrainCell cell)
    {
        return !string.IsNullOrWhiteSpace(cell.LandformRegionName)
            ? cell.LandformRegionName
            : cell.PrimaryRegionName;
    }

    private static string ResolveManifestationSuffix(SpiritEyeManifestation manifestation)
    {
        return manifestation switch
        {
            SpiritEyeManifestation.SpiritSpring => "灵泉",
            SpiritEyeManifestation.EarthBreath => "地窍",
            SpiritEyeManifestation.StoneMarrow => "石髓",
            SpiritEyeManifestation.WoodBloom => "木华",
            SpiritEyeManifestation.WindEye => "风眼",
            SpiritEyeManifestation.FireCave => "火穴",
            SpiritEyeManifestation.YinPool => "阴潭",
            SpiritEyeManifestation.YangPool => "阳池",
            SpiritEyeManifestation.ChaosBreath => "混沌地窍",
            _ => "灵眼"
        };
    }

    private static GatheringGround FindGround(List<GatheringGround> grounds, int id)
    {
        for (int i = 0; i < grounds.Count; i++)
        {
            if (grounds[i].Id == id) return grounds[i];
        }
        return null;
    }

    private static string TrimGroundSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string[] suffixes = { "明堂", "福地", "灵地", "残穴", "地" };
        for (int i = 0; i < suffixes.Length; i++)
        {
            if (value.EndsWith(suffixes[i], StringComparison.Ordinal) && value.Length > suffixes[i].Length)
                return value.Substring(0, value.Length - suffixes[i].Length);
        }
        return value;
    }

    private static string TrimRegionSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string result = value.Trim();
        string[] suffixes =
        {
            "山脉", "群峰", "高原", "高地", "平原", "盆地", "谷地", "峡谷", "海域", "海", "湖", "河", "川", "岭", "原", "林", "地"
        };
        for (int i = 0; i < suffixes.Length; i++)
        {
            if (!result.EndsWith(suffixes[i], StringComparison.Ordinal) || result.Length <= suffixes[i].Length) continue;
            return result.Substring(0, result.Length - suffixes[i].Length);
        }
        return result;
    }
}
