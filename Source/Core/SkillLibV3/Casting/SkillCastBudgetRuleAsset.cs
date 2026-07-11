using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3;

public readonly struct SkillCastBudgetContext
{
    public readonly ActorExtend Caster;
    public readonly Entity Skill;
    public readonly BaseSimObject PrimaryTarget;
    public readonly SkillCastResourceBinding ResourceBinding;

    public SkillCastBudgetContext(ActorExtend caster, Entity skill, BaseSimObject primaryTarget,
        SkillCastResourceBinding resourceBinding)
    {
        Caster = caster;
        Skill = skill;
        PrimaryTarget = primaryTarget;
        ResourceBinding = resourceBinding;
    }
}

public readonly struct SkillCastBudgetResolution
{
    public readonly int MaxSteps;
    public readonly bool ForceFullBudgetAgainstMajorThreat;

    public SkillCastBudgetResolution(int maxSteps, bool forceFullBudgetAgainstMajorThreat = false)
    {
        MaxSteps = maxSteps;
        ForceFullBudgetAgainstMajorThreat = forceFullBudgetAgainstMajorThreat;
    }
}

public delegate SkillCastBudgetResolution SkillCastBudgetResolveAction(SkillCastBudgetContext context);

/// <summary>
/// 按已绑定资源匹配的连发预算规则。具体体系在 Content 中注册规则实现。
/// </summary>
public sealed class SkillCastBudgetRuleAsset : Asset
{
    private readonly HashSet<string> _resourceAssetIds = new();

    public int Priority;
    public SkillCastBudgetResolveAction Resolve;

    public SkillCastBudgetRuleAsset MatchResources(params SkillCastResourceAsset[] resources)
    {
        foreach (var resource in resources) _resourceAssetIds.Add(resource.id);
        return this;
    }

    internal bool Matches(SkillCastResourceBinding binding)
    {
        return binding.Resources.Any(resource => _resourceAssetIds.Contains(resource.id));
    }
}

public sealed class SkillCastBudgetRuleLibrary : AssetLibrary<SkillCastBudgetRuleAsset>
{
}

public static class SkillCastBudgetResolver
{
    public static SkillCastBudgetResolution Resolve(ActorExtend caster, Entity skill, BaseSimObject primaryTarget)
    {
        var binding = SkillCastResourceResolver.Resolve(caster, skill);
        if (binding == null) return new SkillCastBudgetResolution(1);

        var context = new SkillCastBudgetContext(caster, skill, primaryTarget, binding);
        var matched = false;
        var maxSteps = int.MaxValue;
        var forceFullBudget = false;
        foreach (var rule in ModClass.I.SkillV3.CastBudgetRuleLib.list.OrderByDescending(item => item.Priority))
        {
            if (!rule.Matches(binding)) continue;
            var resolution = rule.Resolve(context);
            maxSteps = System.Math.Min(maxSteps, System.Math.Max(1, resolution.MaxSteps));
            forceFullBudget |= resolution.ForceFullBudgetAgainstMajorThreat;
            matched = true;
        }

        return matched
            ? new SkillCastBudgetResolution(maxSteps, forceFullBudget)
            : new SkillCastBudgetResolution(1);
    }
}
