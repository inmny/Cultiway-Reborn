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

/// <summary>推进高阶元神节点的到期、维持付费、预算超载、锚点反噬和明确神魂攻击。</summary>
public sealed class YuanshenAdvancedNodeSystem : BaseSystem, IWorldStateClearable
{
    /// <summary>本轮需要归返的高阶节点。</summary>
    private readonly List<NodeRequest> returns = new();

    /// <summary>本轮需要按锚点反噬消散的节点。</summary>
    private readonly List<DisperseRequest> dispersals = new();

    /// <summary>所属人物已经失效的节点。</summary>
    private readonly List<Entity> invalidNodes = new();

    /// <summary>先收集高阶节点状态，再在查询外提交归返、消散和清理。</summary>
    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();
        returns.Clear();
        dispersals.Clear();
        invalidNodes.Clear();
        float deltaTime = Mathf.Max(0f, Tick.deltaTime);
        double now = World.world?.getCurWorldTime() ?? 0d;

        ModClass.I.W.Query<YuanshenNodeState, YuanshenAdvancedNodeState, Position>()
            .ForEachEntity((
                ref YuanshenNodeState state,
                ref YuanshenAdvancedNodeState advanced,
                ref Position position,
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
                if (!YuanshenNodeCombatService.CanUseSoulAbilities(owner))
                {
                    invalidNodes.Add(node);
                    return;
                }
                if (advanced.anchor.IsValid &&
                    !YuanshenAnchorNetworkService.TryGetUsableAuthorized(
                        owner, advanced.anchor, out _, out _))
                {
                    float ratio = ResolveAnchorBacklash(state.role);
                    dispersals.Add(new DisperseRequest(owner, node, ratio));
                    return;
                }
                if (state.action == YuanshenNodeAction.Returning) return;
                if (advanced.expires_at > 0d && advanced.expires_at <= now)
                {
                    returns.Add(new NodeRequest(owner, node));
                    return;
                }
                advanced.upkeep_elapsed += deltaTime;
                if (advanced.upkeep_elapsed >= 1f)
                {
                    float elapsed = Mathf.Floor(advanced.upkeep_elapsed);
                    advanced.upkeep_elapsed -= elapsed;
                    if (!TryPayUpkeep(owner, ref advanced, elapsed))
                    {
                        returns.Add(new NodeRequest(owner, node));
                        return;
                    }
                }
                DivineSenseBudget budget = DivineSenseBudgetService.Resolve(owner);
                if (budget.ReservedLoad > budget.TotalLoadCapacity * 1.05f)
                {
                    returns.Add(new NodeRequest(owner, node));
                    return;
                }
                if (state.role is YuanshenNodeRole.DharmaForm or YuanshenNodeRole.Avatar)
                    UpdateExplicitCombat(owner, node, ref advanced, position.v2, deltaTime);
            });

