using System;
using System.Linq;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Editor;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Blueprints;

public sealed class SkillBlueprintExportOptions
{
    public bool PreserveContainerNameAsCustom;
    public SkillBlueprintOriginKind OriginKind = SkillBlueprintOriginKind.Created;
    public long SourceActorId = -1L;
}

public sealed class SkillBlueprintExportResult
{
    public SkillBlueprint Blueprint;
    public SkillCompatibilityResult Validation = new();
    public bool Success => Blueprint != null && Validation.IsCompatible;
}

public sealed class SkillBlueprintExporter
{
    public SkillBlueprintExportResult Export(Entity containerEntity, SkillBlueprintExportOptions options = null)
    {
        options ??= new SkillBlueprintExportOptions();
        var result = new SkillBlueprintExportResult();
        if (containerEntity.IsNull || !containerEntity.HasComponent<SkillContainer>())
        {
            result.Validation.AddError("export.container");
            return result;
        }

        var container = containerEntity.GetComponent<SkillContainer>();
        var blueprint = new SkillBlueprint
        {
            EntityAssetId = container.SkillEntityAssetID,
            AnimationIndex = container.AnimationIndex,
            CastResourceRequirement = container.CastResourceRequirement.DeepClone(),
            TrajectoryAssetId = SkillBlueprintTrajectory.ResolveEffectiveId(containerEntity),
            Origin = new SkillBlueprintOriginData
            {
                Kind = options.OriginKind,
                SourceActorId = options.SourceActorId
            }
        };

        foreach (var componentType in containerEntity.GetComponentTypes()
                     .Where(type => typeof(IModifier).IsAssignableFrom(type))
                     .OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            if (componentType == typeof(Trajectory)) continue;

            var modifierAsset = ModClass.I.SkillV3.ModifierLib.GetByComponentType(componentType);
            if (modifierAsset == null)
            {
                var modifier = (IModifier)containerEntity.GetComponent(componentType);
                result.Validation.AddError("export.descriptor_missing", modifier.ModifierAsset.id);
                continue;
            }
            if (modifierAsset.EditorDerived && !modifierAsset.EditorPersistWhenHidden) continue;
            blueprint.Modifiers.Add(modifierAsset.Export(containerEntity));
        }

        if (options.PreserveContainerNameAsCustom && containerEntity.HasName &&
            !string.IsNullOrWhiteSpace(containerEntity.Name.value))
        {
            blueprint.NameMode = SkillBlueprintNameMode.Custom;
            blueprint.CustomName = containerEntity.Name.value;
        }

        blueprint.Origin.SourceSignature = SkillBlueprintSignature.Build(blueprint);
        result.Blueprint = blueprint;
        return result;
    }
}
