using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>人物级元神决策系统：按优先级统一裁决心神份额的自动使用，不寻找目标。</summary>
public sealed class YuanshenDecisionSystem : QuerySystem<ActorBinder, Yuanshen, YuanshenDecisionRuntime>
{
    /// <summary>离开人物查询后执行的本轮决策人物。</summary>
    private readonly List<ActorExtend> actors = new();

    /// <summary>复用的已锁定节点集合。</summary>
    private readonly List<YuanshenNodeHandle> locked = new(8);

    /// <summary>建立有效人物查询。</summary>
    public YuanshenDecisionSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagRecycle>());
    }

    /// <summary>约每秒一次基于显式战斗目标执行有限决策。</summary>
    protected override void OnUpdate()
    {
        actors.Clear();
        float deltaTime = Mathf.Max(0f, Tick.deltaTime);
        Query.ForEachEntity((
            ref ActorBinder binder,
            ref Yuanshen _,
            ref YuanshenDecisionRuntime runtime,
            Entity __) =>
        {
            Actor actor = binder.Actor;
            if (actor == null || actor.isRekt() || !actor.isAlive() || IsPlayerControlled(actor)) return;
            runtime.think_cooldown -= deltaTime;
            if (runtime.think_cooldown > 0f) return;
            runtime.think_cooldown = 0.8f + Mathf.Abs(actor.data.id % 5L) * 0.1f;
            actors.Add(actor.GetExtend());
        });
        for (var i = 0; i < actors.Count; i++) Think(actors[i]);
    }

    /// <summary>处理一名人物的一次元神决策。</summary>
    /// <param name="actor">拥有元神的人物。</param>
    private void Think(ActorExtend actor)
    {
        Actor owner = actor.Base;
        if (actor.HasComponent<YuanshenBodilessTransitState>()) return;
        bool recoveryTookOver = TryHandleBodyRecovery(actor);
        if (HasLiveCombatTarget(owner))
        {
            if (!recoveryTookOver) ExecuteCombatTasks(actor, owner);
            return;
        }
        if (!recoveryTookOver) TryHandleAdvancedPeace(actor);
        EndCombatTasks(actor);
    }

    /// <summary>基于人物当前明确战斗目标执行元神战斗决策。</summary>
    /// <param name="actor">拥有元神的人物。</param>
    /// <param name="owner">人物本体。</param>
    private void ExecuteCombatTasks(ActorExtend actor, Actor owner)
    {
        BaseSimObject targetObject = owner.attack_target;
        ref YuanshenDecisionRuntime decisionRuntime = ref actor.GetComponent<YuanshenDecisionRuntime>();
        decisionRuntime.combat_target_id = targetObject.isActor() ? targetObject.a.data.id : 0L;
        Vector2 targetPosition = targetObject.current_position;
        locked.Clear();
        YuanshenNodeLockService.CollectLocks(owner, locked);
        YuanshenNodeHandle hostileLock = default;
        bool hasHostileLock = false;
        for (var i = 0; i < locked.Count; i++)
        {
            Actor targetOwner = World.world?.units?.get(locked[i].OwnerActorId);
            if (targetOwner == null || targetOwner.isRekt() || !owner.canAttackTarget(targetOwner)) continue;
            hostileLock = locked[i];
            hasHostileLock = true;
            break;
        }
        if (hasHostileLock) YuanshenNodeCombatService.TryBasicStrike(actor, hostileLock);

        if (!actor.TryGetComponent(out Yuanshen yuanshen) || yuanshen.stage < 3) return;
        if (targetObject.isActor())
        {
            Actor explicitTarget = targetObject.a;
            if (yuanshen.stage >= 8 && IsHighValueTarget(owner, explicitTarget))
            {
                if (YuanshenAdvancedNodeService.CountRole(actor, YuanshenNodeRole.DharmaForm) == 0)
                    YuanshenAdvancedNodeService.TryCreateDharmaForm(
                        actor,
                        ClampToTether(owner.current_position, targetPosition),
                        explicitTarget);
                YuanshenAdvancedNodeService.TryAssignRoleEngage(
                    actor,
                    YuanshenNodeRole.DharmaForm,
                    explicitTarget);
            }
            if (yuanshen.stage >= 9)
                YuanshenAdvancedNodeService.TryAssignRoleEngage(actor, YuanshenNodeRole.Avatar, explicitTarget);
        }
        YuanshenNodeHandle thought;
        if (!TryGetFirstThought(actor, out thought))
        {
            Vector2 destination = ClampToTether(owner.current_position, targetPosition);
            if (!YuanshenThoughtService.TryCreateThought(actor, destination) ||
                !TryGetFirstThought(actor, out thought)) return;
        }
        if (hasHostileLock)
            YuanshenThoughtService.TryAssignLockedNodeTracking(actor, thought, hostileLock, 12f);
        else
            YuanshenThoughtService.TryAssignMove(actor, thought, ClampToTether(owner.current_position, targetPosition));
        if (yuanshen.stage >= 4) YuanshenThoughtService.TryFocus(actor, thought);
        if (!targetObject.isActor()) return;
        Actor actorTarget = targetObject.a;
        if (YuanshenLifecycleService.IsBodiless(actor) && IsVulnerable(actorTarget) &&
            Vector2.Distance(owner.current_position, actorTarget.current_position) <=
            YuanshenBodyRecoveryService.PossessionRange)
        {
            YuanshenBodyRecoveryService.TryStartPossession(actor, actorTarget);
        }
    }

    /// <summary>判断人物当前是否存在仍然有效的明确战斗目标。</summary>
    /// <param name="owner">人物本体。</param>
    /// <returns>战斗目标存在且仍然存活时返回真。</returns>
    private static bool HasLiveCombatTarget(Actor owner)
    {
        return owner.has_attack_target && owner.isEnemyTargetAlive() && owner.attack_target != null &&
               !owner.attack_target.isRekt();
    }

    /// <summary>在没有战斗目标时只使用本人已经建立的授权设施处理守护和化身准备。</summary>
    /// <param name="actor">拥有高阶元神的人物。</param>
    private static void TryHandleAdvancedPeace(ActorExtend actor)
    {
        if (!actor.TryGetComponent(out Yuanshen yuanshen) || yuanshen.stage < 7 ||
            YuanshenLifecycleService.IsBodiless(actor)) return;
        if (YuanshenAnchorNetworkService.TryGetRecentlyAttackedOwned(
                actor,
                2d * Cultiway.Const.TimeScales.SecPerMonth,
                out _,
                out Vector2 attackedPosition) &&
            YuanshenAdvancedNodeService.CountRole(actor, YuanshenNodeRole.Manifestation) == 0)
            YuanshenAdvancedNodeService.TryCreateManifestation(actor, attackedPosition);
        if (yuanshen.stage >= 9 && !actor.HasComponent<YuanshenAvatarPreparationState>() &&
            YuanshenAdvancedNodeService.CountRole(actor, YuanshenNodeRole.Avatar) == 0 &&
            YuanshenAnchorNetworkService.TryGetFirstOwned(actor, out _, out Vector2 anchorPosition) &&
            Vector2.Distance(actor.Base.current_position, anchorPosition) <=
            YuanshenAnchorNetworkService.PresenceRange)
            YuanshenAdvancedNodeService.TryStartAvatarPreparation(actor, anchorPosition);
    }

    /// <summary>只按明确战斗目标的境界与公开生命值判断是否值得显化法相。</summary>
    /// <param name="owner">元神原人物。</param>
    /// <param name="target">人物当前明确战斗目标。</param>
    /// <returns>目标为化神、持有元神或生命规模接近原人物时返回真。</returns>
    private static bool IsHighValueTarget(Actor owner, Actor target)
    {
        if (target == null || target.isRekt()) return false;
        ActorExtend extend = target.GetExtend();
        if (extend.TryGetComponent(out Yuanshen targetYuanshen) && targetYuanshen.formation.IsValid) return true;
        if (extend.HasCultisys<Xian>() &&
            extend.GetCultisys<Xian>().CurrLevel >= Cultiway.Content.Const.XianLevels.Huashen) return true;
        return target.getMaxHealth() >= Mathf.Max(100f, owner.getMaxHealth() * 0.8f);
    }

    /// <summary>处理无战斗目标时的本命寄托和塑体。</summary>
    /// <param name="actor">拥有元神的人物。</param>
    /// <returns>无身生存流程接管本轮决策时返回真。</returns>
    private static bool TryHandleBodyRecovery(ActorExtend actor)
    {
        if (!actor.TryGetComponent(out Yuanshen yuanshen)) return false;
        if (yuanshen.stage >= 7 && !actor.HasComponent<YuanshenArtifactAnchorState>())
            YuanshenArtifactAnchorService.TryBindNext(actor);
        if (!YuanshenLifecycleService.IsBodiless(actor)) return false;
        if (actor.HasComponent<YuanshenPossessionState>() || actor.HasComponent<YuanshenReconstructionState>())
            return true;
        if (actor.Base.has_attack_target && actor.Base.isEnemyTargetAlive()) return false;
        if (yuanshen.stage >= 9 && actor.HasComponent<YuanshenArtifactAnchorState>())
        {
            YuanshenArtifactAnchorService.TryRestAtAnchor(actor);
            if (YuanshenBodyRecoveryService.TryStartReconstruction(actor)) return true;
        }
        return true;
    }

    /// <summary>战斗结束后收回自动分念和法相。</summary>
    /// <param name="actor">拥有元神的人物。</param>
    private static void EndCombatTasks(ActorExtend actor)
    {
        if (actor.TryGetComponent(out YuanshenDecisionRuntime current) && current.combat_target_id == 0L &&
            YuanshenThoughtService.CountThoughts(actor) == 0 &&
            YuanshenAdvancedNodeService.CountRole(actor, YuanshenNodeRole.DharmaForm) == 0) return;
        ref YuanshenDecisionRuntime runtime = ref actor.GetComponent<YuanshenDecisionRuntime>();
        runtime.combat_target_id = 0L;
        YuanshenThoughtService.RequestAllThoughtsReturn(actor);
        YuanshenAdvancedNodeService.RequestRoleReturn(actor, YuanshenNodeRole.DharmaForm);
    }

    /// <summary>读取人物当前第一枚仍有效的普通分念。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="handle">返回稳定句柄。</param>
    /// <returns>至少存在一枚有效分念时返回真。</returns>
    private static bool TryGetFirstThought(ActorExtend actor, out YuanshenNodeHandle handle)
    {
        handle = default;
        if (!actor.TryGetComponent(out YuanshenRuntimeState runtime) || runtime.thought_nodes == null) return false;
        for (var i = 0; i < runtime.thought_nodes.Count; i++)
        {
            YuanshenNodeHandle candidate = runtime.thought_nodes[i];
            if (!YuanshenNodeLockService.TryResolve(candidate, out Entity node) ||
                !node.TryGetComponent(out YuanshenNodeIdentity identity) ||
                identity.role != YuanshenNodeRole.Thought) continue;
            handle = candidate;
            return true;
        }
        return false;
    }

    /// <summary>只从明确战斗目标的公开受伤与昏迷状态判断能否尝试夺舍。</summary>
    /// <param name="target">人物当前战斗目标。</param>
    /// <returns>目标生命不足三成半、昏迷或带有神魂创伤时返回真。</returns>
    private static bool IsVulnerable(Actor target)
    {
        float healthRatio = target.getMaxHealth() > 0f
            ? Mathf.Clamp01(target.getHealth() / target.getMaxHealth())
            : 0f;
        return healthRatio <= 0.35f || target.is_unconscious ||
               CombatStatusEffects.HasStatus(target, StatusEffects.SoulTrauma);
    }

    /// <summary>把明确目标裁到命魂牵引上限内。</summary>
    /// <param name="origin">人物本体位置。</param>
    /// <param name="target">人物已持有的战斗目标位置。</param>
    /// <returns>合法牵引地点。</returns>
    private static Vector2 ClampToTether(Vector2 origin, Vector2 target)
    {
        Vector2 offset = target - origin;
        float limit = YuanshenTravelService.MaximumTetherDistance * 0.9f;
        return offset.sqrMagnitude <= limit * limit ? target : origin + offset.normalized * limit;
    }

    /// <summary>判断人物当前是否直接受玩家控制。</summary>
    /// <param name="actor">待判断人物。</param>
    /// <returns>同一人物正由玩家控制时返回真。</returns>
    private static bool IsPlayerControlled(Actor actor)
    {
        return ControlledCultivatorSkillControls.TryGetControlledActor(out Actor controlled) && controlled == actor;
    }
}
