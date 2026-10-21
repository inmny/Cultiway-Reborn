using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Impacts;
using UnityEngine;

namespace Cultiway.Content;

[Dependency(typeof(SkillCastResources))]
public sealed class SkillCastBudgetRules : ExtendLibrary<SkillCastBudgetRuleAsset, SkillCastBudgetRules>
{
    public static SkillCastBudgetRuleAsset Xian { get; private set; }
    public static SkillCastBudgetRuleAsset Magic { get; private set; }
    public static SkillCastBudgetRuleAsset Knight { get; private set; }

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

        Knight.Priority = 100;
        Knight.MatchResources(SkillCastResources.Vigor);
        Knight.Resolve = ResolveKnightBudget;
    }

    private static SkillCastBudgetResolution ResolveXianBudget(SkillCastBudgetContext context)
    {
        var level = context.Caster.GetCultisys<Components.Xian>().CurrLevel;
        return ResolveStructuredBudget(context, level);
    }

    /// <summary>高阶强度体现在真实范围与完整重招；领域和光束不再自动复制上千份。</summary>
    /// <param name="context">实际资源和技能。</param><param name="level">当前体系阶段。</param>
    private static SkillCastBudgetResolution ResolveStructuredBudget(SkillCastBudgetContext context, int level)
    {
        var profile = context.Skill.GetComponent<SkillContainer>().Asset.ImpactProfile;
        if(profile.IsField || profile.IsBarrier || profile.IsBeam || profile.Kind==SkillImpactKind.GroundWave)
            return new SkillCastBudgetResolution(1);
        int baseCount=level switch{0=>1,1=>2,2=>3,3=>4,_=>6};
        if(profile.Kind is SkillImpactKind.Explosion or SkillImpactKind.HeavySkyfall or SkillImpactKind.GroundManifest)
            baseCount=level>=3?2:1;
        int salvo=context.Skill.TryGetComponent(out SalvoCount repeat)?Mathf.Max(0,repeat.Value-1):0;
        int burst=context.Skill.TryGetComponent(out BurstCount spread)?Mathf.Max(0,spread.Value-1):0;
        return new SkillCastBudgetResolution(Mathf.Clamp(baseCount+salvo+burst,1,24));
    }

    private static SkillCastBudgetResolution ResolveMagicBudget(SkillCastBudgetContext context)
    {
        if (!context.Caster.HasCultisys<Magic>()) return new SkillCastBudgetResolution(1);

        var level = context.Caster.GetCultisys<Magic>().CurrLevel;
        return ResolveStructuredBudget(context, level);
    }

    private static SkillCastBudgetResolution ResolveKnightBudget(SkillCastBudgetContext context)
    {
        if (!context.Caster.HasCultisys<Components.Knight>()) return new SkillCastBudgetResolution(1);

        var level = context.Caster.GetCultisys<Components.Knight>().CurrLevel;
        return ResolveStructuredBudget(context, level);
    }
}
