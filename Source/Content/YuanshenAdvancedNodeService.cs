using System;
using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>法相、稳定化身、显圣和锚点迁移共用的高阶元神节点入口。</summary>
public static class YuanshenAdvancedNodeService
{
    /// <summary>战斗法相最低心神份额。</summary>
    public const float DharmaShare = 50f;

    /// <summary>稳定化身最低心神份额。</summary>
    public const float AvatarShare = 30f;

    /// <summary>显圣投影心神份额。</summary>
    public const float ManifestationShare = 20f;

    /// <summary>战斗法相最长基础持续秒数。</summary>
    public const double DharmaDuration = 20d;

    /// <summary>显圣基础持续秒数。</summary>
    public const double ManifestationDuration = 12d;

    /// <summary>稳定化身载体准备时间。</summary>
    public const double AvatarPreparationDuration = Cultiway.Const.TimeScales.SecPerYear;

    /// <summary>法相启动消耗的最大灵气比例。</summary>
    private const float DharmaStartWakanRatio = 0.2f;

    /// <summary>显圣启动消耗的最大灵气比例。</summary>
    private const float ManifestationStartWakanRatio = 0.1f;

    /// <summary>节点沿锚点迁移的最小灵气比例。</summary>
    private const float TransitBaseWakanRatio = 0.02f;

    /// <summary>法相每秒最大灵气消耗比例。</summary>
    private const float DharmaUpkeepRatio = 0.01f;

    /// <summary>稳定化身每秒最大灵气消耗比例。</summary>
    private const float AvatarUpkeepRatio = 0.002f;

    /// <summary>显圣每秒最大灵气消耗比例。</summary>
    private const float ManifestationUpkeepRatio = 0.004f;

    /// <summary>高阶节点攻击距离。</summary>
    public const float EngageRange = 18f;

    /// <summary>创建一尊短时高投入战斗法相并前往明确地点。</summary>
    /// <param name="actor">原人物、资源支付者和社会后果承担者。</param>
    /// <param name="target">法相的明确初始地点。</param>
    /// <param name="combatTarget">可选的原人物明确战斗目标。</param>
    /// <returns>元神八层、份额、预算、资源和身体状态均允许时返回真。</returns>
    public static bool TryCreateDharmaForm(ActorExtend actor, Vector2 target, Actor combatTarget = null)
    {
        if (!CanCreateAdvanced(actor, YuanshenNodeRole.DharmaForm, 8, DharmaShare, true,
                out Vector2 origin) ||
            !YuanshenTravelService.IsWithinTether(actor, target) ||
            !WakanResourceService.TrySpendMaximumRatio(actor, DharmaStartWakanRatio)) return false;
        Entity node = CreateAdvancedNode(
            actor,
            YuanshenNodeRole.DharmaForm,
            DharmaShare,
            origin,
            target,
            Now + DharmaDuration,
            DharmaUpkeepRatio,
            default,
            false);
        if (node.IsNull) return false;
        if (combatTarget != null && !combatTarget.isRekt() && actor.Base.canAttackTarget(combatTarget))
            AssignEngage(node, combatTarget);
        FocusNode(actor, node);
        return true;
    }

    /// <summary>在明确授权锚点投下一道短时护持显圣。</summary>
    /// <param name="actor">原人物和资源支付者。</param>
    /// <param name="point">明确点选的授权锚点位置。</param>
    /// <returns>层数、份额、锚点容量、预算和资源满足时返回真。</returns>
    public static bool TryCreateManifestation(ActorExtend actor, Vector2 point)
    {
        if (!CanCreateAdvanced(actor, YuanshenNodeRole.Manifestation, 7, ManifestationShare, true,
                out _) ||
            !YuanshenAnchorNetworkService.TryGetAuthorizedAtPoint(actor, point,
                out YuanshenAnchorHandle anchorHandle, out Entity anchor) ||
            !anchor.TryGetComponent(out Position anchorPosition) ||
            !YuanshenAnchorNetworkService.CanAcceptLoad(anchorHandle, ManifestationShare)) return false;
        if (!WakanResourceService.TrySpendMaximumRatio(actor, ManifestationStartWakanRatio)) return false;
        Entity node = CreateAdvancedNode(
            actor,
            YuanshenNodeRole.Manifestation,
            ManifestationShare,
            anchorPosition.v2,
            anchorPosition.v2,
            Now + ManifestationDuration,
            ManifestationUpkeepRatio,
            anchorHandle,
            true);
        if (node.IsNull || !YuanshenAnchorNetworkService.TrySetResidence(node, anchorHandle))
        {
            if (!node.IsNull) Disperse(actor, node, 0f);
            return false;
        }
        FocusNode(actor, node);
        return true;
    }

