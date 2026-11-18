using System.Collections.Generic;
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
            float wait = cast.ActorMapping(index).Wait;
            if (wait > 0f)
            {
                if (!ActorTransferSourceLive(running) || !cast.CanTransferActor(index, node, id, foot, waitForExit: true)) continue;
                int queued = 0;
                if (running.ActorTransferHolds != null)
                    foreach (ActorTransferHold hold in running.ActorTransferHolds)
                        if (hold.Step == index) queued++;
                if (queued >= cast.ActorTransferSlots(index)) continue;
                if (queued == 0 && cast.TryMapActor(index, node, id, foot, out Vector3 ready) && simulation.CanLandActor(actor, foot, ref ready))
                {
                    ArriveActorTransfer(running, index, actor, id, ready, simulation.contactTime, generation);
                    return;
                }
                if (TryHoldActorTransfer(running, index, node, actor, id, foot, simulation.contactTime + wait)) return;
                continue;
            }

            if (!cast.TryMapActor(index, node, id, foot, out Vector3 destination)) continue;
            if (running.WorldScope?.AllowsTravelArrival?.Invoke(actor, destination) == false ||
                !simulation.CanLandActor(actor, foot, ref destination))
            {
                cast.RejectActorTransfer(index);
                continue;
            }

            ArriveActorTransfer(running, index, actor, id, destination, simulation.contactTime, generation);
            return;
        }
    }

    /// <summary>实际落点通过检查之后，才更新原人物和公开的步骤结果。</summary>
    /// <param name="running">本次施放。</param><param name="index">转移步骤。</param>
    /// <param name="actor">原人物。</param><param name="id">原编号。</param><param name="destination">安全落点。</param>
    /// <param name="clock">确认时点。</param><param name="generation">所属世界。</param>
    private static void ArriveActorTransfer(RunningCast running, int index, Actor actor, long id, Vector3 destination, double clock, long generation)
    {
        bool keepTask = running.WorldScope?.AllowsTravelArrival != null;
        if (!ReleaseForExchange(actor, id, generation, keepTask) || running.Replaced || running.Cast.Finished || !actor.isAlive() || !actor.hasHealth()) return;
        PublishBodyPosition(actor, destination);
        Persistence.ActorPositionSaveService.Remember(actor.GetExtend());
        NotifyActorChanged(actor);
        running.Cast.FinishActorTransfer(index, clock, id, PhysicsFoot(actor));
    }

    /// <summary>只接管站稳且未被别的动作占用的原人物；不取得高度或受力控制。</summary>
    /// <param name="running">本次施放。</param><param name="step">转移步骤。</param><param name="node">实际入口。</param>
    /// <param name="actor">原人物。</param><param name="id">原编号。</param><param name="foot">当前脚下。</param>
    /// <param name="deadline">最终释放时点。</param>
    private static bool TryHoldActorTransfer(RunningCast running, int step, int node, Actor actor, long id, Vector3 foot, double deadline)
    {
        if (actor.position_height > 0f || actor.under_forces || actor.isFlying() || running.StagedActors?.Contains((step, id)) == true) return false;
        BodyControlLease lease = BodyControlState.TryAcquire(id, BodyOccupancy.Stance);
        if (lease == null) return false;
        (running.StagedActors ??= new()).Add((step, id));
        (running.ActorTransferHolds ??= new()).Add(new(step, node, actor, id, foot, deadline, lease));
        actor.cancelAllBeh();
        actor.stopMovement();
        return true;
    }

    /// <summary>原来源失效时，即使独立形体还存在，也不能继续暂存原人物。</summary>
    /// <param name="running">本次施放。</param>
    private static bool ActorTransferSourceLive(RunningCast running) =>
        running.InitialOwner.WorldSource || running.InitialOwner.IsLive && running.SpatialSourceLive;

    /// <summary>只在世界确认中移交。超时、关门、人物脱离或失去来源只归还本次控制。</summary>
    /// <param name="running">本次施放。</param><param name="clock">当前确认时点。</param><param name="generation">所属世界。</param>
    private void ConfirmActorTransferHolds(RunningCast running, double clock, long generation)
    {
        List<ActorTransferHold> holds = running.ActorTransferHolds;
        if (running.Replaced || running.Cast.Finished || !ActorTransferSourceLive(running))
        {
            ReleaseActorTransferHolds(running);
            return;
        }
        for (int i = 0; i < holds.Count && generation == worldGeneration;)
        {
            ActorTransferHold hold = holds[i];
            Actor actor = hold.Actor;
            Vector3 destination = default;
            bool valid = clock < hold.Deadline && hold.Lease.IsHeld && actor.data?.id == hold.Id && actor.isAlive() &&
                         actor.hasHealth() && !actor.under_forces && !actor.isFlying() && actor.position_height <= 0f &&
                         (actor.current_position - (Vector2)hold.Foot).sqrMagnitude <= .0001f;
            Vector3 foot = valid ? PhysicsFoot(actor) : default;
            valid = valid && running.Cast.CanTransferActor(hold.Step, hold.Node, hold.Id, foot, waitForExit: true);
            bool arrived = valid && running.Cast.TryMapActor(hold.Step, hold.Node, hold.Id, foot, out destination) &&
                           CanLandActor(actor, foot, ref destination);
            if (valid && !arrived) { i++; continue; }
            holds.RemoveAt(i);
            hold.Lease.Dispose();
            if (arrived) ArriveActorTransfer(running, hold.Step, actor, hold.Id, destination, clock, generation);
        }
    }

    /// <summary>移除持有凭据，不拉回人物，也不触碰其他动作的控制。</summary>
    /// <param name="running">结束的施放。</param>
    private static void ReleaseActorTransferHolds(RunningCast running)
    {
        if (running.ActorTransferHolds == null) return;
        foreach (ActorTransferHold hold in running.ActorTransferHolds) hold.Lease.Dispose();
        running.ActorTransferHolds.Clear();
    }

    /// <summary>主线程保留原人物、原位置与控制凭据，数值预测不改写这条记录。</summary>
    private readonly struct ActorTransferHold
    {
        internal readonly int Step, Node;
        internal readonly Actor Actor;
        internal readonly long Id;
        internal readonly Vector3 Foot;
        internal readonly double Deadline;
        internal readonly BodyControlLease Lease;

        /// <summary>保存已经发生的真实入口接触。</summary>
        /// <param name="step">转移步骤。</param><param name="node">实际入口。</param>
        /// <param name="actor">原人物。</param><param name="id">原编号。</param><param name="foot">入队脚下位置。</param>
        /// <param name="deadline">最终释放时点。</param><param name="lease">本次占用。</param>
        internal ActorTransferHold(int step, int node, Actor actor, long id, Vector3 foot, double deadline, BodyControlLease lease)
        {
            Step = step; Node = node; Actor = actor; Id = id; Foot = foot; Deadline = deadline; Lease = lease;
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
        Vector3 landing = destination;
        bool Occupied(Actor other, long id)
        {
            if (other == actor || other.data?.id != id || !other.isAlive()) return false;
            Vector3 otherFoot = PhysicsFoot(other);
            float otherHeight = SpatialActorPose.Height(other), separation = radius + otherHeight * .16f;
            return otherFoot.z < landing.z + height && otherFoot.z + otherHeight > landing.z &&
                   ((Vector2)otherFoot - point).sqrMagnitude < separation * separation;
        }
        foreach (int candidate in nearby)
            if (Occupied(actors[candidate].Actor, actors[candidate].Id)) return false;
        // 同一确认时点先通过的人已在新位置，索引稍后才统一刷新。
        foreach (var change in changedActors)
            if (Occupied(change.Value, change.Key)) return false;
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
