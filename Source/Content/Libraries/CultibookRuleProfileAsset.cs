using System;

namespace Cultiway.Content.Libraries;

public class CultibookRuleProfileAsset : Asset
{
    public string Tag;
    public string[] NameStems = [];
    public string[] Suffixes = ["功", "诀", "经"];
    public string DescriptionFragment;
    public float MasteryWeight = 0.5f;
    public float ArmorWeight = 0.5f;
    public float SecondaryElementWeight = 0.5f;
    public float RequirementRatio = 0.55f;
    public float AffinityThreshold = 0.3f;
    public int MaxSkillCount = 3;
    public float SkillChanceBonus;
    internal Func<CultibookRuleContext, float> ScoreContext;

    internal float ScoreFor(CultibookRuleContext context)
    {
        return Math.Max(0f, ScoreContext?.Invoke(context) ?? 0f);
    }

    internal string PickNameStem(int seed)
    {
        if (NameStems == null || NameStems.Length == 0) return "归元";
        return NameStems[(seed & int.MaxValue) % NameStems.Length];
    }

    internal string PickSuffix(int seed)
    {
        if (Suffixes == null || Suffixes.Length == 0) return "功";
        return Suffixes[(seed & int.MaxValue) % Suffixes.Length];
    }
}
