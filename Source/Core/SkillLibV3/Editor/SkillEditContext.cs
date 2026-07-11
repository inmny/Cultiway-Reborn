using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.Blueprints;

namespace Cultiway.Core.SkillLibV3.Editor;

public sealed class SkillEditContext
{
    public SkillBlueprint Blueprint { get; private set; }
    public SkillEntityAsset EntityAsset { get; private set; }
    public TrajectoryAsset TrajectoryAsset { get; private set; }
    public HashSet<string> SemanticTags { get; } = new(StringComparer.Ordinal);
    public HashSet<string> SelectedModifierIds { get; } = new(StringComparer.Ordinal);

    public static SkillEditContext Create(SkillBlueprint blueprint)
    {
        var context = new SkillEditContext { Blueprint = blueprint };
        if (!string.IsNullOrEmpty(blueprint.EntityAssetId))
        {
            context.EntityAsset = ModClass.I.SkillV3.SkillLib.get(blueprint.EntityAssetId);
            if (context.EntityAsset != null)
            {
                context.SemanticTags.UnionWith(context.EntityAsset.SeriesTags);
            }
        }

        if (!string.IsNullOrEmpty(blueprint.TrajectoryAssetId))
        {
            context.TrajectoryAsset = ModClass.I.SkillV3.TrajLib.get(blueprint.TrajectoryAssetId);
            if (context.TrajectoryAsset != null)
            {
                context.SemanticTags.UnionWith(context.TrajectoryAsset.MotionTags);
                context.SemanticTags.UnionWith(context.TrajectoryAsset.EditorSemanticTags);
            }
        }

        foreach (var modifier in blueprint.Modifiers)
        {
            if (string.IsNullOrWhiteSpace(modifier.AssetId)) continue;
            context.SelectedModifierIds.Add(modifier.AssetId);
            var modifierAsset = ModClass.I.SkillV3.ModifierLib.get(modifier.AssetId);
            if (modifierAsset != null)
            {
                context.SemanticTags.UnionWith(modifierAsset.SimilarityTags);
            }
            if (modifierAsset != null)
            {
                context.SemanticTags.UnionWith(modifierAsset.EditorSemanticTags);
            }
        }
        return context;
    }
}