    /// <summary>在明确抵达的授权锚点开始一年稳定化身载体准备。</summary>
    /// <param name="actor">原人物和资源支付者。</param>
    /// <param name="point">明确锚点位置。</param>
    /// <returns>元神九层、唯一化身、份额和锚点条件均满足时返回真。</returns>
    public static bool TryStartAvatarPreparation(ActorExtend actor, Vector2 point)
    {
        if (!CanUseAdvanced(actor, 9) || actor.HasComponent<YuanshenAvatarPreparationState>() ||
            CountRole(actor, YuanshenNodeRole.Avatar) > 0 || !HasAvailableThread(actor) ||
            !CanReserveShare(actor, AvatarShare) ||
            !YuanshenAnchorNetworkService.TryGetAuthorizedAtPoint(actor, point,
                out YuanshenAnchorHandle handle, out Entity anchor) ||
            !anchor.TryGetComponent(out Position position) ||
            !YuanshenAnchorNetworkService.HasPresenceNear(actor, position.v2) ||
            !YuanshenAnchorNetworkService.CanAcceptLoad(handle, AvatarShare) ||
            !actor.HasCultisys<Xian>()) return false;
        float maximumWakan = Mathf.Max(1f, actor.Base.stats[BaseStatses.MaxWakan.id]);
        actor.E.AddComponent(new YuanshenAvatarPreparationState
        {
            anchor = handle,
            last_updated_at = Now,
            last_interrupted_at = Now - Cultiway.Const.TimeScales.SecPerMonth,
            required_wakan = maximumWakan
        });
        return true;
    }

    /// <summary>取消尚未完成的稳定化身载体准备，已支付资源不返还。</summary>
    /// <param name="actor">正在准备载体的人物。</param>
    /// <returns>原本存在准备状态时返回真。</returns>
    public static bool CancelAvatarPreparation(ActorExtend actor)
    {
        if (actor == null || !actor.HasComponent<YuanshenAvatarPreparationState>()) return false;
        actor.E.RemoveComponent<YuanshenAvatarPreparationState>();
        return true;
    }

    /// <summary>完成已经推进到期的化身载体，并生成唯一稳定化身节点。</summary>
    /// <param name="actor">原人物。</param>
    /// <param name="preparation">查询外冻结的完成状态。</param>
    /// <returns>锚点、份额和唯一性仍有效并创建化身时返回真。</returns>
    public static bool CompleteAvatarPreparation(
        ActorExtend actor,
        in YuanshenAvatarPreparationState preparation)
    {
        if (!CanUseAdvanced(actor, 9) || !actor.HasComponent<YuanshenAvatarPreparationState>() ||
            CountRole(actor, YuanshenNodeRole.Avatar) > 0 ||
            !CanReserveShare(actor, AvatarShare) ||
            !YuanshenAnchorNetworkService.TryGetUsableAuthorized(actor, preparation.anchor,
                out _, out Vector2 position) ||
            !YuanshenAnchorNetworkService.CanAcceptLoad(preparation.anchor, AvatarShare)) return false;
        Entity node = CreateAdvancedNode(
            actor,
            YuanshenNodeRole.Avatar,
            AvatarShare,
            position,
            position,
            0d,
            AvatarUpkeepRatio,
            preparation.anchor,
            false);
        if (node.IsNull || !YuanshenAnchorNetworkService.TrySetResidence(node, preparation.anchor))
        {
            if (!node.IsNull) Disperse(actor, node, 0f);
            return false;
        }
        actor.E.RemoveComponent<YuanshenAvatarPreparationState>();
        FocusNode(actor, node);
        return true;
    }

    /// <summary>让指定角色的第一枚有效高阶节点攻击明确敌人。</summary>
    /// <param name="actor">节点所属原人物。</param>
    /// <param name="role">法相或化身角色。</param>
    /// <param name="target">原人物当前明确敌人。</param>
    /// <returns>找到合法节点并写入目标时返回真。</returns>
    public static bool TryAssignRoleEngage(ActorExtend actor, YuanshenNodeRole role, Actor target)
    {
        if (actor == null || target == null || target.isRekt() || !target.isAlive() ||
            !actor.Base.canAttackTarget(target) ||
            !actor.TryGetComponent(out YuanshenRuntimeState runtime) || runtime.advanced_nodes == null)
            return false;
        for (var i = 0; i < runtime.advanced_nodes.Count; i++)
        {
            YuanshenNodeHandle handle = runtime.advanced_nodes[i];
            if (!YuanshenNodeLockService.TryResolve(handle, out Entity node) ||
                !node.TryGetComponent(out YuanshenNodeState identity) || identity.role != role) continue;
            AssignEngage(node, target);
            return true;
        }
        return false;
    }

