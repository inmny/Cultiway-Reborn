using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>将真实乘员实体同步到载具座位，并清理由死亡、落地或能力失效造成的残留关系。</summary>
public sealed class ArtifactVehiclePassengerSystem : QuerySystem<ActorBinder, ArtifactVehiclePassenger>
{
    private readonly List<Entity> disembark = new();

    protected override void OnUpdate()
    {
        disembark.Clear();
        Query.ForEachEntity((ref ActorBinder binder, ref ArtifactVehiclePassenger passenger, Entity entity) =>
        {
            Actor actor = binder.Actor;
            if (actor == null || !actor.isAlive() || !passenger.driver.IsAvailable() ||
                !passenger.driver.TryGetComponent(out ActorBinder driverBinder))
            {
                disembark.Add(entity);
                return;
            }

            Actor driver = driverBinder.Actor;
            if (driver == null || !driver.isAlive() ||
                !driver.data.hasFlag(ContentActorDataKeys.IsFlying_flag) ||
                driver.GetExtend().E.GetRelations<ArtifactVehicleRelation>().Length == 0)
            {
                disembark.Add(entity);
                return;
            }

            ArtifactVehicleService.UpdatePassengerPose(actor, driver, passenger.seat_index);
        });

        for (int i = 0; i < disembark.Count; i++) ArtifactVehicleService.Disembark(disembark[i]);
    }
}
