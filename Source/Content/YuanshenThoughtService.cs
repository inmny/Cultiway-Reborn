using System;
using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>普通分念的创建、任务、移动、聚焦和归一服务。</summary>
public static class YuanshenThoughtService
{
    /// <summary>普通分念固定心神份额。</summary>
    public const float DefaultShare = 15f;

    /// <summary>命魂必须保留的最低心神份额。</summary>
    public const float MinimumMainSoulShare = 40f;

    /// <summary>生成普通分念的最大灵气比例。</summary>
    private const float SpawnWakanRatio = 0.03f;

    /// <summary>从人物当前主位置分出一道固定份额的普通分念并前往明确坐标。</summary>
    /// <param name="actor">分念所属人物。</param>
    /// <param name="target">明确指定的初始移动目标。</param>
    /// <returns>层数、份额、预算和资源全部允许时返回真。</returns>
    public static bool TryCreateThought(ActorExtend actor, Vector2 target)
    {
        if (!CanCreateThought(actor, out Vector2 origin)) return false;
        ref Xian xian = ref actor.GetCultisys<Xian>();
        float maximumWakan = Mathf.Max(0f, actor.Base.stats[BaseStatses.MaxWakan.id]);
        float cost = maximumWakan * SpawnWakanRatio;
        if (xian.wakan + 0.001f < cost) return false;

        ref YuanshenRuntimeState runtime = ref actor.GetOrAddComponent<YuanshenRuntimeState>();
        EnsureSession(actor, ref runtime);
        runtime.generation = runtime.generation == int.MaxValue ? 1 : Mathf.Max(0, runtime.generation) + 1;
        int logicalId = Mathf.Max(2, runtime.next_logical_id++);
        Entity node = CreateThoughtNode(actor, runtime.session_id, logicalId, runtime.generation, DefaultShare, origin, target);
        YuanshenNodeIdentity identity = node.GetComponent<YuanshenNodeIdentity>();
        runtime.thought_nodes ??= new List<YuanshenNodeHandle>(3);
        runtime.thought_nodes.Add(new YuanshenNodeHandle(in identity));
        runtime.main_soul_share -= DefaultShare;
        WakanResourceService.Spend(actor, ref xian, cost);
        YuanshenTravelService.NotifyMindStateChanged(actor);
        return true;
    }

    /// <summary>为一枚本人节点设置前往明确地面点的任务。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="handle">需要命令的稳定节点句柄。</param>
    /// <param name="target">明确地面目标。</param>
    /// <returns>节点属于本人且目标在牵引范围内时返回真。</returns>
    public static bool TryAssignMove(ActorExtend actor, YuanshenNodeHandle handle, Vector2 target)
    {
        if (!TryResolveOwnedNode(actor, handle, out Entity node) ||
            !YuanshenTravelService.IsWithinTether(actor, target)) return false;
        ref YuanshenNodeIdentity identity = ref node.GetComponent<YuanshenNodeIdentity>();
        identity.action = YuanshenNodeAction.Moving;
        ref YuanshenNodeMotion motion = ref node.GetComponent<YuanshenNodeMotion>();
        motion.target = target;
        ref YuanshenNodeTask task = ref node.GetComponent<YuanshenNodeTask>();
        task = new YuanshenNodeTask
        {
            kind = YuanshenNodeTaskKind.Move,
            point = target,
            started_at = ResolveNow()
        };
        return true;
    }