    /// <summary>让当前聚焦的法相或化身攻击一名明确敌人。</summary>
    /// <param name="actor">节点所属原人物。</param>
    /// <param name="target">玩家或人工智能明确指定的敌方人物。</param>
    /// <returns>聚焦节点角色与敌对关系均有效时返回真。</returns>
    public static bool TryAssignFocusedEngage(ActorExtend actor, Actor target)
    {
        if (actor == null || target == null || target.isRekt() || !target.isAlive() ||
            !actor.Base.canAttackTarget(target) ||
            !YuanshenThoughtService.TryGetFocused(actor, out YuanshenNodeHandle handle, out _) ||
            !YuanshenNodeLockService.TryResolve(handle, out Entity node) ||
            !node.TryGetComponent(out YuanshenNodeState identity) ||
            identity.role is not (YuanshenNodeRole.DharmaForm or YuanshenNodeRole.Avatar)) return false;
        AssignEngage(node, target);
        return true;
    }

    /// <summary>让当前聚焦元神节点沿明确连接迁往点选授权锚点。</summary>
    /// <param name="actor">节点所属原人物和资源支付者。</param>
    /// <param name="point">明确目标锚点位置。</param>
    /// <returns>节点、连接、容量、引导和资源条件满足时返回真。</returns>
    public static bool TryTransitFocused(ActorExtend actor, Vector2 point)
    {
        if (actor == null) return false;
        if (!YuanshenThoughtService.TryGetFocused(actor, out YuanshenNodeHandle focused, out Vector2 nodePosition))
            return TryStartBodilessTransit(actor, point);
        if (!YuanshenNodeLockService.TryResolve(focused, out Entity node) ||
            node.HasComponent<YuanshenAnchorTransitState>() ||
            !node.TryGetComponent(out YuanshenNodeState identity) ||
            identity.role == YuanshenNodeRole.Manifestation ||
            identity.IntegrityRatio < 0.5f ||
            !YuanshenAnchorNetworkService.TryGetAuthorizedAtPoint(actor, point,
                out YuanshenAnchorHandle destination, out Entity destinationEntity) ||
            !destinationEntity.TryGetComponent(out Position destinationPosition) ||
            !YuanshenAnchorNetworkService.CanTransit(actor, nodePosition, destination,
                out YuanshenAnchorHandle source) ||
            !YuanshenAnchorNetworkService.CanAcceptLoad(destination, identity.mind_share)) return false;
        float distance = Vector2.Distance(nodePosition, destinationPosition.v2);
        float costRatio = Mathf.Clamp(TransitBaseWakanRatio + distance / 100f * 0.01f,
            TransitBaseWakanRatio, 0.15f);
        if (!WakanResourceService.TrySpendMaximumRatio(actor, costRatio)) return false;
        double duration = Math.Max(1d, distance / 100d);
        var transit = new YuanshenAnchorTransitState
        {
            source = source,
            destination = destination,
            completes_at = Now + duration,
            starting_integrity = identity.integrity_current
        };
        if (node.HasComponent<YuanshenAnchorTransitState>()) node.GetComponent<YuanshenAnchorTransitState>() = transit;
        else node.AddComponent(transit);
        ref YuanshenNodeState mutableIdentity = ref node.GetComponent<YuanshenNodeState>();
        mutableIdentity.action = YuanshenNodeAction.Idle;
        ref YuanshenNodeTask task = ref node.GetComponent<YuanshenNodeTask>();
        task = new YuanshenNodeTask
        {
            kind = YuanshenNodeTaskKind.AnchorTransit,
            point = destinationPosition.v2,
            started_at = Now
        };
        return true;
    }

    /// <summary>让无肉身命魂本体沿明确连接开始迁往点选锚点。</summary>
    /// <param name="actor">保持原身份的人物本体。</param>
    /// <param name="point">玩家或人工智能明确点选的终点锚点。</param>
    /// <returns>无身状态、连接、终点和灵气均允许时返回真。</returns>
    public static bool TryStartBodilessTransit(ActorExtend actor, Vector2 point)
    {
        if (!CanUseAdvanced(actor, 7) || !YuanshenLifecycleService.IsBodiless(actor) ||
            actor.HasComponent<YuanshenBodilessTransitState>() ||
            !YuanshenAnchorNetworkService.TryGetAuthorizedAtPoint(actor, point,
                out YuanshenAnchorHandle destination, out Entity destinationEntity) ||
            !destinationEntity.TryGetComponent(out Position destinationPosition) ||
            !YuanshenAnchorNetworkService.CanTransit(actor, actor.Base.current_position, destination,
                out YuanshenAnchorHandle source)) return false;
        float distance = Vector2.Distance(actor.Base.current_position, destinationPosition.v2);
        if (distance <= YuanshenAnchorNetworkService.PresenceRange) return false;
        float costRatio = Mathf.Clamp(TransitBaseWakanRatio + distance / 100f * 0.01f,
            TransitBaseWakanRatio, 0.15f);
        if (!WakanResourceService.TrySpendMaximumRatio(actor, costRatio)) return false;
        actor.GetOrAddComponent<YuanshenBodilessTransitState>() = new YuanshenBodilessTransitState
        {
            source = source,
            destination = destination,
            source_position = actor.Base.current_position,
            completes_at = Now + Math.Max(1d, distance / 100d),
            starting_health = actor.Base.data.health
        };
        actor.Base.cancelAllBeh();
        actor.Base.clearAttackTarget();
        actor.Base.clearTileTarget();
        return true;
    }

