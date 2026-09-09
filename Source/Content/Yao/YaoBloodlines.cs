using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Const;

namespace Cultiway.Content.YaoBeasts;

/// <summary>静态血脉定义：一条预先设计好的成长路线。</summary>
public sealed class YaoBloodlineAsset
{
    /// <summary>血脉编号。</summary>
    public string Id;

    /// <summary>本地化名称键。</summary>
    public string NameKey;

    /// <summary>血脉允许的身体结构编号。</summary>
    public string[] AllowedBodyPlanIds;

    /// <summary>血脉先天携带的器官（器官编号与等级）。</summary>
    public (string organId, int rank, string slotId)[] InnateOrgans;

    /// <summary>隐性等位池：可能被遗传但未表达的器官。</summary>
    public string[] HiddenAllelePool;

    /// <summary>固定形态路线：形态类别到固定形态编号。</summary>
    public (YaoFormKind kind, string morphId, string bodyPlanId, int requiredRealm)[] MorphRoutes;

    /// <summary>倾向的妖丹方向编号。</summary>
    public string[] CorePatternIds;

    /// <summary>对天劫元素的应对倾向。</summary>
    public float TribulationAffinity;

    /// <summary>返祖规则：返祖时优先挑选的器官。</summary>
    public string[] AtavismOrganPool;
}

/// <summary>首批血脉与登记入口。</summary>
public static class YaoBloodlines
{
    /// <summary>蛟龙血脉：蛇形、蛟形、龙形。</summary>
    public static YaoBloodlineAsset Jiaolong { get; private set; }

    /// <summary>月狐血脉：狐形与人形。</summary>
    public static YaoBloodlineAsset MoonFox { get; private set; }

    /// <summary>金乌血脉：鸟形真身。</summary>
    public static YaoBloodlineAsset GoldenCrow { get; private set; }

    /// <summary>全部已登记血脉。</summary>
    public static readonly List<YaoBloodlineAsset> All = new();

    /// <summary>按编号读取血脉。</summary>
    public static bool TryGet(string bloodlineId, out YaoBloodlineAsset bloodline)
    {
        bloodline = null;
        if (string.IsNullOrEmpty(bloodlineId)) return false;
        foreach (YaoBloodlineAsset asset in All)
        {
            if (string.Equals(asset.Id, bloodlineId, StringComparison.Ordinal))
            {
                bloodline = asset;
                return true;
            }
        }

        return false;
    }

    internal static void Initialize()
    {
        Jiaolong = Register(new YaoBloodlineAsset
        {
            Id = "yao.bloodline.jiaolong",
            NameKey = "Cultiway.Yao.Bloodline.jiaolong",
            AllowedBodyPlanIds = new[] { "yao.serpentine", "yao.dragon" },
            InnateOrgans = new[]
            {
                ("yao.scale.jiaolong", 2, YaoContent.Slots.Surface),
            },
            HiddenAllelePool = new[]
            {
                "yao.scale.jiaolong", "yao.lung.cloud", "yao.horn.thunder", "yao.eye.dragon",
            },
            MorphRoutes = new[]
            {
                (YaoFormKind.TrueForm, "yao.dragon.base", "yao.dragon", 3),
            },
            CorePatternIds = new[] { "yao.core.water" },
            TribulationAffinity = 0.2f,
            AtavismOrganPool = new[] { "yao.scale.jiaolong", "yao.lung.cloud", "yao.horn.thunder", "yao.eye.dragon" },
        });

        MoonFox = Register(new YaoBloodlineAsset
        {
            Id = "yao.bloodline.moon_fox",
            NameKey = "Cultiway.Yao.Bloodline.moon_fox",
            AllowedBodyPlanIds = new[] { "yao.quadruped" },
            InnateOrgans = new[]
            {
                ("yao.fur.moonlight", 2, YaoContent.Slots.Surface),
            },
            HiddenAllelePool = new[]
            {
                "yao.fur.moonlight", "yao.crown.tails", "yao.eye.illusion", "yao.gland.foxfire",
            },
            MorphRoutes = Array.Empty<(YaoFormKind, string, string, int)>(),
            CorePatternIds = new[] { "yao.core.poison" },
            TribulationAffinity = 0f,
            AtavismOrganPool = new[] { "yao.crown.tails", "yao.eye.illusion", "yao.gland.foxfire" },
        });

        GoldenCrow = Register(new YaoBloodlineAsset
        {
            Id = "yao.bloodline.golden_crow",
            NameKey = "Cultiway.Yao.Bloodline.golden_crow",
            AllowedBodyPlanIds = new[] { "yao.quadruped", "yao.serpentine" },
            InnateOrgans = new[]
            {
                ("yao.feather.fire", 2, YaoContent.Slots.Surface),
            },
            HiddenAllelePool = new[]
            {
                "yao.feather.fire", "yao.vent.truefire", "yao.heart.nirvana",
            },
            MorphRoutes = Array.Empty<(YaoFormKind, string, string, int)>(),
            CorePatternIds = new[] { "yao.core.fire" },
            TribulationAffinity = 0.35f,
            AtavismOrganPool = new[] { "yao.vent.truefire", "yao.heart.nirvana" },
        });
    }

    private static YaoBloodlineAsset Register(YaoBloodlineAsset bloodline)
    {
        All.Add(bloodline);
        return bloodline;
    }
}