    /// <summary>把一枚本人节点指派到已经明确选择的友方人物旁。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="handle">本人节点稳定句柄。</param>
    /// <param name="target">明确指定的友方人物。</param>
    /// <param name="duration">最长任务秒数。</param>
    /// <returns>人物关系、牵引范围和节点均有效时返回真。</returns>
    public static bool TryAssignFollow(
        ActorExtend actor,
        YuanshenNodeHandle handle,
        Actor target,
        float duration = 60f)
    {
        if (target == null || target.isRekt() || actor.Base.canAttackTarget(target) ||
            !YuanshenTravelService.IsWithinTether(actor, target.current_position) ||
            !TryResolveOwnedNode(actor, handle, out Entity node))
            return false;
        ref YuanshenNodeTask task = ref node.GetComponent<YuanshenNodeTask>();
        double now = ResolveNow();
        task = new YuanshenNodeTask
        {
            kind = YuanshenNodeTaskKind.FollowActor,
            target_object_id = target.data.id,
            point = target.current_position,
            started_at = now,
            expires_at = now + Mathf.Max(1f, duration)
        };
        return true;
    }

    /// <summary>把一枚本人节点指派到明确地点驻守。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="handle">本人节点稳定句柄。</param>
    /// <param name="point">明确驻守地点。</param>
    /// <param name="duration">最长任务秒数。</param>
    /// <returns>地点在牵引范围且节点有效时返回真。</returns>
    public static bool TryAssignGuard(
        ActorExtend actor,
        YuanshenNodeHandle handle,
        Vector2 point,
        float duration = 60f)
    {
        if (!TryAssignMove(actor, handle, point) ||
            !YuanshenNodeLockService.TryResolve(handle, out Entity node)) return false;
        ref YuanshenNodeTask task = ref node.GetComponent<YuanshenNodeTask>();
        task.kind = YuanshenNodeTaskKind.GuardPoint;
        task.expires_at = ResolveNow() + Mathf.Max(1f, duration);
        return true;
    }

    /// <summary>把一枚本人节点指派去跟踪已经锁定的元神节点。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="handle">本人节点稳定句柄。</param>
    /// <param name="target">已经锁定的目标节点句柄。</param>
    /// <param name="duration">最长任务秒数。</param>
    /// <returns>两端句柄和牵引范围均有效时返回真。</returns>
    public static bool TryAssignLockedNodeTracking(
        ActorExtend actor,
        YuanshenNodeHandle handle,
        YuanshenNodeHandle target,
        float duration = 30f)
    {
        if (!YuanshenNodeLockService.HasLock(actor.Base, target) ||
            !YuanshenNodeLockService.TryResolve(target, out Entity targetNode) ||
            !targetNode.TryGetComponent(out Position targetPosition) ||
            !YuanshenTravelService.IsWithinTether(actor, targetPosition.v2) ||
            !TryResolveOwnedNode(actor, handle, out Entity node))
            return false;
        ref YuanshenNodeTask task = ref node.GetComponent<YuanshenNodeTask>();
        double now = ResolveNow();
        task = new YuanshenNodeTask
        {
            kind = YuanshenNodeTaskKind.TrackLockedNode,
            target_node = target,
            point = targetPosition.v2,
            started_at = now,
            expires_at = now + Mathf.Max(1f, duration)
        };
        return true;
    }

    /// <summary>命令一枚普通分念返回人物当前主位置。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="handle">普通分念句柄。</param>
    /// <returns>句柄属于本人普通分念时返回真。</returns>
    public static bool RequestReturn(ActorExtend actor, YuanshenNodeHandle handle)
    {
        if (!TryResolveOwnedNode(actor, handle, out Entity node) ||
            !node.TryGetComponent(out YuanshenNodeIdentity _))
            return false;
        ref YuanshenNodeIdentity mutableIdentity = ref node.GetComponent<YuanshenNodeIdentity>();
        ref YuanshenNodeTask task = ref node.GetComponent<YuanshenNodeTask>();
        SetReturning(ref mutableIdentity, ref task);
        return true;
    }

    /// <summary>把节点身份与任务同步切换为归返状态。</summary>
    /// <param name="identity">需要修改的节点身份。</param>
    /// <param name="task">需要修改的节点任务。</param>
    internal static void SetReturning(ref YuanshenNodeIdentity identity, ref YuanshenNodeTask task)
    {
        identity.action = YuanshenNodeAction.Returning;
        task.kind = YuanshenNodeTaskKind.Return;
        task.started_at = ResolveNow();
    }