    /// <summary>完成一段无身命魂锚点迁移。</summary>
    /// <param name="actor">正在迁移的原人物。</param>
    /// <param name="transit">查询阶段冻结的迁移状态。</param>
    /// <returns>终点提交时仍可用并完成移动时返回真。</returns>
    public static bool CompleteBodilessTransit(ActorExtend actor, YuanshenBodilessTransitState transit)
    {
        if (actor == null || !actor.HasComponent<YuanshenBodilessTransitState>() ||
            !YuanshenLifecycleService.IsBodiless(actor) ||
            !YuanshenAnchorNetworkService.TryGetUsableAuthorized(
                actor, transit.destination, out _, out Vector2 destination)) return false;
        WorldTile tile = World.world?.GetTileSimple(Mathf.RoundToInt(destination.x), Mathf.RoundToInt(destination.y));
        if (tile == null) return false;
        actor.E.RemoveComponent<YuanshenBodilessTransitState>();
        actor.Base.spawnOn(tile, 0f);
        return true;
    }

    /// <summary>中断无身命魂迁移并退回起点或开始位置。</summary>
    /// <param name="actor">正在迁移的原人物。</param>
    /// <param name="transit">查询阶段冻结的迁移状态。</param>
    public static void InterruptBodilessTransit(ActorExtend actor, YuanshenBodilessTransitState transit)
    {
        if (actor == null) return;
        if (actor.HasComponent<YuanshenBodilessTransitState>())
            actor.E.RemoveComponent<YuanshenBodilessTransitState>();
        if (!YuanshenLifecycleService.IsBodiless(actor)) return;
        Vector2 fallback = transit.source_position;
        if (transit.source.IsValid && YuanshenAnchorNetworkService.TryGetUsableAuthorized(
                actor, transit.source, out _, out Vector2 sourcePosition)) fallback = sourcePosition;
        WorldTile tile = World.world?.GetTileSimple(Mathf.RoundToInt(fallback.x), Mathf.RoundToInt(fallback.y));
        if (tile != null) actor.Base.spawnOn(tile, 0f);
        YuanshenTravelService.LockMainMindShare(actor, 10f);
    }

    /// <summary>让一枚驻留设施的节点沿原连接引导归还人物主心神。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="node">已经请求归返的节点。</param>
    /// <returns>节点驻留锚点和人物主位置均有效并开始引导时返回真。</returns>
    public static bool TryStartRootReturn(ActorExtend actor, Entity node)
    {
        if (actor == null || node.IsNull || node.HasComponent<YuanshenAnchorTransitState>() ||
            !node.TryGetComponent(out YuanshenNodeState identity) ||
            identity.owner_actor_id != actor.Base.data.id ||
            !node.TryGetComponent(out YuanshenAnchorResidence residence) ||
            !YuanshenAnchorNetworkService.TryGetUsableAuthorized(actor, residence.anchor, out _, out _) ||
            !node.TryGetComponent(out Position position) ||
            !TryResolveRootPosition(actor, identity.role, out Vector2 rootPosition)) return false;
        double duration = Math.Max(1d, Vector2.Distance(position.v2, rootPosition) / 100d);
        node.AddComponent(new YuanshenAnchorTransitState
        {
            source = residence.anchor,
            destination = default,
            completes_at = Now + duration,
            return_to_root = true,
            starting_integrity = identity.integrity_current
        });
        ref YuanshenNodeState mutableIdentity = ref node.GetComponent<YuanshenNodeState>();
        mutableIdentity.action = YuanshenNodeAction.Idle;
        ref YuanshenNodeTask task = ref node.GetComponent<YuanshenNodeTask>();
        task = new YuanshenNodeTask
        {
            kind = YuanshenNodeTaskKind.AnchorTransit,
            point = rootPosition,
            started_at = Now
        };
        return true;
    }

    /// <summary>完成一枚节点已经到期且未受干扰的锚点迁移。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="node">迁移节点。</param>
    /// <param name="transit">冻结迁移状态。</param>
    /// <returns>终点仍可用且容量允许时返回真。</returns>
    public static bool CompleteTransit(
        ActorExtend actor,
        Entity node,
        in YuanshenAnchorTransitState transit)
    {
        if (actor == null || node.IsNull || !node.TryGetComponent(out YuanshenNodeState identity) ||
            identity.owner_actor_id != actor.Base.data.id) return false;
        if (transit.return_to_root)
        {
            if (!TryResolveRootPosition(actor, identity.role, out Vector2 rootPosition)) return false;
            node.GetComponent<Position>().v2 = rootPosition;
            node.RemoveComponent<YuanshenAnchorTransitState>();
            return YuanshenThoughtService.CompleteReturn(actor, node);
        }
        if (!YuanshenAnchorNetworkService.TryGetUsableAuthorized(actor, transit.destination,
                out _, out Vector2 destination) ||
            !YuanshenAnchorNetworkService.TrySetResidence(node, transit.destination)) return false;
        ref Position position = ref node.GetComponent<Position>();
        position.v2 = destination;
        node.RemoveComponent<YuanshenAnchorTransitState>();
        ref YuanshenNodeState mutableIdentity = ref node.GetComponent<YuanshenNodeState>();
        mutableIdentity.action = YuanshenNodeAction.Idle;
        ref YuanshenNodeTask task = ref node.GetComponent<YuanshenNodeTask>();
        task = new YuanshenNodeTask
        {
            kind = YuanshenNodeTaskKind.Idle,
            point = destination,
            started_at = Now
        };
        return true;
    }

