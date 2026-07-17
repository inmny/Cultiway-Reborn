using System;
using System.Collections.Generic;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.SkillLibV3.Blueprints;

namespace Cultiway.Core.SkillLibV3.Editor;

public sealed class SkillEditContext
{
    public SkillBlueprint Blueprint { get; private set; }
    public SkillEntityAsset EntityAsset { get; private set; }
    public TrajectoryAsset TrajectoryAsset { get; private set; }
    public HashSet<SemanticAsset> Semantics { get; } = new();
    public HashSet<string> EditorCompatibilityKeys { get; } = new(StringComparer.Ordinal);
    public HashSet<string> SelectedModifierIds { get; } = new(StringComparer.Ordinal);

    public static SkillEditContext Create(SkillBlueprint blueprint)
    {
        var context = new SkillEditContext { Blueprint = blueprint };
        if (!string.IsNullOrEmpty(blueprint.EntityAssetId))
        {
            context.EntityAsset = ModClass.I.SkillV3.SkillLib.get(blueprint.EntityAssetId);
            if (context.EntityAsset != null)
            {
                SkillSemanticCollector.CollectAssetSemantics(context.EntityAsset, context.Semantics);
            }
        }

        if (!string.IsNullOrEmpty(blueprint.TrajectoryAssetId))
        {
            context.TrajectoryAsset = ModClass.I.SkillV3.TrajLib.get(blueprint.TrajectoryAssetId);
            if (context.TrajectoryAsset != null)
            {
                SkillSemanticCollector.CollectDescriptorSemantics(
                    context.TrajectoryAsset.Semantics, context.Semantics);
                context.EditorCompatibilityKeys.UnionWith(context.TrajectoryAsset.EditorCompatibilityKeys);
            }
        }

        foreach (var modifier in blueprint.Modifiers)
        {
            if (string.IsNullOrWhiteSpace(modifier.AssetId)) continue;
            context.SelectedModifierIds.Add(modifier.AssetId);
            var modifierAsset = ModClass.I.SkillV3.ModifierLib.get(modifier.AssetId);
            if (modifierAsset != null)
            {
                SkillSemanticCollector.CollectDescriptorSemantics(modifierAsset.Semantics, context.Semantics);
                context.EditorCompatibilityKeys.UnionWith(modifierAsset.EditorCompatibilityKeys);
            }
        }
        return context;
    }
}
