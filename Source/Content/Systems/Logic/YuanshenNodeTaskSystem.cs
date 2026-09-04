using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>推进元神节点已经明确指派的单一任务，不寻找未知世界对象。</summary>
public sealed class YuanshenNodeTaskSystem
    : QuerySystem<YuanshenNodeIdentity, YuanshenNodeTask, YuanshenNodeMotion, Position>
{
    /// <summary>任务目标刷新间隔。</summary>
    private const float UpdateInterval = 0.2f;

    /// <summary>建立活动节点任务查询。</summary>
    public YuanshenNodeTaskSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagRecycle>());
    }

    /// <summary>只解析任务中已经冻结的人物编号、节点句柄或法器编号。</summary>
    protected override void OnUpdate()
    {
        float deltaTime = Mathf.Max(0f, Tick.deltaTime);
        Query.ForEachEntity((
            ref YuanshenNodeIdentity identity,
            ref YuanshenNodeTask task,
            ref YuanshenNodeMotion motion,
            ref Position position,
            Entity node) =>
        {
            task.update_elapsed += deltaTime;
            if (task.update_elapsed < UpdateInterval) return;
            task.update_elapsed = 0f;
            Actor ownerBase = World.world?.units?.get(identity.owner_actor_id);
            if (ownerBase == null || ownerBase.isRekt()) return;
            ActorExtend owner = ownerBase.GetExtend();
            double now = World.world?.getCurWorldTime() ?? 0d;
            if (task.expires_at > 0d && task.expires_at <= now)
            {
                YuanshenThoughtService.SetReturning(ref identity, ref task);
                return;
            }

            switch (task.kind)
            {
                case YuanshenNodeTaskKind.Idle:
                    identity.action = YuanshenNodeAction.Idle;
                    break;
                case YuanshenNodeTaskKind.Move:
                case YuanshenNodeTaskKind.GuardPoint:
                    UpdatePointTask(owner, node, ref identity, ref task, ref motion, position.v2);
                    break;
                case YuanshenNodeTaskKind.FollowActor:
                    UpdateFollowTask(owner, node, ref identity, ref task, ref motion);
                    break;
                case YuanshenNodeTaskKind.TrackLockedNode:
                    UpdateTrackTask(owner, node, ref identity, ref task, ref motion);
                    break;
                case YuanshenNodeTaskKind.ControlArtifact:
                    UpdateArtifactTask(owner, node, ref identity, ref task);
                    break;
                case YuanshenNodeTaskKind.AnchorTransit:
                    identity.action = YuanshenNodeAction.Idle;
                    break;
                case YuanshenNodeTaskKind.EngageActor:
                    UpdateEngageTask(owner, node, ref identity, ref task, ref motion, position.v2);
                    break;
                case YuanshenNodeTaskKind.Return:
                    identity.action = YuanshenNodeAction.Returning;
                    break;
            }
        });
    }

    /// <summary>推进移动或守护地点任务。</summary>
    /// <param name="owner">节点所属人物。</param>
    /// <param name="node">执行任务的具体节点。</param>
    /// <param name="identity">节点身份。</param>
    /// <param name="task">当前任务。</param>
    /// <param name="motion">节点移动目标。</param>
    /// <param name="position">节点当前位置。</param>
    private static void UpdatePointTask(
        ActorExtend owner,
        Entity node,
        ref YuanshenNodeIdentity identity,
        ref YuanshenNodeTask task,
        ref YuanshenNodeMotion motion,
        Vector2 position)
    {
        if (!YuanshenAnchorNetworkService.IsNodeWithinTether(owner, node, task.point))
        {
            YuanshenThoughtService.SetReturning(ref identity, ref task);
            return;
        }
        motion.target = task.point;
        if (Vector2.Distance(position, task.point) <= YuanshenTravelService.ReturnCompletionDistance)
        {
            identity.action = YuanshenNodeAction.Idle;
            if (task.kind == YuanshenNodeTaskKind.Move) task.kind = YuanshenNodeTaskKind.Idle;
            return;
        }
        identity.action = YuanshenNodeAction.Moving;
    }

    /// <summary>只按任务中已有的人物编号更新跟随位置。</summary>
    /// <param name="owner">节点所属人物。</param>
    /// <param name="node">执行任务的具体节点。</param>
    /// <param name="identity">节点身份。</param>
    /// <param name="task">当前任务。</param>
    /// <param name="motion">节点移动目标。</param>
    private static void UpdateFollowTask(
        ActorExtend owner,
        Entity node,
        ref YuanshenNodeIdentity identity,
        ref YuanshenNodeTask task,
        ref YuanshenNodeMotion motion)
    {
        Actor target = World.world?.units?.get(task.target_object_id);
        if (target == null || target.isRekt() || !target.isAlive() || owner.Base.canAttackTarget(target) ||
            !YuanshenAnchorNetworkService.IsNodeWithinTether(owner, node, target.current_position))
        {
            YuanshenThoughtService.SetReturning(ref identity, ref task);
            return;
        }
        motion.target = target.current_position;
        identity.action = YuanshenNodeAction.Moving;
    }

    /// <summary>只解析任务中已有且仍被人物锁定的节点句柄。</summary>
    /// <param name="owner">节点所属人物。</param>
    /// <param name="node">执行任务的具体节点。</param>
    /// <param name="identity">节点身份。</param>
    /// <param name="task">当前任务。</param>
    /// <param name="motion">节点移动目标。</param>
    private static void UpdateTrackTask(
        ActorExtend owner,
        Entity node,
        ref YuanshenNodeIdentity identity,
        ref YuanshenNodeTask task,
        ref YuanshenNodeMotion motion)
    {
        if (!YuanshenNodeLockService.HasLock(owner.Base, task.target_node) ||
            !YuanshenNodeLockService.TryResolve(task.target_node, out Entity targetNode) ||
            !targetNode.TryGetComponent(out Position targetPosition) ||
            !YuanshenAnchorNetworkService.IsNodeWithinTether(owner, node, targetPosition.v2))
        {
            YuanshenThoughtService.SetReturning(ref identity, ref task);
            return;
        }
        motion.target = targetPosition.v2;
        identity.action = YuanshenNodeAction.Moving;
    }

    /// <summary>只追击任务中已经明确写入的敌方人物。</summary>
    /// <param name="owner">节点所属人物。</param>
    /// <param name="node">执行任务的具体节点。</param>
    /// <param name="identity">节点身份。</param>
    /// <param name="task">当前任务。</param>
    /// <param name="motion">节点移动目标。</param>
    /// <param name="position">节点当前位置。</param>
    private static void UpdateEngageTask(
        ActorExtend owner,
        Entity node,
        ref YuanshenNodeIdentity identity,
        ref YuanshenNodeTask task,
        ref YuanshenNodeMotion motion,
        Vector2 position)
    {
        Actor target = World.world?.units?.get(task.target_object_id);
        if (target == null || target.isRekt() || !target.isAlive() || !owner.Base.canAttackTarget(target) ||
            !YuanshenAnchorNetworkService.IsNodeWithinTether(owner, node, target.current_position))
        {
            task = new YuanshenNodeTask
            {
                kind = YuanshenNodeTaskKind.Idle,
                point = position,
                started_at = World.world?.getCurWorldTime() ?? 0d
            };
            identity.action = YuanshenNodeAction.Idle;
            return;
        }
        task.point = target.current_position;
        motion.target = target.current_position;
        identity.action = Vector2.Distance(position, target.current_position) >
                          YuanshenAdvancedNodeService.EngageRange * 0.8f
            ? YuanshenNodeAction.Moving
            : YuanshenNodeAction.Idle;
    }

    /// <summary>校验任务中已有的法器编号和远程控制组件。</summary>
    /// <param name="owner">节点所属人物。</param>
    /// <param name="node">执行任务的节点。</param>
    /// <param name="identity">节点身份。</param>
    /// <param name="task">当前任务。</param>
    private static void UpdateArtifactTask(
        ActorExtend owner,
        Entity node,
        ref YuanshenNodeIdentity identity,
        ref YuanshenNodeTask task)
    {
        Entity artifact = task.artifact_entity_id > 0
            ? ModClass.I.W.GetEntityById(task.artifact_entity_id)
            : default;
        if (artifact.IsNull || !artifact.TryGetComponent(out ArtifactYuanshenControl remote) ||
            remote.owner_actor_id != owner.Base.data.id ||
            !node.TryGetComponent(out YuanshenNodeIdentity current) ||
            remote.node != new YuanshenNodeHandle(in current))
        {
            ArtifactYuanshenControlService.CleanupInvalid(artifact);
            task = new YuanshenNodeTask
            {
                kind = YuanshenNodeTaskKind.Idle,
                started_at = World.world?.getCurWorldTime() ?? 0d
            };
        }
        identity.action = YuanshenNodeAction.Idle;
    }

}
