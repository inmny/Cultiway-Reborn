using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.Blueprints;

namespace Cultiway.Core.SkillLibV3.Editor;

public class WanfaPavilionPolicyAsset : Asset
{
    public bool UnrestrictedEntities = true;
    public bool UnrestrictedTrajectories = true;
    public bool UnrestrictedModifiers = true;
    public int MaximumModifierCount = -1;
    public HashSet<string> AvailableEntityIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> AvailableTrajectoryIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> AvailableModifierIds { get; } = new(StringComparer.Ordinal);
    public Func<SkillBlueprint, SkillCompatibilityResult> AdditionalBlueprintValidation;
    public Func<Actor, SkillBlueprint, SkillCompatibilityResult> AdditionalGrantValidation;
    public Action<Actor, SkillBlueprint> GrantCompleted;

    public bool IsEntityAvailable(string id)
    {
        return UnrestrictedEntities || AvailableEntityIds.Contains(id);
    }

    public bool IsTrajectoryAvailable(string id)
    {
        return UnrestrictedTrajectories || AvailableTrajectoryIds.Contains(id);
    }

    public bool IsModifierAvailable(string id)
    {
        return UnrestrictedModifiers || AvailableModifierIds.Contains(id);
    }

    public SkillCompatibilityResult ValidateBlueprint(SkillBlueprint blueprint)
    {
        var result = new SkillCompatibilityResult();
        if (!IsEntityAvailable(blueprint.EntityAssetId))
        {
            result.AddErrorKey("policy.entity_locked", "Cultiway.Wanfa.Validation.policy.entity_locked",
                blueprint.EntityAssetId);
        }
        if (!IsTrajectoryAvailable(blueprint.TrajectoryAssetId))
        {
            result.AddErrorKey("policy.trajectory_locked", "Cultiway.Wanfa.Validation.policy.trajectory_locked",
                blueprint.TrajectoryAssetId);
        }
        foreach (var modifier in blueprint.Modifiers)
        {
            if (!IsModifierAvailable(modifier.AssetId))
            {
                result.AddErrorKey("policy.modifier_locked", "Cultiway.Wanfa.Validation.policy.modifier_locked",
                    modifier.AssetId);
            }
        }
        if (MaximumModifierCount >= 0 && blueprint.Modifiers.Count > MaximumModifierCount)
        {
            result.AddErrorKey("policy.modifier_limit", "Cultiway.Wanfa.Validation.policy.modifier_limit", null,
                MaximumModifierCount);
        }
        if (AdditionalBlueprintValidation != null)
        {
            result.Merge(AdditionalBlueprintValidation(blueprint));
        }
        return result;
    }

    public SkillCompatibilityResult ValidateGrant(Actor actor, SkillBlueprint blueprint)
    {
        var result = ValidateBlueprint(blueprint);
        if (AdditionalGrantValidation != null)
        {
            result.Merge(AdditionalGrantValidation(actor, blueprint));
        }
        return result;
    }

    public void CompleteGrant(Actor actor, SkillBlueprint blueprint)
    {
        GrantCompleted?.Invoke(actor, blueprint);
    }
}

public class WanfaPavilionPolicyLibrary : AssetLibrary<WanfaPavilionPolicyAsset>
{
    public static WanfaPavilionPolicyAsset Free { get; private set; }

    public override void init()
    {
        base.init();
        Free = add(new WanfaPavilionPolicyAsset
        {
            id = "Cultiway.WanfaPavilion.Policy.Free"
        });
    }
}