    /// <summary>迁移受击或连接失效时退回明确起点并施加有限反噬。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="node">迁移节点。</param>
    /// <param name="transit">冻结迁移状态。</param>
    public static void InterruptTransit(
        ActorExtend actor,
        Entity node,
        in YuanshenAnchorTransitState transit)
    {
        if (node.IsNull) return;
        if (transit.source.IsValid &&
            YuanshenAnchorNetworkService.TryGetUsableAuthorized(actor, transit.source, out _, out Vector2 source))
        {
            node.GetComponent<Position>().v2 = source;
                YuanshenAnchorNetworkService.TrySetResidence(node, transit.source);
        }
        else if (node.TryGetComponent(out YuanshenNodeState currentIdentity) &&
                 TryResolveRootPosition(actor, currentIdentity.role, out Vector2 root))
        {
        YuanshenAnchorNetworkService.ReleaseResidence(node);
            node.GetComponent<Position>().v2 = root;
            }
        if (node.HasComponent<YuanshenAnchorTransitState>()) node.RemoveComponent<YuanshenAnchorTransitState>();
        if (node.HasComponent<YuanshenNodeState>())
        {
            ref YuanshenNodeState identity = ref node.GetComponent<YuanshenNodeState>();
            identity.action = YuanshenNodeAction.Idle;
        }
        if (node.HasComponent<YuanshenNodeTask>())
            node.GetComponent<YuanshenNodeTask>() = new YuanshenNodeTask
            {
                kind = YuanshenNodeTaskKind.Idle,
                started_at = Now
            };
        LockAdditionalShare(actor, node, 0.1f);
    }

    /// <summary>让指定角色的全部高阶节点主动归返。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="role">需要收回的高阶角色。</param>
    /// <returns>至少有一枚有效节点收到归返命令时返回真。</returns>
    public static bool RequestRoleReturn(ActorExtend actor, YuanshenNodeRole role)
    {
        if (actor == null || !actor.TryGetComponent(out YuanshenRuntimeState runtime) ||
            runtime.advanced_nodes == null) return false;
        bool changed = false;
        for (var i = 0; i < runtime.advanced_nodes.Count; i++)
        {
            YuanshenNodeHandle handle = runtime.advanced_nodes[i];
            if (!YuanshenNodeLockService.TryResolve(handle, out Entity node) ||
                !node.TryGetComponent(out YuanshenNodeState identity) || identity.role != role) continue;
            changed |= YuanshenThoughtService.RequestReturn(actor, handle);
        }
        return changed;
    }

    /// <summary>统计人物当前指定角色的有效高阶节点。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="role">需要统计的节点角色。</param>
    /// <returns>有效节点数量。</returns>
    public static int CountRole(ActorExtend actor, YuanshenNodeRole role)
    {
        if (actor == null || !actor.TryGetComponent(out YuanshenRuntimeState runtime) ||
            runtime.advanced_nodes == null) return 0;
        int count = 0;
        for (var i = 0; i < runtime.advanced_nodes.Count; i++)
            if (YuanshenNodeLockService.TryResolve(runtime.advanced_nodes[i], out Entity node) &&
                node.TryGetComponent(out YuanshenNodeState identity) && identity.role == role) count++;
        return count;
    }

    /// <summary>高阶节点依赖锚点失效时按比例锁伤并回收其余份额。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="node">需要消散的高阶节点。</param>
    /// <param name="lockRatio">当前可用份额转为创伤的比例。</param>
    public static void Disperse(ActorExtend actor, Entity node, float lockRatio)
    {
        if (actor == null || node.IsNull || !node.TryGetComponent(out YuanshenNodeState identity) ||
            identity.owner_actor_id != actor.Base.data.id) return;
        float remaining = Mathf.Max(0f, identity.mind_share);
        float locked = remaining * Mathf.Clamp01(lockRatio);
        ref YuanshenRuntimeState runtime = ref actor.GetOrAddComponent<YuanshenRuntimeState>();
        runtime.main_soul_share += remaining - locked;
        runtime.injury_locked_share = Mathf.Clamp(runtime.injury_locked_share + locked, 0f, 100f);
        YuanshenNodeHandle handle = new(in identity);
        ArtifactYuanshenControlService.ReleaseNodeArtifact(actor, handle);
        RemoveHandle(runtime.advanced_nodes, handle);
        RemoveHandle(runtime.thought_nodes, handle);
        if (runtime.focused_node == handle)
            runtime.focused_node = default;
        YuanshenAnchorNetworkService.ReleaseResidence(node);
        YuanshenNodeLockService.UnregisterNode(node);
        if (!node.Tags.Has<TagRecycle>()) node.AddTag<TagRecycle>();
        if (locked > 0f)
            CombatStatusEffects.ApplyStatus(
                actor.Base,
                StatusEffects.SoulTrauma,
                Mathf.Max(Cultiway.Const.TimeScales.SecPerMonth,
                    locked * Cultiway.Const.TimeScales.SecPerMonth),
                actor.Base);
        YuanshenTravelService.NotifyMindStateChanged(actor);
    }

