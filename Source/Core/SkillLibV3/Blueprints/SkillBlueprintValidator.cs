using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cultiway.Core.SkillLibV3.Editor;

namespace Cultiway.Core.SkillLibV3.Blueprints;

public sealed class SkillBlueprintValidator
{
    public SkillCompatibilityResult Validate(SkillBlueprint blueprint)
    {
        var result = new SkillCompatibilityResult();
        if (blueprint == null)
        {
            return result.AddError("blueprint.null");
        }

        ValidateIdentity(blueprint, result);
        if (blueprint.Modifiers == null)
        {
            return result.AddError("blueprint.modifiers_null", blueprint.Id);
        }
        if (blueprint.Modifiers.Any(item => item == null))
        {
            return result.AddError("blueprint.modifier_null", blueprint.Id);
        }
        var context = SkillEditContext.Create(blueprint);
        ValidateEntity(context, result);
        ValidateTrajectory(context, result);
        ValidateModifiers(context, result);
        ValidateName(blueprint, result);
        AddBalanceWarnings(blueprint, result);
        return result;
    }

    private static void ValidateIdentity(SkillBlueprint blueprint, SkillCompatibilityResult result)
    {
        if (string.IsNullOrWhiteSpace(blueprint.Id))
        {
            result.AddError("blueprint.id");
        }
        if (blueprint.Revision < 1)
        {
            result.AddError("blueprint.revision", blueprint.Id);
        }
        if (blueprint.SchemaVersion != SkillBlueprint.CurrentSchemaVersion)
        {
            result.AddError("blueprint.schema", blueprint.Id, blueprint.SchemaVersion);
        }
    }

    private static void ValidateEntity(SkillEditContext context, SkillCompatibilityResult result)
    {
        if (string.IsNullOrWhiteSpace(context.Blueprint.EntityAssetId))
        {
            result.AddError("entity.empty");
            return;
        }
        if (context.EntityAsset == null)
        {
            result.AddError("entity.missing", context.Blueprint.EntityAssetId);
            return;
        }
        if (string.IsNullOrEmpty(context.EntityAsset.EditorCategoryKey))
        {
            result.AddError("entity.descriptor_missing", context.Blueprint.EntityAssetId);
            return;
        }
        if (!context.EntityAsset.EditorSelectable)
        {
            result.AddError("entity.internal", context.Blueprint.EntityAssetId);
        }
        if (context.EntityAsset.Animations.Count == 0)
        {
            result.AddError("entity.animation_empty", context.Blueprint.EntityAssetId);
            return;
        }
        if (context.Blueprint.AnimationIndex < 0 ||
            context.Blueprint.AnimationIndex >= context.EntityAsset.Animations.Count)
        {
            result.AddError("animation.index", context.Blueprint.EntityAssetId,
                context.Blueprint.AnimationIndex, context.EntityAsset.Animations.Count);
        }
    }

    private static void ValidateTrajectory(SkillEditContext context, SkillCompatibilityResult result)
    {
        if (string.IsNullOrWhiteSpace(context.Blueprint.TrajectoryAssetId))
        {
            result.AddError("trajectory.empty");
            return;
        }
        if (context.TrajectoryAsset == null)
        {
            result.AddError("trajectory.missing", context.Blueprint.TrajectoryAssetId);
            return;
        }
        if (string.IsNullOrEmpty(context.TrajectoryAsset.EditorDescriptionKey))
        {
            result.AddError("trajectory.descriptor_missing", context.Blueprint.TrajectoryAssetId);
            return;
        }

        result.Merge(context.TrajectoryAsset.CheckEditorCompatibility(context, false));
    }

