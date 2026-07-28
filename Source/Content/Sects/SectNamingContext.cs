using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Core.Semantics;
using Cultiway.Utils;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Sects;

/// <summary>
/// 建宗完成时用于生成名称的稳定语义快照。
/// </summary>
public sealed class SectNamingContext
{
    public string Signature;
    public int Seed;
    public int PrimaryElement = NamingRuleUtils.NoElement;
    public string CultivateMethodId;
    public string DoctrineCore;
    public string DoctrineShort;
    public string ResidenceName;
    public string ResidenceCore;
    public string ResidenceFull;
    public string HomeCityCore;
    public string Direction;
    public readonly HashSet<string> TraitIds = new(StringComparer.Ordinal);
    public readonly HashSet<string> DoctrineSemanticIds = new(StringComparer.Ordinal);

    public bool HasTrait(SectTrait trait)
    {
        return trait != null && TraitIds.Contains(trait.id);
    }

    public bool HasDoctrineSemantic(SemanticAsset semantic)
    {
        return semantic != null && DoctrineSemanticIds.Contains(semantic.id);
    }

    /// <summary>
    /// 在宗门创宗信息完整后提取一次命名上下文。
    /// </summary>
    public static SectNamingContext Create(Sect sect, Actor founder)
    {
        CultibookAsset doctrine = sect.GetDoctrineCultibook();
        var context = new SectNamingContext
        {
            CultivateMethodId = doctrine?.CultivateMethodId ?? string.Empty,
            DoctrineCore = SectNameText.ExtractDoctrineCore(doctrine?.Name),
            ResidenceName = SectNameText.NormalizeToken(sect.data.ResidenceName),
            HomeCityCore = SectNameText.Shorten(SectNameText.NormalizeToken(sect.data.HomeCityName), 3)
        };
        context.DoctrineShort = SectNameText.Shorten(context.DoctrineCore, 2);
        context.PrimaryElement = ResolvePrimaryElement(doctrine);
        CollectDoctrineSemantics(context, doctrine);
        CollectTraits(context, sect);
        CollectResidence(context, sect);

        string traitSignature = string.Join("|", context.TraitIds.OrderBy(id => id, StringComparer.Ordinal));
        long worldSeed = World.world.map_stats.life_dna;
        context.Signature =
            $"{worldSeed}|{sect.getID()}|{founder.data.id}|{doctrine?.id}|{sect.data.ResidenceTileID}|{traitSignature}";
        context.Seed = NamingRuleUtils.StableHash(context.Signature);
        return context;
    }

    private static int ResolvePrimaryElement(CultibookAsset doctrine)
    {
        if (doctrine == null) return NamingRuleUtils.NoElement;

        ElementRequirement requirement = doctrine.ElementReq;
        float[] values =
        [
            requirement.MinIron,
            requirement.MinWood,
            requirement.MinWater,
            requirement.MinFire,
            requirement.MinEarth,
            requirement.MinNeg,
            requirement.MinPos,
            requirement.MinEntropy
        ];
        return NamingRuleUtils.GetMaxIndex(values, out _);
    }

    private static void CollectDoctrineSemantics(SectNamingContext context, CultibookAsset doctrine)
    {
        if (doctrine?.Semantics == null) return;

        HashSet<SemanticAsset> semantics = new();
        doctrine.Semantics.CollectExpanded(ModClass.L.SemanticLibrary, semantics);
        foreach (SemanticAsset semantic in semantics)
        {
            context.DoctrineSemanticIds.Add(semantic.id);
        }
    }

    private static void CollectTraits(SectNamingContext context, Sect sect)
    {
        foreach (SectTrait trait in sect.getTraits())
        {
            context.TraitIds.Add(trait.id);
        }
    }

    private static void CollectResidence(SectNamingContext context, Sect sect)
    {
        WorldTile tile = sect.GetResidenceTile();
        if (tile == null) return;

        GeoRegion region = tile.GetExtend().GetGeoRegion(GeoRegionLayer.Landform)
                           ?? tile.GetExtend().GetGeoRegion(GeoRegionLayer.Primary);
        string regionType = region?.GetCategory().GetDisplayName();

        context.ResidenceCore = SectNameText.ExtractResidenceCore(context.ResidenceName, regionType);
        context.ResidenceFull = SectNameText.IsResidenceStyleName(context.ResidenceName)
            ? context.ResidenceName
            : string.Empty;
        context.Direction = ResolveDirection(tile);
    }

    private static string ResolveDirection(WorldTile tile)
    {
        float horizontal = (tile.x + 0.5f) / MapBox.width;
        float vertical = (tile.y + 0.5f) / MapBox.height;
        string x = horizontal < 0.35f ? "西" : horizontal > 0.65f ? "东" : string.Empty;
        string y = vertical < 0.35f ? "南" : vertical > 0.65f ? "北" : string.Empty;
        return y + x;
    }
}

internal static class SectNameText
{
    private static readonly string[] DoctrinePrefixes =
    [
        "太上", "无极", "太清", "九转", "天元", "玄天"
    ];

    private static readonly string[] DoctrineSuffixes =
    [
        "真解", "秘录", "要术", "宝鉴", "玄功", "秘典", "功", "诀", "经", "典", "法", "录", "术"
    ];

    private static readonly string[] ResidenceEndings =
    [
        "山庄", "书院", "山", "谷", "洞", "庄", "院", "宫", "观", "阁", "堂", "会", "府", "宗", "门", "派"
    ];

    internal static string ExtractDoctrineCore(string name)
    {
        string value = NormalizeToken(name);
        foreach (string suffix in DoctrineSuffixes)
        {
            if (value.EndsWith(suffix, StringComparison.Ordinal) && value.Length > suffix.Length)
            {
                value = value.Substring(0, value.Length - suffix.Length);
                break;
            }
        }

        foreach (string prefix in DoctrinePrefixes)
        {
            if (value.StartsWith(prefix, StringComparison.Ordinal) && value.Length - prefix.Length >= 2)
            {
                value = value.Substring(prefix.Length);
                break;
            }
        }

        if (value.StartsWith("真", StringComparison.Ordinal) && value.Length > 3)
        {
            value = value.Substring(1);
        }
        return Shorten(value, 4);
    }

    internal static string ExtractResidenceCore(string residenceName, string regionType)
    {
        string value = NormalizeToken(residenceName);
        string type = NormalizeToken(regionType);
        if (!string.IsNullOrEmpty(type)
            && value.EndsWith(type, StringComparison.Ordinal)
            && value.Length - type.Length >= 2)
        {
            value = value.Substring(0, value.Length - type.Length);
        }
        else if (value.EndsWith("外山门", StringComparison.Ordinal) && value.Length > 3)
        {
            value = value.Substring(0, value.Length - 3);
        }
        return Shorten(value, 3);
    }

    internal static bool IsResidenceStyleName(string name)
    {
        string value = NormalizeToken(name);
        return value.Length is >= 2 and <= 6
               && ResidenceEndings.Any(ending => value.EndsWith(ending, StringComparison.Ordinal));
    }

    internal static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        char[] chars = value.Where(IsCjk).ToArray();
        return new string(chars);
    }

    internal static string Shorten(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

    internal static bool IsCjk(char value)
    {
        return value is >= '\u3400' and <= '\u4dbf'
               or >= '\u4e00' and <= '\u9fff'
               or >= '\uf900' and <= '\ufaff';
    }
}