    /// <summary>命令人物当前全部普通分念归返。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <returns>至少有一道有效分念收到命令时返回真。</returns>
    public static bool RequestAllThoughtsReturn(ActorExtend actor)
    {
        if (actor == null || !actor.TryGetComponent(out YuanshenRuntimeState runtime) ||
            runtime.thought_nodes == null)
            return false;
        bool changed = false;
        for (var i = 0; i < runtime.thought_nodes.Count; i++)
            changed |= RequestReturn(actor, runtime.thought_nodes[i]);
        return changed;
    }

    /// <summary>节点抵达主位置后回收实体并把未锁定份额归还命魂。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="node">已经抵达的普通或高阶节点。</param>
    /// <returns>节点仍属于当前人物会话时返回真。</returns>
    public static bool CompleteReturn(ActorExtend actor, Entity node)
    {
        if (actor == null || node.IsNull ||
            !node.TryGetComponent(out YuanshenNodeIdentity identity) ||
            identity.owner_actor_id != actor.Base.data.id ||
            !actor.TryGetComponent(out YuanshenRuntimeState current) ||
            current.session_id != identity.session_id)
            return false;

        ref YuanshenRuntimeState runtime = ref actor.GetComponent<YuanshenRuntimeState>();
        runtime.main_soul_share += Mathf.Max(0f, identity.mind_share);
        YuanshenNodeHandle handle = new(in identity);
        ArtifactYuanshenControlService.ReleaseNodeArtifact(actor, handle);
        RemoveHandle(runtime.thought_nodes, handle);
        RemoveHandle(runtime.advanced_nodes, handle);
        if (actor.TryGetComponent(out YuanshenFocusState focus) && focus.handle == handle)
            actor.E.RemoveComponent<YuanshenFocusState>();
        YuanshenAnchorNetworkService.ReleaseResidence(node);
        YuanshenNodeLockService.UnregisterNode(node);
        if (!node.Tags.Has<TagRecycle>()) node.AddTag<TagRecycle>();
        YuanshenTravelService.NotifyMindStateChanged(actor);
        return true;
    }

    /// <summary>玩家将后续节点命令和允许的技能起点聚焦到一枚本人节点。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="handle">需要聚焦的稳定句柄。</param>
    /// <returns>句柄仍属于本人时返回真。</returns>
    public static bool TryFocus(ActorExtend actor, YuanshenNodeHandle handle)
    {
        if (!TryResolveOwnedNode(actor, handle, out _)) return false;
        actor.GetOrAddComponent<YuanshenFocusState>() = new YuanshenFocusState { handle = handle };
        return true;
    }

    /// <summary>读取当前有效聚焦节点和位置，失效时自动清除聚焦。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="handle">返回聚焦句柄。</param>
    /// <param name="position">返回聚焦位置。</param>
    /// <returns>聚焦仍有效时返回真。</returns>
    public static bool TryGetFocused(
        ActorExtend actor,
        out YuanshenNodeHandle handle,
        out Vector2 position)
    {
        handle = default;
        position = default;
        if (actor == null || !actor.TryGetComponent(out YuanshenFocusState focus)) return false;
        if (!TryResolveOwnedNode(actor, focus.handle, out Entity node) ||
            !node.TryGetComponent(out Position nodePosition))
        {
            actor.E.RemoveComponent<YuanshenFocusState>();
            return false;
        }
        handle = focus.handle;
        position = nodePosition.v2;
        return true;
    }