    /// <summary>按高阶节点角色、份额和预算检查创建条件。</summary>
    private static bool CanCreateAdvanced(
        ActorExtend actor,
        YuanshenNodeRole role,
        int requiredStage,
        float share,
        bool requiresThread,
        out Vector2 origin)
    {
        origin = default;
        if (!CanUseAdvanced(actor, requiredStage) || CountRole(actor, role) > 0 ||
            !CanReserveShare(actor, share) ||
            !YuanshenTravelService.TryGetMainSoulPosition(actor, out Vector3 origin3)) return false;
        DivineSenseBudget budget = DivineSenseBudgetService.Resolve(actor);
        float requiredLoadRatio = role switch
        {
            YuanshenNodeRole.DharmaForm => 0.35f,
            YuanshenNodeRole.Avatar => 0.25f,
            YuanshenNodeRole.Manifestation => 0.2f,
            _ => 0.1f
        };
        if (budget.TotalLoadCapacity <= 0f ||
            budget.ReservedLoad + budget.TotalLoadCapacity * requiredLoadRatio > budget.TotalLoadCapacity ||
            requiresThread && budget.AvailableArtifactThreads <= 0) return false;
        origin = origin3;
        return true;
    }

    /// <summary>检查人物元神层数和重度身体恢复状态。</summary>
    private static bool CanUseAdvanced(ActorExtend actor, int requiredStage)
    {
        if (!YuanshenNodeCombatService.CanUseSoulAbilities(actor) ||
            !actor.TryGetComponent(out Yuanshen yuanshen) || yuanshen.stage < requiredStage ||
            actor.HasComponent<YuanshenPossessionState>() || actor.HasComponent<YuanshenReconstructionState>() ||
            actor.HasComponent<YuanshenBodilessTransitState>() ||
            CombatStatusEffects.HasStatus(actor.Base, StatusEffects.BodyDisharmony) ||
            CombatStatusEffects.HasStatus(actor.Base, StatusEffects.BodyReconstructionWeakness)) return false;
        return !actor.TryGetComponent(out YuanshenRuntimeState runtime) || runtime.injury_locked_share <= 15f;
    }

    /// <summary>检查共享神识预算是否还能承担一枚独立节点。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <returns>至少还有一道可用分念容量时返回真。</returns>
    private static bool HasAvailableThread(ActorExtend actor)
    {
        return DivineSenseBudgetService.Resolve(actor).AvailableArtifactThreads > 0;
    }

    /// <summary>检查命魂在划出份额后仍保留最低份额。</summary>
    private static bool CanReserveShare(ActorExtend actor, float share)
    {
        ref YuanshenRuntimeState runtime = ref actor.GetOrAddComponent<YuanshenRuntimeState>();
        YuanshenThoughtService.EnsureSession(actor, ref runtime);
        return runtime.main_soul_share - share >= YuanshenThoughtService.MinimumMainSoulShare;
    }

