using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Utils;

/// <summary>
/// 为法术容器生成不依赖 CLR 类型名的稳定结构签名。
/// </summary>
public static class SkillContainerSignature
{
    public static string Build(Entity skill)
    {
        if (skill.IsNull || !skill.HasComponent<SkillContainer>()) return string.Empty;

        var container = skill.GetComponent<SkillContainer>();
        var blueprint = new SkillBlueprint
        {
            EntityAssetId = container.SkillEntityAssetID,
            AnimationIndex = container.AnimationIndex,
            CastResourceRequirement = container.CastResourceRequirement.DeepClone(),
            TrajectoryAssetId = SkillBlueprintTrajectory.ResolveEffectiveId(skill)
        };

        foreach (var componentType in skill.GetComponentTypes()
                     .Where(type => typeof(IModifier).IsAssignableFrom(type))
                     .OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            if (componentType == typeof(Trajectory)) continue;

            var modifierAsset = ModClass.I.SkillV3.ModifierLib.GetByComponentType(componentType);
            if (modifierAsset != null)
            {
                if (modifierAsset.EditorDerived && !modifierAsset.EditorPersistWhenHidden) continue;
                blueprint.Modifiers.Add(modifierAsset.Export(skill));
                continue;
            }

            var modifier = (IModifier)skill.GetComponent(componentType);
            var spec = new SkillModifierSpec { AssetId = modifier.ModifierAsset.id };
            foreach (var field in componentType.GetFields(BindingFlags.Instance | BindingFlags.Public)
                         .OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                spec.Parameters[field.Name] = FormatValue(field.GetValue(modifier));
            }
            blueprint.Modifiers.Add(spec);
        }

        return SkillBlueprintSignature.Build(blueprint);
    }

    private static string FormatValue(object value)
    {
        return value switch
        {
            null => string.Empty,
            float number => number.ToString("R", CultureInfo.InvariantCulture),
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            string text => text,
            IEnumerable values => string.Join(",", values.Cast<object>()
                .Select(FormatValue)
                .OrderBy(item => item, StringComparer.Ordinal)),
            _ => value.ToString()
        };
    }
}
