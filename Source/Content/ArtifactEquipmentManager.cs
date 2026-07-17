using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Content;

public class ArtifactEquipmentManager : ICanInit
{
    public void Init()
    {
        InventoryLifecycle.RegisterBeforeItemAdded(RemoveEquipmentBeforeTransfer);
        InventoryLifecycle.RegisterAfterItemAdded(MarkArtifactCarrier);
        InventoryLifecycle.RegisterBeforeItemExtracted(RemoveEquipmentBeforeExtraction);
        InventoryLifecycle.RegisterAfterItemExtracted(ClearArtifactCarrier);
    }

    private static void RemoveEquipmentBeforeTransfer(IHasInventory _, Entity item)
    {
        using ListPool<Entity> owners = new(item.GetIncomingLinks<EquippedArtifactRelation>().Entities);
        foreach (Entity owner in owners)
        {
            owner.RemoveRelation<EquippedArtifactRelation>(item);
            owner.GetComponent<ActorBinder>().AE.MarkSemanticProfileDirty();
        }
    }

    private static void MarkArtifactCarrier(IHasInventory inventory, Entity item)
    {
        if (inventory is not ActorExtend actor || !item.HasComponent<Artifact>()) return;
        if (!actor.E.HasComponent<ArtifactLoadoutState>())
        {
            actor.E.AddComponent(new ArtifactLoadoutState());
        }
    }

    private static void RemoveEquipmentBeforeExtraction(IHasInventory inventory, Entity item)
    {
        if (inventory is ActorExtend actor)
        {
            actor.E.RemoveRelation<EquippedArtifactRelation>(item);
            actor.MarkSemanticProfileDirty();
        }
    }

    private static void ClearArtifactCarrier(IHasInventory inventory, Entity _)
    {
        if (inventory is not ActorExtend actor || actor.HasItem<Artifact>()) return;
        if (actor.E.HasComponent<ArtifactLoadoutState>())
        {
            actor.E.RemoveComponent<ArtifactLoadoutState>();
        }
    }
}