        for (var i = 0; i < returns.Count; i++)
        {
            NodeRequest request = returns[i];
            if (!request.Node.IsNull && request.Node.TryGetComponent(out YuanshenNodeState state))
                YuanshenThoughtService.RequestReturn(request.Actor, new YuanshenNodeHandle(in state));
        }
        for (var i = 0; i < dispersals.Count; i++)
        {
            DisperseRequest request = dispersals[i];
            YuanshenAdvancedNodeService.Disperse(request.Actor, request.Node, request.LockRatio);
        }
        for (var i = 0; i < invalidNodes.Count; i++)
            YuanshenTravelService.RecycleInvalidNode(invalidNodes[i]);
    }

    /// <summary>世界切换时丢弃尚未提交的帧内请求。</summary>
    void IWorldStateClearable.ClearWorldState()
    {
        returns.Clear();
        dispersals.Clear();
        invalidNodes.Clear();
    }

    /// <summary>结算一枚法相或化身对明确人物目标的神魂攻击。</summary>
    private static void UpdateExplicitCombat(
        ActorExtend owner,
        Entity node,
        ref YuanshenAdvancedNodeState advanced,
        Vector2 position,
        float deltaTime)
    {
        if (advanced.target_actor_id <= 0L) return;
        Actor target = World.world?.units?.get(advanced.target_actor_id);
        if (target == null || target.isRekt() || !target.isAlive() || !owner.Base.canAttackTarget(target))
        {
            advanced.target_actor_id = 0L;
            if (node.HasComponent<YuanshenNodeTask>())
                node.GetComponent<YuanshenNodeTask>() = new YuanshenNodeTask
                {
                    kind = YuanshenNodeTaskKind.Idle,
                    point = position,
                    started_at = World.world?.getCurWorldTime() ?? 0d
                };
            return;
        }
        advanced.attack_elapsed += deltaTime;
        if (advanced.attack_elapsed < 2f ||
            Vector2.Distance(position, target.current_position) > YuanshenAdvancedNodeService.EngageRange) return;
        advanced.attack_elapsed = 0f;
        YuanshenNodeState state = node.GetComponent<YuanshenNodeState>();
        Yuanshen yuanshen = owner.GetComponent<Yuanshen>();
        float maximumSoul = Mathf.Max(1f, owner.Base.stats[WorldboxGame.BaseStats.MaxSoul.id]);
        float divineSense = Mathf.Max(0f, owner.Base.stats[nameof(WorldboxGame.BaseStats.DivineSense)]);
        float roleScale = state.role == YuanshenNodeRole.DharmaForm ? 1.4f : 0.75f;
        float pathScale = 1f;
        if (node.TryGetComponent(out YuanshenDharmaAppearance appearance))
        {
            if (appearance.sword_path) pathScale += 0.2f;
            if (appearance.body_path) pathScale += 0.08f;
            if (appearance.illusion_path) pathScale += 0.05f;
        }
        float raw = (maximumSoul * 0.025f + divineSense * 0.12f + Mathf.Max(1f, yuanshen.strength) * 1.5f)
                    * roleScale * pathScale;
        float damage = Mathf.Clamp(raw, 1f, Mathf.Max(1f, target.getMaxHealth() * 0.12f));
        SoulDamageService.Deal(owner.Base, target, damage);
        YuanshenNodeLockService.GrantLock(target, new YuanshenNodeHandle(in state));
    }

    /// <summary>按节点角色支付维持灵气，香火只降低对应显圣的一部分消耗。</summary>
    private static bool TryPayUpkeep(
        ActorExtend owner,
        ref YuanshenAdvancedNodeState advanced,
        float elapsed)
    {
        if (elapsed <= 0f || !owner.HasCultisys<Xian>()) return false;
        float costRatio = Mathf.Max(0f, advanced.upkeep_ratio) * elapsed;
        if (advanced.support_only && advanced.anchor.IsValid &&
            YuanshenAnchorNetworkService.TryConsumeIncense(advanced.anchor, elapsed * 0.25f,
                out float incense))
            costRatio *= Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(incense / (elapsed * 0.25f)));
        return WakanResourceService.TrySpendMaximumRatio(owner, costRatio);
    }

    /// <summary>按依赖角色确定物质锚点失效反噬比例。</summary>
    /// <param name="role">依赖锚点的节点角色。</param>
    /// <returns>锚点失效时锁定的节点份额比例。</returns>
    private static float ResolveAnchorBacklash(YuanshenNodeRole role)
    {
        return role switch
        {
            YuanshenNodeRole.Avatar => 0.5f,
            YuanshenNodeRole.Manifestation => 0.25f,
            _ => 0.25f
        };
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

    /// <summary>带锁伤比例的节点消散请求。</summary>
    private readonly struct DisperseRequest
    {
        /// <summary>节点所属人物。</summary>
        public readonly ActorExtend Actor;

        /// <summary>节点实体。</summary>
        public readonly Entity Node;

        /// <summary>剩余份额转为创伤的比例。</summary>
        public readonly float LockRatio;

        /// <summary>创建消散请求。</summary>
        public DisperseRequest(ActorExtend actor, Entity node, float lockRatio)
        {
            Actor = actor;
            Node = node;
            LockRatio = lockRatio;
        }
    }
}
