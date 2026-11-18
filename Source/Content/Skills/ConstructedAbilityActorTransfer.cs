using Cultiway.Content.Combat;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>接触已经确认后先检查出口，再解除旧动作并移动原人物，最后发布实际落点。</summary>
    /// <param name="running">触发入口的施放。</param><param name="node">真正接触人物的入口。</param>
    /// <param name="actor">实际人物。</param><param name="id">此次接触时的稳定身份。</param>
    /// <param name="generation">接触所属的世界。</param>
    private static void ResolveActorTransfer(RunningCast running, int node, Actor actor, long id, long generation)
    {
        if (actor.data?.id != id || !actor.isAlive() || !actor.hasHealth()) return;
        AbilityCast cast = running.Cast;
        foreach (int index in cast.Plan.ActorTransferSteps)
        {
            Vector3 foot = PhysicsFoot(actor);
            if (!cast.TryMapActor(index, node, id, foot, out Vector3 destination)) continue;
            if (!simulation.CanLandActor(actor, foot, ref destination))
            {
                cast.RejectActorTransfer(index);
                continue;
            }

            if (!ReleaseForExchange(actor, id, generation)) return;
            PublishBodyPosition(actor, destination);
            Persistence.ActorPositionSaveService.Remember(actor.GetExtend());
            NotifyActorChanged(actor);
            cast.FinishActorTransfer(index, simulation.contactTime, id, PhysicsFoot(actor));
            return;
        }
    }

    /// <summary>只检验落点附近的地面、模型和实际占位，不扫过双门之间。</summary>
    /// <param name="actor">待搬运的原人物。</param><param name="from">当前脚下。</param>
    /// <param name="destination">对应落点；成功时写入实际地面高度。</param>
    private bool CanLandActor(Actor actor, Vector3 from, ref Vector3 destination)
    {
        Vector2 point = destination;
        if ((point - (Vector2)from).sqrMagnitude < .0001f || !HasTransferGround(point)) return false;

        float height = SpatialActorPose.Height(actor), radius = height * .16f;
        destination.z = terrain?.Sample(point.x, point.y) ?? 0f;
        if (BodyLocomotion.DestinationAllowed?.Invoke(destination, height) == false) return false;
        actorIndex.Query(new Bounds(destination + Vector3.forward * (height * .5f),
            new Vector3(radius * 2f, radius * 2f, height)), nearby);
        foreach (int candidate in nearby)
        {
            ActorMotion other = actors[candidate];
            if (other.Actor == actor || other.Actor.data?.id != other.Id || !other.Actor.isAlive()) continue;
            Vector3 otherFoot = PhysicsFoot(other.Actor);
            float separation = radius + SpatialActorPose.Height(other.Actor) * .16f;
            if (otherFoot.z < destination.z + height && otherFoot.z + other.Height > destination.z &&
                ((Vector2)otherFoot - point).sqrMagnitude < separation * separation) return false;
        }
        return true;
    }

    /// <summary>双门出口禁止放进地图外、危险地块或已有建筑内。</summary>
    /// <param name="point">出口所在的连续平面点。</param>
    private static bool HasTransferGround(Vector2 point)
    {
        if (!Toolbox.inMapBorder(ref point)) return false;
        WorldTile tile = World.world.GetTileSimple((int)point.x, (int)point.y);
        return !tile.Type.block && !tile.Type.liquid && !tile.Type.lava && !tile.Type.damage_units && !tile.hasBuilding();
    }

}
