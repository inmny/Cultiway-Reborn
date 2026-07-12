using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core.SkillLibV3;

namespace Cultiway.Content;

[Dependency(typeof(SkillCastResources))]
public sealed class SkillCastBudgetRules : ExtendLibrary<SkillCastBudgetRuleAsset, SkillCastBudgetRules>
{
    public static SkillCastBudgetRuleAsset Xian { get; private set; }
    public static SkillCastBudgetRuleAsset Magic { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.SkillCastBudgetRule";

    protected override void OnInit()
    {
        Xian.Priority = 100;
        Xian.MatchResources(SkillCastResources.Wakan);
        Xian.Resolve = ResolveXianBudget;

        Magic.Priority = 100;
        Magic.MatchResources(SkillCastResources.Mana);
        Magic.Resolve = ResolveMagicBudget;
    }

    private static SkillCastBudgetResolution ResolveXianBudget(SkillCastBudgetContext context)
    {
        var level = context.Caster.GetCultisys<Components.Xian>().CurrLevel;
        var budget = level switch
        {
            0 => 1,
            XianLevels.XianBase => 4,
            XianLevels.Jindan => 32,
            XianLevels.Yuanying => 256,
            _ => 1024
        };
        return new SkillCastBudgetResolution(budget, level >= XianLevels.Yuanying);
    }

    private static SkillCastBudgetResolution ResolveMagicBudget(SkillCastBudgetContext context)
    {
        if (!context.Caster.HasCultisys<Magic>()) return new SkillCastBudgetResolution(1);

        var level = context.Caster.GetCultisys<Magic>().CurrLevel;
        var budget = level switch
        {
            0 => 1,
            1 => 4,
            2 => 32,
            3 => 256,
            _ => 1024
        };
        return new SkillCastBudgetResolution(budget, level >= 3);
    }
}
