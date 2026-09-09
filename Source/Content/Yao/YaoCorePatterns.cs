using System;
using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Core.Semantics;

namespace Cultiway.Content.YaoBeasts;

/// <summary>妖丹方向的静态定义。</summary>
public sealed class YaoCorePatternAsset
{
    /// <summary>方向编号。</summary>
    public string Id;

    /// <summary>本地化名称键。</summary>
    public string NameKey;

    /// <summary>方向形成倾向：语义编号到权重。</summary>
    public (string semanticId, float weight)[] Tendencies;

    /// <summary>方向提供的妖力容量系数。</summary>
    public float PowerCapacityMultiplier;

    /// <summary>方向提供的核心神通技能资产编号。</summary>
    public string[] SkillIds;

    /// <summary>方向对天劫元素的弱抗性语义（劫伤更高的元素）。</summary>
    public string[] WeakAgainstSemantics;

    /// <summary>凝丹所需的最低身体稳定度。</summary>
    public float RequiredStability;
}

/// <summary>首批妖丹方向与登记入口。</summary>
public static class YaoCorePatterns
{
    /// <summary>水行妖丹。</summary>
    public static YaoCorePatternAsset Water { get; private set; }

    /// <summary>毒行妖丹。</summary>
    public static YaoCorePatternAsset Poison { get; private set; }

    /// <summary>火行妖丹。</summary>
    public static YaoCorePatternAsset Fire { get; private set; }

    /// <summary>厚土妖丹。</summary>
    public static YaoCorePatternAsset Earth { get; private set; }

    /// <summary>全部已登记方向。</summary>
    public static readonly List<YaoCorePatternAsset> All = new();

    /// <summary>按编号读取方向。</summary>
    public static YaoCorePatternAsset Get(string patternId)
    {
        foreach (YaoCorePatternAsset pattern in All)
        {
            if (string.Equals(pattern.Id, patternId, StringComparison.Ordinal)) return pattern;
        }

        return null;
    }

    internal static void Initialize()
    {
        Water = Register(new YaoCorePatternAsset
        {
            Id = "yao.core.water",
            NameKey = "Cultiway.Yao.CorePattern.water",
            Tendencies = new[] { ("semantic.element.water", 1f), ("semantic.element.ice", 0.6f) },
            PowerCapacityMultiplier = 1.2f,
            SkillIds = new[] { "Cultiway.WaterBlade", "Cultiway.WaterWall" },
            WeakAgainstSemantics = new[] { "semantic.element.lightning" },
            RequiredStability = 55f,
        });
        Poison = Register(new YaoCorePatternAsset
        {
            Id = "yao.core.poison",
            NameKey = "Cultiway.Yao.CorePattern.poison",
            Tendencies = new[] { ("semantic.element.poison", 1f), ("semantic.element.wood", 0.5f) },
            PowerCapacityMultiplier = 1.1f,
            SkillIds = new[] { "Cultiway.PoisonMist", "Cultiway.PoisonPool" },
            WeakAgainstSemantics = new[] { "semantic.element.fire" },
            RequiredStability = 50f,
        });
        Fire = Register(new YaoCorePatternAsset
        {
            Id = "yao.core.fire",
            NameKey = "Cultiway.Yao.CorePattern.fire",
            Tendencies = new[] { ("semantic.element.fire", 1f), ("semantic.element.lightning", 0.4f) },
            PowerCapacityMultiplier = 1.3f,
            SkillIds = new[] { "Cultiway.FireBlade", "Cultiway.Fireball" },
            WeakAgainstSemantics = new[] { "semantic.element.water", "semantic.element.ice" },
            RequiredStability = 60f,
        });
        Earth = Register(new YaoCorePatternAsset
        {
            Id = "yao.core.earth",
            NameKey = "Cultiway.Yao.CorePattern.earth",
            Tendencies = new[] { ("semantic.element.earth", 1f) },
            PowerCapacityMultiplier = 1f,
            SkillIds = new[] { "Cultiway.GroundSpike", "Cultiway.EarthWall" },
            WeakAgainstSemantics = new[] { "semantic.element.wind" },
            RequiredStability = 45f,
        });
    }

    private static YaoCorePatternAsset Register(YaoCorePatternAsset pattern)
    {
        All.Add(pattern);
        return pattern;
    }

    /// <summary>按妖兽的语义档案、血脉方向与真实生活经历评分并选择主方向。</summary>
    public static YaoCorePatternAsset ResolveBest(ActorExtend actor, string bloodlineId)
    {
        SemanticProfile profile = actor.GetSemanticProfile();
        YaoCorePatternAsset best = null;
        float bestScore = float.MinValue;

        foreach (YaoCorePatternAsset pattern in All)
        {
            float score = 0f;
            foreach ((string semanticId, float weight) in pattern.Tendencies)
            {
                if (!ModClass.L.SemanticLibrary.TryResolve(semanticId, out SemanticAsset semantic)) continue;
                score += profile.GetScore(semantic, SemanticQueryPolicy.Default).Net * weight;
            }

            // 血脉方向提供有限加成，但不强制唯一结果。
            if (YaoBloodlines.TryGet(bloodlineId, out YaoBloodlineAsset bloodline) &&
                Array.IndexOf(bloodline.CorePatternIds, pattern.Id) >= 0)
            {
                score += 0.5f;
            }

            if (score <= bestScore) continue;
            bestScore = score;
            best = pattern;
        }

        return best ?? Water;
    }
}