    private static void ValidateModifiers(SkillEditContext context, SkillCompatibilityResult result)
    {
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenComponents = new HashSet<Type>();
        var conflictOwners = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var spec in context.Blueprint.Modifiers)
        {
            if (string.IsNullOrWhiteSpace(spec.AssetId))
            {
                result.AddError("modifier.empty");
                continue;
            }
            if (!seenIds.Add(spec.AssetId))
            {
                result.AddError("modifier.duplicate", spec.AssetId);
                continue;
            }

            var modifierAsset = ModClass.I.SkillV3.ModifierLib.get(spec.AssetId);
            if (modifierAsset == null)
            {
                result.AddError("modifier.missing", spec.AssetId);
                continue;
            }

            if (modifierAsset.EditorComponentType == null)
            {
                result.AddError("modifier.descriptor_missing", spec.AssetId);
                continue;
            }
            if (!modifierAsset.EditorSelectable && !modifierAsset.EditorPersistWhenHidden)
            {
                result.AddError("modifier.internal", spec.AssetId);
            }
            if (!seenComponents.Add(modifierAsset.EditorComponentType))
            {
                result.AddError("modifier.component_duplicate", spec.AssetId);
            }

            ValidateFields(spec, modifierAsset, result);
            result.Merge(modifierAsset.CheckEditorCompatibility(context, spec));

            foreach (var conflictTag in modifierAsset.ConflictTags)
            {
                if (conflictOwners.TryGetValue(conflictTag, out var owner))
                {
                    result.AddError("modifier.conflict", spec.AssetId, owner.Localize());
                }
                else
                {
                    conflictOwners[conflictTag] = spec.AssetId;
                }
            }
        }
    }

    private static void ValidateFields(SkillModifierSpec spec, SkillModifierAsset modifierAsset,
        SkillCompatibilityResult result)
    {
        if (spec.Parameters == null)
        {
            result.AddError("modifier.parameters_null", spec.AssetId);
            return;
        }
        var knownFields = new HashSet<string>(modifierAsset.EditorFields.Select(field => field.ParameterKey),
            StringComparer.Ordinal);
        foreach (var parameter in spec.Parameters.Keys)
        {
            if (!knownFields.Contains(parameter))
            {
                result.AddError("modifier.parameter_unknown", spec.AssetId, parameter);
            }
        }
        foreach (var field in modifierAsset.EditorFields)
        {
            if (!spec.Parameters.TryGetValue(field.ParameterKey, out var value))
            {
                result.AddError("modifier.parameter_missing", spec.AssetId, field.DisplayName);
                continue;
            }
            if (!field.TryNormalizeStoredValue(value, out var normalizedValue, out var error))
            {
                result.AddError("modifier.parameter_invalid", spec.AssetId, error);
                continue;
            }
            if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
            {
                result.AddError("modifier.parameter_noncanonical", spec.AssetId, field.DisplayName);
                continue;
            }

            double numericValue = 0d;
            var numeric = field.Kind == SkillEditorFieldKind.Float &&
                          double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
                              out numericValue);
            if (field.Kind == SkillEditorFieldKind.Integer &&
                long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integerValue))
            {
                numericValue = integerValue;
                numeric = true;
            }
            if (numeric && field.MaxValue > field.MinValue &&
                numericValue >= field.MinValue + (field.MaxValue - field.MinValue) * 0.9d)
            {
                result.AddWarning("balance.parameter_extreme", spec.AssetId, field.DisplayName);
            }
        }
    }

    private static void ValidateName(SkillBlueprint blueprint, SkillCompatibilityResult result)
    {
        if (!Enum.IsDefined(typeof(SkillBlueprintNameMode), blueprint.NameMode))
        {
            result.AddError("name.mode_invalid", blueprint.Id);
        }
        else if (blueprint.NameMode == SkillBlueprintNameMode.Custom &&
                 string.IsNullOrWhiteSpace(blueprint.CustomName))
        {
            result.AddError("name.custom_empty", blueprint.Id);
        }
        else if (blueprint.NameMode == SkillBlueprintNameMode.Custom && blueprint.CustomName.Length > 24)
        {
            result.AddError("name.custom_too_long", blueprint.Id, 24);
        }
        if (!string.IsNullOrWhiteSpace(blueprint.Category) && blueprint.Category.Length > 12)
        {
            result.AddError("category.too_long", blueprint.Id, 12);
        }
    }

    private static void AddBalanceWarnings(SkillBlueprint blueprint, SkillCompatibilityResult result)
    {
        var estimatedStepCost = 1f + blueprint.Modifiers.Count * 0.1f;
        if (estimatedStepCost >= 3f)
        {
            result.AddWarning("balance.cost", blueprint.Id, estimatedStepCost);
        }

        var trajectory = ModClass.I.SkillV3.TrajLib.get(blueprint.TrajectoryAssetId);
        if (trajectory == null) return;

        foreach (var spec in blueprint.Modifiers)
        {
            var modifier = ModClass.I.SkillV3.ModifierLib.get(spec.AssetId);
            if (modifier == null) continue;
            if (modifier.EditorSemanticTags.Contains(SkillEditorSemanticTags.Speed) &&
                (trajectory.EditorSemanticTags.Contains(SkillEditorSemanticTags.Static) ||
                 trajectory.EditorSemanticTags.Contains(SkillEditorSemanticTags.Instant)))
            {
                result.AddWarning("semantic.speed_static", spec.AssetId);
            }
            if (modifier.EditorSemanticTags.Contains(SkillEditorSemanticTags.OnTravel) &&
                trajectory.EditorSemanticTags.Contains(SkillEditorSemanticTags.Instant))
            {
                result.AddWarning("semantic.travel_instant", spec.AssetId);
            }
        }
    }
}