    /// <summary>创建一枚高阶节点并原子扣除心神份额。</summary>
    private static Entity CreateAdvancedNode(
        ActorExtend actor,
        YuanshenNodeRole role,
        float share,
        Vector2 origin,
        Vector2 target,
        double expiresAt,
        float upkeepRatio,
        YuanshenAnchorHandle anchor,
        bool supportOnly)
    {
        ref YuanshenRuntimeState runtime = ref actor.GetOrAddComponent<YuanshenRuntimeState>();
        YuanshenThoughtService.EnsureSession(actor, ref runtime);
        if (runtime.main_soul_share - share < YuanshenThoughtService.MinimumMainSoulShare) return default;
        runtime.generation = runtime.generation == int.MaxValue ? 1 : Mathf.Max(0, runtime.generation) + 1;
        int logicalId = Mathf.Max(2, runtime.next_logical_id++);
        ResolveAppearance(actor.GetComponent<Yuanshen>(), out YuanshenDharmaAppearance appearance,
            out Sprite sprite);
        float integrityScale = role switch
        {
            YuanshenNodeRole.DharmaForm => 2f,
            YuanshenNodeRole.Avatar => 1.4f,
            YuanshenNodeRole.Manifestation => 0.8f,
            _ => 1f
        };
        float integrity = YuanshenTravelService.ResolveIntegrityMaximum(actor, share) * integrityScale;
        float scale = role switch
        {
            YuanshenNodeRole.DharmaForm => 0.72f,
            YuanshenNodeRole.Avatar => 0.36f,
            YuanshenNodeRole.Manifestation => 0.3f,
            _ => 0.3f
        };
        Color tint = appearance.element_color;
        tint.a = role == YuanshenNodeRole.Avatar ? 0.86f : role == YuanshenNodeRole.Manifestation ? 0.52f : 0.72f;
        bool moving = Vector2.Distance(origin, target) > YuanshenTravelService.ReturnCompletionDistance;
        Entity node = ModClass.I.W.CreateEntity(
            new YuanshenNodeState
            {
                owner_actor_id = actor.Base.data.id,
                session_id = runtime.session_id,
                logical_id = logicalId,
                generation = runtime.generation,
                role = role,
                mind_share = share,
                action = moving ? YuanshenNodeAction.Moving : YuanshenNodeAction.Idle,
                move_target = target,
                move_speed = YuanshenTravelService.NodeMoveSpeed * (role == YuanshenNodeRole.DharmaForm ? 0.75f : 0.9f),
                integrity_maximum = integrity,
                integrity_current = integrity,
                allocated_share = share,
                tether_condition = YuanshenTetherCondition.Stable
            },
            new YuanshenNodeTask
            {
                kind = moving ? YuanshenNodeTaskKind.Move : YuanshenNodeTaskKind.Idle,
                point = target,
                started_at = Now
            },
            new YuanshenAdvancedNodeState
            {
                expires_at = expiresAt,
                upkeep_ratio = upkeepRatio,
                anchor = anchor,
                support_only = supportOnly
            },
            appearance,
            new Position(origin.x, origin.y, role == YuanshenNodeRole.DharmaForm ? 0.55f : 0.4f),
            new Scale(scale),
            new AnimData { frames = [sprite] });
        node.AddComponent(new AnimBindRenderer());
        node.AddComponent(new AnimTint(tint));
        YuanshenNodeState identity = node.GetComponent<YuanshenNodeState>();
        runtime.advanced_nodes ??= new List<YuanshenNodeHandle>(3);
        runtime.advanced_nodes.Add(identity.GetHandle());
        runtime.main_soul_share -= share;
        YuanshenNodeLockService.RegisterNode(node);
        YuanshenTravelService.NotifyMindStateChanged(actor);
        return node;
    }

    /// <summary>把明确敌方人物写入高阶节点唯一任务。</summary>
    private static void AssignEngage(Entity node, Actor target)
    {
        ref YuanshenAdvancedNodeState advanced = ref node.GetComponent<YuanshenAdvancedNodeState>();
        advanced.target_actor_id = target.data.id;
        ref YuanshenNodeTask task = ref node.GetComponent<YuanshenNodeTask>();
        task = new YuanshenNodeTask
        {
            kind = YuanshenNodeTaskKind.EngageActor,
            target_object_id = target.data.id,
            point = target.current_position,
            started_at = Now
        };
        ref YuanshenNodeState identity = ref node.GetComponent<YuanshenNodeState>();
        identity.action = YuanshenNodeAction.Idle;
    }

    /// <summary>创建后把技能和后续命令聚焦到高阶节点。</summary>
    private static void FocusNode(ActorExtend actor, Entity node)
    {
        if (node.TryGetComponent(out YuanshenNodeState identity))
            YuanshenThoughtService.TryFocus(actor, new YuanshenNodeHandle(in identity));
    }

