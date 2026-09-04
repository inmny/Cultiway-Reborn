using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>推进元神节点与无身命魂沿锚点连接的已有迁移，并引导驻留节点归返。</summary>
public sealed class YuanshenAnchorTransitSystem : BaseSystem, IWorldStateClearable
{
    /// <summary>本轮需要从设施锚点开始归返引导的节点。</summary>
    private readonly List<NodeRequest> rootTransitStarts = new();

    /// <summary>本轮到期的锚点迁移。</summary>
    private readonly List<TransitRequest> completedTransits = new();

    /// <summary>本轮受击或断路的锚点迁移。</summary>
    private readonly List<TransitRequest> interruptedTransits = new();

    /// <summary>本轮到期的无身命魂锚点迁移。</summary>
    private readonly List<BodilessTransitRequest> completedBodilessTransits = new();

    /// <summary>本轮受击、失效或状态改变的无身命魂锚点迁移。</summary>
    private readonly List<BodilessTransitRequest> interruptedBodilessTransits = new();

    /// <summary>所属人物已经失效的节点。</summary>
    private readonly List<Entity> invalidNodes = new();

    /// <summary>先收集迁移与驻留状态，再在查询外提交完成、中断和归返引导。</summary>
    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();
        rootTransitStarts.Clear();
        completedTransits.Clear();
        interruptedTransits.Clear();
        completedBodilessTransits.Clear();
        interruptedBodilessTransits.Clear();
        invalidNodes.Clear();
        double now = World.world?.getCurWorldTime() ?? 0d;

        CollectNodeTransits(now);
        CollectBodilessTransits(now);
        CollectResidenceReturns();

