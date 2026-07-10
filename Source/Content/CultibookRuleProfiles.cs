using System;
using Cultiway.Abstract;
using Cultiway.Content.Libraries;

namespace Cultiway.Content;

[Dependency(typeof(CultivateMethods))]
internal class CultibookRuleProfiles : ExtendLibrary<CultibookRuleProfileAsset, CultibookRuleProfiles>
{
    public static CultibookRuleProfileAsset Balanced { get; private set; }
    public static CultibookRuleProfileAsset Mastery { get; private set; }
    public static CultibookRuleProfileAsset Guard { get; private set; }
    public static CultibookRuleProfileAsset Water { get; private set; }
    public static CultibookRuleProfileAsset Battle { get; private set; }
    public static CultibookRuleProfileAsset Killing { get; private set; }
    public static CultibookRuleProfileAsset Fortune { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.CultibookRuleProfile";

    protected override void OnInit()
    {
        Set(Balanced, "balanced", ["归元", "周天", "太玄"], ["功", "经", "诀"],
            "调和精通与护体", 0.5f, 0.5f, 0.55f, 0.52f, 0.3f, 2, 0f,
            context => IsStandard(context)
                ? 10f - Math.Abs(context.MasteryBias - context.ArmorBias) * 4f
                : 0f);

        Set(Mastery, "mastery", ["御灵", "衍法", "通玄"], ["诀", "经", "典"],
            "偏重元素精通与术法运转", 0.78f, 0.22f, 0.35f, 0.62f, 0.38f, 3, 0.01f,
            context => IsStandard(context)
                ? 8f + Math.Max(0f, context.MasteryBias - context.ArmorBias) * 8f + context.AlignedSkillCount
                : 0f);

        Set(Guard, "guard", ["镇元", "护脉", "玄甲"], ["功", "经", "法"],
            "偏重元素抗性与护体根基", 0.32f, 0.68f, 0.45f, 0.5f, 0.28f, 2, -0.005f,
            context => IsStandard(context)
                ? 8f + Math.Max(0f, context.ArmorBias - context.MasteryBias) * 8f
                : 0f);

        Set(Water, "water", ["沧溟", "玄潮", "寒渊"], ["经", "诀", "功"],
            "借水势周流灵息并兼顾护体", 0.62f, 0.38f, 0.7f, 0.58f, 0.36f, 3, 0.005f,
            context => IsMethod(context, CultivateMethods.WaterMeditation) ? 100f : 0f);

        Set(Battle, "battle", ["斗战", "百炼", "破军"], ["诀", "典", "功"],
            "以战养法，着重精通与法术领悟", 0.84f, 0.16f, 0.4f, 0.66f, 0.42f, 3, 0.02f,
            context => IsMethod(context, CultivateMethods.BattleCultivate) ? 100f : 0f);

        Set(Killing, "killing", ["血煞", "噬魂", "幽冥"], ["经", "诀", "典"],
            "以杀伐之气催动元素精通", 0.9f, 0.1f, 0.25f, 0.72f, 0.48f, 3, 0.025f,
            context => IsMethod(context, CultivateMethods.KillAbsorb) ? 100f : 0f);

        Set(Fortune, "fortune", ["皇极", "山河", "紫气"], ["经", "典", "诀"],
            "汇聚国运，兼容多重法理", 0.55f, 0.45f, 0.82f, 0.48f, 0.26f, 3, 0f,
            context => IsMethod(context, CultivateMethods.KingdomFortune) ? 100f : 0f);
    }

    private static void Set(CultibookRuleProfileAsset profile, string tag, string[] nameStems,
        string[] suffixes, string description, float masteryWeight, float armorWeight,
        float secondaryWeight, float requirementRatio, float affinityThreshold, int maxSkillCount,
        float skillChanceBonus, Func<CultibookRuleContext, float> score)
    {
        profile.Tag = tag;
        profile.NameStems = nameStems;
        profile.Suffixes = suffixes;
        profile.DescriptionFragment = description;
        profile.MasteryWeight = masteryWeight;
        profile.ArmorWeight = armorWeight;
        profile.SecondaryElementWeight = secondaryWeight;
        profile.RequirementRatio = requirementRatio;
        profile.AffinityThreshold = affinityThreshold;
        profile.MaxSkillCount = maxSkillCount;
        profile.SkillChanceBonus = skillChanceBonus;
        profile.ScoreContext = score;
    }

    private static bool IsStandard(CultibookRuleContext context)
    {
        return IsMethod(context, CultivateMethods.Standard);
    }

    private static bool IsMethod(CultibookRuleContext context, CultivateMethodAsset method)
    {
        return context != null && method != null && context.CultivateMethodId == method.id;
    }
}
