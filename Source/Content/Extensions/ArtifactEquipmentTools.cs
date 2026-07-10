using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Extensions;

public static class ArtifactEquipmentTools
{
    public static bool EquipArtifact(this ActorExtend actor, Entity artifact)
    {
        if (actor == null || actor.E.IsNull || artifact.IsNull || !artifact.HasComponent<Artifact>()) return false;
        if (!Carries(actor.E, artifact)) return false;

        using var previousOwners = new ListPool<Entity>(
            artifact.GetIncomingLinks<EquippedArtifactRelation>().Entities);
        foreach (Entity previousOwner in previousOwners)
        {
            previousOwner.RemoveRelation<EquippedArtifactRelation>(artifact);
        }

        actor.E.AddRelation(new EquippedArtifactRelation { artifact = artifact });
        return true;
    }

    public static bool UnequipArtifact(this ActorExtend actor, Entity artifact)
    {
        if (actor == null || actor.E.IsNull || artifact.IsNull) return false;
        if (!actor.IsArtifactEquipped(artifact)) return false;

        actor.E.RemoveRelation<EquippedArtifactRelation>(artifact);
        return true;
    }

    public static bool IsArtifactEquipped(this ActorExtend actor, Entity artifact)
    {
        if (actor == null || actor.E.IsNull || artifact.IsNull) return false;

        var relations = actor.E.GetRelations<EquippedArtifactRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            if (relations[i].artifact == artifact) return true;
        }
        return false;
    }

    public static bool HasEquippedArtifacts(this ActorExtend actor)
    {
        if (actor == null || actor.E.IsNull) return false;

        var relations = actor.E.GetRelations<EquippedArtifactRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            Entity artifact = relations[i].artifact;
            if (!artifact.IsNull && artifact.HasComponent<Artifact>()) return true;
        }
        return false;
    }

    public static IEnumerable<Entity> GetEquippedArtifacts(this ActorExtend actor)
    {
        if (actor == null || actor.E.IsNull) yield break;

        var relations = actor.E.GetRelations<EquippedArtifactRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            Entity artifact = relations[i].artifact;
            if (!artifact.IsNull && artifact.HasComponent<Artifact>())
            {
                yield return artifact;
            }
        }
    }

    private static bool Carries(Entity owner, Entity artifact)
    {
        var relations = owner.GetRelations<InventoryRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            if (relations[i].item == artifact) return true;
        }
        return false;
    }
}
