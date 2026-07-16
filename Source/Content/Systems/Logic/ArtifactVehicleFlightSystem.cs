using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Patch;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using strings;

namespace Cultiway.Content.Systems.Logic;

/// <summary>刷新飞行中的法器载具，并补足没有修仙组件的载具驾驭者的停飞判定。</summary>
public sealed class ArtifactVehicleFlightSystem : QuerySystem<ActorBinder, ArtifactLoadoutState>
{
    private readonly List<Actor> refresh = new();
    private readonly List<Actor> stop = new();

    protected override void OnUpdate()
    {
        refresh.Clear();
        stop.Clear();
        Query.ForEachEntity((ref ActorBinder binder, ref ArtifactLoadoutState _, Entity _) =>
        {
            Actor actor = binder.Actor;
            if (actor == null || !actor.isAlive() ||
                !actor.data.hasFlag(ContentActorDataKeys.IsFlying_flag)) return;

            refresh.Add(actor);
            if (!ArtifactVehicleService.TryResolve(actor, out _))
            {
                if (!PatchAboutFly.CanStartCultiwayFlight(actor)) stop.Add(actor);
                return;
            }
            if (actor.data.hasFlag(ContentActorDataKeys.ManualControlledFlight_flag))
            {
                if (!actor.hasStatus(S_Status.possessed) || !ControllableUnit.isControllingUnit(actor))
                {
                    stop.Add(actor);
                }
                return;
            }

            if (!actor.has_attack_target && !actor.isJustAttacked() &&
                !actor.is_moving && !actor.isFollowingLocalPath())
            {
                stop.Add(actor);
            }
        });

        for (int i = 0; i < refresh.Count; i++) ArtifactVehicleService.SyncFlightRelation(refresh[i]);
        for (int i = 0; i < stop.Count; i++)
        {
            stop[i].data.removeFlag(ContentActorDataKeys.ManualControlledFlight_flag);
            PatchAboutFly.StopCultiwayFlight(stop[i], false);
        }
    }
}