    /// <summary>从形成显化原子选择有限主体模板、元素覆盖和道路倾向。</summary>
    private static void ResolveAppearance(
        in Yuanshen yuanshen,
        out YuanshenDharmaAppearance appearance,
        out Sprite sprite)
    {
        CoreFormationSnapshot formation = yuanshen.formation;
        YuanshenDharmaTemplate template = YuanshenDharmaTemplate.General;
        float templateWeight = float.MinValue;
        bool sword = false;
        bool body = false;
        bool illusion = false;
        bool reservoir = false;
        CoreFormationAtomState[] atoms = formation.atoms ?? Array.Empty<CoreFormationAtomState>();
        for (var i = 0; i < atoms.Length; i++)
        {
            CoreFormationAtomState atom = atoms[i];
            if (!atom.IsActive(yuanshen.stage)) continue;
            if (CoreFormationAtoms.PathSword != null && atom.atom_id == CoreFormationAtoms.PathSword.id) sword = true;
            if (CoreFormationAtoms.PathBody != null && atom.atom_id == CoreFormationAtoms.PathBody.id) body = true;
            if (CoreFormationAtoms.PathIllusion != null && atom.atom_id == CoreFormationAtoms.PathIllusion.id) illusion = true;
            if (CoreFormationAtoms.PathReservoir != null && atom.atom_id == CoreFormationAtoms.PathReservoir.id)
                reservoir = true;
            YuanshenDharmaTemplate candidate = ResolveTemplate(atom.atom_id);
            if (candidate == YuanshenDharmaTemplate.General || atom.weight < templateWeight) continue;
            template = candidate;
            templateWeight = atom.weight;
        }
        Color color = SkillVfxColor.GetElementColor(formation.composition);
        appearance = new YuanshenDharmaAppearance
        {
            template = template,
            element_color = color,
            sword_path = sword,
            body_path = body,
            illusion_path = illusion,
            reservoir_path = reservoir
        };
        string spritePath = template switch
        {
            YuanshenDharmaTemplate.SpiritPlatform => "cultiway/icons/artifact_atoms/resonance_rings",
            YuanshenDharmaTemplate.SwordEmbryo => "cultiway/icons/artifact_atoms/sword_edge",
            YuanshenDharmaTemplate.DragonAspect => "cultiway/icons/artifact_atoms/ancestral_guardian_vow",
            YuanshenDharmaTemplate.PrimalBody => "cultiway/icons/artifact_atoms/life_pattern",
            _ => "cultiway/special_effects/aura/yuanying_aura"
        };
        sprite = SpriteTextureLoader.getSprite(spritePath) ??
                 SpriteTextureLoader.getSprite("cultiway/special_effects/aura/yuanying_indicator");
        if (sprite == null) throw new InvalidOperationException("缺少高阶元神节点可用贴图。");
    }

    /// <summary>把形成原子编号映射为有限主体模板。</summary>
    private static YuanshenDharmaTemplate ResolveTemplate(string atomId)
    {
        if (CoreFormationAtoms.ManifestSpiritPlatform != null &&
            atomId == CoreFormationAtoms.ManifestSpiritPlatform.id) return YuanshenDharmaTemplate.SpiritPlatform;
        if (CoreFormationAtoms.ManifestSwordEmbryo != null &&
            atomId == CoreFormationAtoms.ManifestSwordEmbryo.id) return YuanshenDharmaTemplate.SwordEmbryo;
        if (CoreFormationAtoms.ManifestDragonAspect != null &&
            atomId == CoreFormationAtoms.ManifestDragonAspect.id) return YuanshenDharmaTemplate.DragonAspect;
        if (CoreFormationAtoms.ManifestPrimalBody != null &&
            atomId == CoreFormationAtoms.ManifestPrimalBody.id) return YuanshenDharmaTemplate.PrimalBody;
        return YuanshenDharmaTemplate.General;
    }

    /// <summary>锁定节点当前剩余份额的一部分但不摧毁节点。</summary>
    private static void LockAdditionalShare(ActorExtend actor, Entity node, float ratio)
    {
        if (actor == null || node.IsNull || !node.TryGetComponent(out YuanshenNodeState current)) return;
        float amount = Mathf.Min(current.mind_share, current.mind_share * Mathf.Clamp01(ratio));
        if (amount <= 0f) return;
        ref YuanshenNodeState identity = ref node.GetComponent<YuanshenNodeState>();
        identity.mind_share -= amount;
        identity.locked_share += amount;
        ref YuanshenRuntimeState runtime = ref actor.GetOrAddComponent<YuanshenRuntimeState>();
        runtime.injury_locked_share = Mathf.Clamp(runtime.injury_locked_share + amount, 0f, 100f);
        CombatStatusEffects.ApplyStatus(
            actor.Base,
            StatusEffects.SoulTrauma,
            Mathf.Max(Cultiway.Const.TimeScales.SecPerMonth,
                amount * Cultiway.Const.TimeScales.SecPerMonth),
            actor.Base);
        YuanshenTravelService.NotifyMindStateChanged(actor);
    }

    /// <summary>解析节点归还心神时使用的唯一主位置。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="role">节点角色。</param>
    /// <param name="position">返回肉身或命魂位置。</param>
    /// <returns>主位置可用时返回真。</returns>
    private static bool TryResolveRootPosition(
        ActorExtend actor,
        YuanshenNodeRole role,
        out Vector2 position)
    {
        position = default;
        if (actor?.Base == null || actor.Base.isRekt()) return false;
        if (YuanshenTravelService.TryGetMainSoulPosition(actor, out Vector3 soulPosition))
        {
            position = soulPosition;
            return true;
        }
        position = actor.Base.current_position;
        return actor.Base.current_tile != null;
    }

    /// <summary>从句柄列表移除指定节点。</summary>
    private static void RemoveHandle(List<YuanshenNodeHandle> handles, YuanshenNodeHandle target)
    {
        if (handles == null) return;
        for (var i = handles.Count - 1; i >= 0; i--)
            if (handles[i] == target) handles.RemoveAt(i);
    }

    /// <summary>当前世界时间。</summary>
    private static double Now => World.world?.getCurWorldTime() ?? 0d;
}
