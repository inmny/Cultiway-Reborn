using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Patch;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Content;

/// <summary>在载具法器离开库存前终止对应飞行，避免留下无来源的载具关系。</summary>
public sealed class ArtifactVehicleInventoryIntegration : ICanInit
{
    public void Init()
    {
        InventoryLifecycle.RegisterBeforeItemAdded(StopVehicleBeforeTransfer);
        InventoryLifecycle.RegisterBeforeItemExtracted(StopVehicleBeforeTransfer);
    }

    private static void StopVehicleBeforeTransfer(IHasInventory _, Entity item)
    {
        using ListPool<Entity> owners = new(item.GetIncomingLinks<ArtifactVehicleRelation>().Entities);
        for (int i = 0; i < owners.Count; i++)
        {
            Entity owner = owners[i];
            if (!owner.TryGetComponent(out ActorBinder binder)) continue;
            PatchAboutFly.StopCultiwayFlight(binder.Actor, false);
        }
    }
}