        for (var i = 0; i < rootTransitStarts.Count; i++)
        {
            NodeRequest request = rootTransitStarts[i];
            if (!YuanshenAdvancedNodeService.TryStartRootReturn(request.Actor, request.Node))
                YuanshenAdvancedNodeService.Disperse(request.Actor, request.Node, 0.25f);
        }
        for (var i = 0; i < interruptedTransits.Count; i++)
        {
            TransitRequest request = interruptedTransits[i];
            YuanshenAdvancedNodeService.InterruptTransit(request.Actor, request.Node, request.Transit);
        }
        for (var i = 0; i < completedTransits.Count; i++)
        {
            TransitRequest request = completedTransits[i];
            if (!YuanshenAdvancedNodeService.CompleteTransit(request.Actor, request.Node, request.Transit))
                YuanshenAdvancedNodeService.InterruptTransit(request.Actor, request.Node, request.Transit);
        }
        for (var i = 0; i < interruptedBodilessTransits.Count; i++)
        {
            BodilessTransitRequest request = interruptedBodilessTransits[i];
            YuanshenAdvancedNodeService.InterruptBodilessTransit(request.Actor, request.Transit);
        }
        for (var i = 0; i < completedBodilessTransits.Count; i++)
        {
            BodilessTransitRequest request = completedBodilessTransits[i];
            if (!YuanshenAdvancedNodeService.CompleteBodilessTransit(request.Actor, request.Transit))
                YuanshenAdvancedNodeService.InterruptBodilessTransit(request.Actor, request.Transit);
        }
        for (var i = 0; i < invalidNodes.Count; i++)
            YuanshenTravelService.RecycleInvalidNode(invalidNodes[i]);
    }

    /// <summary>世界切换时丢弃尚未提交的帧内请求。</summary>
    void IWorldStateClearable.ClearWorldState()
    {
        rootTransitStarts.Clear();
        completedTransits.Clear();
        interruptedTransits.Clear();
        completedBodilessTransits.Clear();
        interruptedBodilessTransits.Clear();
        invalidNodes.Clear();
    }

    /// <summary>推进全部角色节点的已有锚点迁移，并收集驻留节点的归返引导。</summary>
    /// <param name="now">当前世界时间。</param>
    private void CollectNodeTransits(double now)
    {
        ModClass.I.W.Query<YuanshenNodeState, YuanshenAnchorTransitState>()
            .ForEachEntity((
                ref YuanshenNodeState state,
                ref YuanshenAnchorTransitState transit,
                Entity node) =>
            {
                if (node.Tags.Has<TagRecycle>()) return;
                Actor ownerBase = World.world?.units?.get(state.owner_actor_id);
                if (ownerBase == null || ownerBase.isRekt() || !ownerBase.isAlive())
                {
                    invalidNodes.Add(node);
                    return;
                }
                ActorExtend owner = ownerBase.GetExtend();
                bool sourceValid = !transit.source.IsValid ||
                                   YuanshenAnchorNetworkService.TryGetUsableAuthorized(
                                       owner, transit.source, out _, out _);
                bool destinationValid = transit.return_to_root ||
                                        YuanshenAnchorNetworkService.TryGetUsableAuthorized(
                                            owner, transit.destination, out _, out _);
                if (!sourceValid || !destinationValid ||
                    state.integrity_current + 0.001f < transit.starting_integrity)
                    interruptedTransits.Add(new TransitRequest(owner, node, transit));
                else if (transit.completes_at <= now)
                    completedTransits.Add(new TransitRequest(owner, node, transit));
            });
    }

    /// <summary>推进无身命魂本体的锚点迁移会话。</summary>
    /// <param name="now">当前世界时间。</param>
    private void CollectBodilessTransits(double now)
    {
        ModClass.I.W.Query<ActorBinder, YuanshenBodilessTransitState>().ForEachEntity((
            ref ActorBinder binder,
            ref YuanshenBodilessTransitState transit,
            Entity actorEntity) =>
        {
            Actor ownerBase = binder.Actor;
            if (ownerBase == null || ownerBase.isRekt() || !ownerBase.isAlive()) return;
            ActorExtend owner = ownerBase.GetExtend();
            ownerBase.cancelAllBeh();
            ownerBase.clearAttackTarget();
            ownerBase.clearTileTarget();
            bool sourceValid = !transit.source.IsValid ||
                               YuanshenAnchorNetworkService.TryGetUsableAuthorized(
                                   owner, transit.source, out _, out _);
            bool destinationValid = YuanshenAnchorNetworkService.TryGetUsableAuthorized(
                owner, transit.destination, out _, out _);
            if (!YuanshenLifecycleService.IsBodiless(owner) || !sourceValid || !destinationValid ||
                ownerBase.data.health + 0.001f < transit.starting_health)
                interruptedBodilessTransits.Add(new BodilessTransitRequest(owner, transit));
            else if (transit.completes_at <= now)
                completedBodilessTransits.Add(new BodilessTransitRequest(owner, transit));
        });
    }

    /// <summary>收集正在归返且驻留于锚点的节点，改为从设施锚点开始归返引导。</summary>
    private void CollectResidenceReturns()
    {
        ModClass.I.W.Query<YuanshenNodeState, YuanshenAnchorResidence>().ForEachEntity((
            ref YuanshenNodeState state,
            ref YuanshenAnchorResidence residence,
            Entity node) =>
        {
            if (node.Tags.Has<TagRecycle>() || node.HasComponent<YuanshenAnchorTransitState>() ||
                state.action != YuanshenNodeAction.Returning) return;
            Actor ownerBase = World.world?.units?.get(state.owner_actor_id);
            if (ownerBase == null || ownerBase.isRekt() || !ownerBase.isAlive())
            {
                invalidNodes.Add(node);
                return;
            }
            rootTransitStarts.Add(new NodeRequest(ownerBase.GetExtend(), node));
        });
    }

    /// <summary>节点与所属人物请求。</summary>
    private readonly struct NodeRequest
    {
        /// <summary>节点所属人物。</summary>
        public readonly ActorExtend Actor;

        /// <summary>节点实体。</summary>
        public readonly Entity Node;

        /// <summary>创建节点请求。</summary>
        public NodeRequest(ActorExtend actor, Entity node)
        {
            Actor = actor;
            Node = node;
        }
    }

    /// <summary>锚点迁移完成或中断请求。</summary>
    private readonly struct TransitRequest
    {
        /// <summary>节点所属人物。</summary>
        public readonly ActorExtend Actor;

        /// <summary>迁移节点。</summary>
        public readonly Entity Node;

        /// <summary>冻结迁移状态。</summary>
        public readonly YuanshenAnchorTransitState Transit;

        /// <summary>创建迁移请求。</summary>
        public TransitRequest(ActorExtend actor, Entity node, YuanshenAnchorTransitState transit)
        {
            Actor = actor;
            Node = node;
            Transit = transit;
        }
    }

    /// <summary>无身人物锚点迁移完成或中断请求。</summary>
    private readonly struct BodilessTransitRequest
    {
        /// <summary>保持原身份的人物。</summary>
        public readonly ActorExtend Actor;

        /// <summary>冻结的无身迁移状态。</summary>
        public readonly YuanshenBodilessTransitState Transit;

        /// <summary>创建无身迁移请求。</summary>
        /// <param name="actor">保持原身份的人物。</param>
        /// <param name="transit">查询阶段冻结的迁移状态。</param>
        public BodilessTransitRequest(ActorExtend actor, YuanshenBodilessTransitState transit)
        {
            Actor = actor;
            Transit = transit;
        }
    }
}
