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

/// <summary>推进分念与高阶元神节点的移动、牵引和异常清理。</summary>
public sealed class YuanshenNodeMovementSystem
    : QuerySystem<YuanshenNodeState, Position>, IWorldStateClearable
{
    /// <summary>本帧抵达肉身、需要在查询结束后提交归一的节点。</summary>
    private readonly List<ReturnRequest> completedReturns = new();

    /// <summary>本帧失去合法所属人物、需要在查询结束后回收的节点。</summary>
    private readonly List<Entity> invalidNodes = new();

    /// <summary>建立只处理有效运行节点的查询。</summary>
    public YuanshenNodeMovementSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagRecycle>());
    }

    /// <summary>世界清理时丢弃尚未提交的帧内请求。</summary>
    void IWorldStateClearable.ClearWorldState()
    {
        completedReturns.Clear();
        invalidNodes.Clear();
    }

    /// <summary>先推进全部节点，再统一提交归一与回收，避免查询中改变实体结构。</summary>
    protected override void OnUpdate()
    {
        completedReturns.Clear();
        invalidNodes.Clear();
        float deltaTime = Mathf.Max(0f, Tick.deltaTime);

        Query.ForEachEntity((
            ref YuanshenNodeState state,
            ref Position position,
            Entity node) =>
        {
            Actor owner = World.world?.units?.get(state.owner_actor_id);
            if (!TryResolveOwner(owner, in state, out ActorExtend actor))
            {
                invalidNodes.Add(node);
                return;
            }

            YuanshenTravelService.UpdateTetherCondition(
                ref state.tether_condition,
                ref state.tether_interference_seconds,
                ref state.tether_last_interference_at,
                deltaTime);
            if ((owner.isJustAttacked() ||
                 !YuanshenAnchorNetworkService.IsNodeWithinTether(actor, node, position.v2)) &&
                state.tether_condition != YuanshenTetherCondition.Severed)
                state.action = YuanshenNodeAction.Returning;

            if (state.action == YuanshenNodeAction.Returning)
            {
                bool hasBackup = YuanshenArtifactAnchorService.TryResolve(actor, out _, out Vector3 backupPosition);
                if (state.tether_condition == YuanshenTetherCondition.Severed && !hasBackup) return;
                if (state.tether_condition is YuanshenTetherCondition.Obstructed or YuanshenTetherCondition.Severed &&
                    hasBackup)
                    state.move_target = backupPosition;
                else if (YuanshenTravelService.TryGetMainSoulPosition(actor, out Vector3 mainPosition))
                    state.move_target = mainPosition;
                else if (hasBackup)
                    state.move_target = backupPosition;
            }

            if (state.action is YuanshenNodeAction.Idle or YuanshenNodeAction.Broken) return;
            float speedScale = state.tether_condition switch
            {
                YuanshenTetherCondition.Fluctuating => 0.8f,
                YuanshenTetherCondition.Obstructed => 0.5f,
                _ => 1f
            };
            position.v2 = Vector2.MoveTowards(position.v2, state.move_target,
                state.move_speed * speedScale * deltaTime);
            if (Vector2.Distance(position.v2, state.move_target) > YuanshenTravelService.ReturnCompletionDistance) return;

            if (state.action == YuanshenNodeAction.Returning)
            {
                completedReturns.Add(new ReturnRequest(actor, node));
                return;
            }
            state.action = YuanshenNodeAction.Idle;
        });

        for (var i = 0; i < completedReturns.Count; i++)
        {
            ReturnRequest request = completedReturns[i];
            if (request.Node.TryGetComponent(out YuanshenNodeState _))
                YuanshenThoughtService.CompleteReturn(request.Actor, request.Node);
        }
        for (var i = 0; i < invalidNodes.Count; i++)
            YuanshenTravelService.RecycleInvalidNode(invalidNodes[i]);
    }

    /// <summary>解析节点所属人物并校验人物侧唯一节点引用。</summary>
    /// <param name="owner">按稳定编号取得的人物。</param>
    /// <param name="state">节点声明的完整稳定状态。</param>
    /// <param name="actor">返回人物扩展。</param>
    /// <returns>人物和双向引用都有效时返回真。</returns>
    private static bool TryResolveOwner(
        Actor owner,
        in YuanshenNodeState state,
        out ActorExtend actor)
    {
        actor = null;
        if (owner == null || owner.isRekt()) return false;
        actor = owner.GetExtend();
        if (!YuanshenNodeCombatService.CanUseSoulAbilities(actor) ||
            !actor.TryGetComponent(out YuanshenRuntimeState runtime) ||
            runtime.session_id != state.session_id ||
            state.owner_actor_id != owner.data.id)
        {
            actor = null;
            return false;
        }
        return true;
    }

    /// <summary>查询结束后提交的一次命魂归一请求。</summary>
    private readonly struct ReturnRequest
    {
        /// <summary>命魂所属人物。</summary>
        public readonly ActorExtend Actor;

        /// <summary>已经抵达肉身的节点。</summary>
        public readonly Entity Node;

        /// <summary>创建一条不可变归一请求。</summary>
        /// <param name="actor">命魂所属人物。</param>
        /// <param name="node">已经抵达的节点。</param>
        public ReturnRequest(ActorExtend actor, Entity node)
        {
            Actor = actor;
            Node = node;
        }
    }
}
