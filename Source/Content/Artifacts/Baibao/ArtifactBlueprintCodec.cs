using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Artifacts.Baibao;

/// <summary>
/// 在法宝实体与百宝阁蓝图之间转换。转换边界明确排除持有者和世界运行状态。
/// </summary>
public static class ArtifactBlueprintCodec
{
    public static ArtifactBlueprint Capture(Entity artifact, Actor sourceActor)
    {
        ArtifactBlueprint blueprint = new()
        {
            Name = artifact.GetComponent<EntityName>().value,
            ShapeId = artifact.GetComponent<ItemShape>().shape_id,
            Level = artifact.GetComponent<ItemLevel>(),
            AtomData = ArtifactBlueprintData.Clone(artifact.GetComponent<ArtifactAtomData>()),
            ControlProfile = artifact.GetComponent<ArtifactControlProfile>(),
            Appearance = ArtifactBlueprintData.Clone(artifact.GetComponent<ArtifactAppearance>()),
            AbilitySet = ArtifactBlueprintData.Clone(artifact.GetComponent<ArtifactAbilitySet>()),
            OriginKind = ArtifactBlueprintOriginKind.Archived,
            SourceActorId = sourceActor.data.id,
            SourceActorName = sourceActor.getName(),
        };

        foreach (ArtifactBlueprintExtensionAsset extension in
                 Libraries.Manager.ArtifactBlueprintExtensionLibrary.All.OrderBy(item => item.order).ThenBy(item => item.id))
        {
            if (!extension.CanCapture(artifact)) continue;
            blueprint.Extensions.Add(new ArtifactBlueprintExtensionData
            {
                ExtensionId = extension.id,
                Data = extension.Capture(artifact)?.DeepClone(),
            });
        }
        return blueprint;
    }

    public static ArtifactBlueprint FromComposeResult(ArtifactComposeResult result)
    {
        return new ArtifactBlueprint
        {
            Name = result.Name,
            ShapeId = result.Shape.id,
            Level = result.Level,
            AtomData = result.ToAtomData(),
            ControlProfile = result.ToControlProfile(),
            Appearance = ArtifactBlueprintData.Clone(result.Appearance),
            AbilitySet = ArtifactBlueprintData.Clone(result.AbilitySet),
            OriginKind = ArtifactBlueprintOriginKind.Forged,
        };
    }

    public static Entity Materialize(ArtifactBlueprint blueprint, string creatorName)
    {
        ArtifactShapeAsset shape = (ArtifactShapeAsset)ModClass.L.ItemShapeLibrary.get(blueprint.ShapeId);
        ArtifactAbilitySet abilitySet = ArtifactBlueprintData.Clone(blueprint.AbilitySet);
        Entity artifact = SpecialItemUtils
            .StartBuild(shape, World.world.getCurWorldTime(), creatorName)
            .AddComponent(new Artifact())
            .AddComponent(blueprint.Level)
            .AddComponent(new EntityName(blueprint.Name))
            .AddComponent(ArtifactBlueprintData.Clone(blueprint.AtomData))
            .AddComponent(blueprint.ControlProfile)
            .AddComponent(abilitySet)
            .AddComponent(ArtifactAbilityRuntime.CreateInitial(abilitySet))
            .AddComponent(ArtifactBlueprintData.Clone(blueprint.Appearance))
            .Build();

        foreach (ArtifactBlueprintExtensionData data in blueprint.Extensions
                     .OrderBy(item => Libraries.Manager.ArtifactBlueprintExtensionLibrary.get(item.ExtensionId).order)
                     .ThenBy(item => item.ExtensionId))
        {
            ArtifactBlueprintExtensionAsset extension =
                Libraries.Manager.ArtifactBlueprintExtensionLibrary.get(data.ExtensionId);
            extension.Restore(artifact, data.Data?.DeepClone());
        }
        return artifact;
    }
}