    /// <summary>在玩家明确点击点附近选择一枚本人节点，不用于自动发现。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="point">玩家明确点击位置。</param>
    /// <param name="radius">点击命中半径。</param>
    /// <param name="handle">返回距离最近的本人节点。</param>
    /// <returns>点击实际命中本人节点时返回真。</returns>
    public static bool TryGetOwnedAtPoint(
        ActorExtend actor,
        Vector2 point,
        float radius,
        out YuanshenNodeHandle handle)
    {
        handle = default;
        if (actor == null || !actor.TryGetComponent(out YuanshenRuntimeState runtime)) return false;
        YuanshenNodeHandle best = default;
        float bestDistance = Mathf.Max(0f, radius);
        bool found = false;
        ConsiderHandles(runtime.thought_nodes);
        ConsiderHandles(runtime.advanced_nodes);
        handle = best;
        return found;

        void ConsiderHandles(List<YuanshenNodeHandle> handles)
        {
            if (handles == null) return;
            for (var i = 0; i < handles.Count; i++)
            {
                if (!YuanshenNodeLockService.TryResolve(handles[i], out Entity node)) continue;
                ConsiderNode(node);
            }
        }

        void ConsiderNode(Entity node)
        {
            if (node.IsNull || node.Tags.Has<TagRecycle>() ||
                !node.TryGetComponent(out YuanshenNodeIdentity identity) ||
                identity.owner_actor_id != actor.Base.data.id ||
                !node.TryGetComponent(out Position position)) return;
            float distance = Vector2.Distance(point, position.v2);
            if (distance > bestDistance) return;
            YuanshenNodeHandle candidate = new(in identity);
            if (found && Mathf.Approximately(distance, bestDistance) &&
                candidate.LogicalId >= best.LogicalId) return;
            found = true;
            bestDistance = distance;
            best = candidate;
        }
    }

    /// <summary>统计人物当前仍有效的普通分念数量。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <returns>有效普通分念数。</returns>
    public static int CountThoughts(ActorExtend actor)
    {
        if (actor == null || !actor.TryGetComponent(out YuanshenRuntimeState runtime) ||
            runtime.thought_nodes == null)
            return 0;
        int count = 0;
        for (var i = 0; i < runtime.thought_nodes.Count; i++)
            if (YuanshenNodeLockService.TryResolve(runtime.thought_nodes[i], out _)) count++;
        return count;
    }

    /// <summary>判断人物层数、份额、槽位、灵气和牵引能否创建固定份额分念。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="origin">返回当前主位置。</param>
    /// <returns>全部门槛满足时返回真。</returns>
    private static bool CanCreateThought(ActorExtend actor, out Vector2 origin)
    {
        origin = default;
        if (actor == null || actor.Base == null || actor.Base.isRekt() || !actor.Base.isAlive() ||
            !actor.TryGetComponent(out Yuanshen yuanshen) || yuanshen.stage < 3 ||
            actor.HasComponent<YuanshenReconstructionState>() || actor.HasComponent<YuanshenPossessionState>() ||
            actor.HasComponent<YuanshenBodilessTransitState>() ||
            !actor.HasCultisys<Xian>() ||
            !YuanshenTravelService.TryGetMainSoulPosition(actor, out Vector3 origin3))
            return false;
        int stageLimit = yuanshen.stage >= 8 ? 3 : yuanshen.stage >= 5 ? 2 : 1;
        if (CountThoughts(actor) >= stageLimit) return false;
        ref YuanshenRuntimeState runtime = ref actor.GetOrAddComponent<YuanshenRuntimeState>();
        float availableMainSoul = runtime.main_soul_share > 0f
            ? runtime.main_soul_share
            : runtime.AvailableShare;
        if (availableMainSoul - DefaultShare < MinimumMainSoulShare) return false;
        DivineSenseBudget budget = DivineSenseBudgetService.Resolve(actor);
        if (budget.AvailableArtifactThreads <= 0 || budget.AutomaticPreparedLimit < budget.TotalLoadCapacity * 0.1f)
            return false;
        origin = origin3;
        return true;
    }

    /// <summary>创建与命魂共用稳定身份、完整度、移动和渲染基础的普通分念。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="sessionId">当前活动会话编号。</param>
    /// <param name="logicalId">会话内逻辑编号。</param>
    /// <param name="generation">节点生成代次。</param>
    /// <param name="share">节点心神份额。</param>
    /// <param name="origin">生成位置。</param>
    /// <param name="target">初始目标。</param>
    /// <returns>新建真实地图实体。</returns>
    private static Entity CreateThoughtNode(
        ActorExtend actor,
        long sessionId,
        int logicalId,
        int generation,
        float share,
        Vector2 origin,
        Vector2 target)
    {
        Sprite sprite = SpriteTextureLoader.getSprite("cultiway/special_effects/aura/yuanying_indicator") ??
                        SpriteTextureLoader.getSprite("cultiway/icons/artifact_atoms/spirit_awakening_script");
        if (sprite == null) throw new InvalidOperationException("缺少普通分念可用贴图。");
        float integrity = YuanshenTravelService.ResolveIntegrityMaximum(actor, share);
        Entity node = ModClass.I.W.CreateEntity(
            new YuanshenNodeIdentity
            {
                owner_actor_id = actor.Base.data.id,
                session_id = sessionId,
                logical_id = logicalId,
                generation = generation,
                role = YuanshenNodeRole.Thought,
                mind_share = share,
                action = YuanshenNodeAction.Moving
            },
            new YuanshenNodeMotion { target = target, speed = YuanshenTravelService.NodeMoveSpeed * 0.9f },
            new YuanshenNodeTask
            {
                kind = YuanshenNodeTaskKind.Move,
                point = target,
                started_at = ResolveNow()
            },
            new YuanshenNodeIntegrity
            {
                maximum = integrity,
                current = integrity,
                allocated_share = share,
                locked_share = 0f
            },
            new YuanshenTetherState { condition = YuanshenTetherCondition.Stable },
            new Position(origin.x, origin.y, 0.3f),
            new Scale(0.24f),
            new AnimData { frames = [sprite] },
            new AnimBindRenderer(),
            new AnimTint(new Color(0.62f, 0.9f, 1f, 0.65f)));
        YuanshenNodeLockService.RegisterNode(node);
        return node;
    }

    /// <summary>没有命魂出窍时也为分念创建稳定人物会话。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="runtime">人物元神运行状态。</param>
    internal static void EnsureSession(ActorExtend actor, ref YuanshenRuntimeState runtime)
    {
        if (runtime.session_id != 0L) return;
        runtime.generation = Mathf.Max(1, runtime.generation);
        runtime.session_id = unchecked(actor.Base.data.id * 2147483647L + runtime.generation);
        runtime.next_logical_id = 2;
        if (runtime.main_soul_share <= 0f) runtime.main_soul_share = runtime.AvailableShare;
    }

    /// <summary>解析并校验一枚属于当前人物的节点。</summary>
    /// <param name="actor">预期所属人物。</param>
    /// <param name="handle">节点稳定句柄。</param>
    /// <param name="node">返回当前实体。</param>
    /// <returns>节点有效且属于当前人物会话时返回真。</returns>
    private static bool TryResolveOwnedNode(ActorExtend actor, YuanshenNodeHandle handle, out Entity node)
    {
        node = default;
        return actor != null && handle.OwnerActorId == actor.Base.data.id &&
               YuanshenNodeLockService.TryResolve(handle, out node);
    }

    /// <summary>从运行时列表删除一枚稳定句柄。</summary>
    /// <param name="handles">需要修改的句柄列表。</param>
    /// <param name="handle">待删除句柄。</param>
    private static void RemoveHandle(List<YuanshenNodeHandle> handles, YuanshenNodeHandle handle)
    {
        if (handles == null) return;
        for (var i = handles.Count - 1; i >= 0; i--)
            if (handles[i] == handle) handles.RemoveAt(i);
    }

    /// <summary>取得当前世界时间。</summary>
    /// <returns>没有世界时返回零。</returns>
    private static double ResolveNow()
    {
        return World.world?.getCurWorldTime() ?? 0d;
    }
}
