using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>集中管理有上限、需要明确交互的元神设施锚点和连接。</summary>
public static class YuanshenAnchorNetworkService
{
    /// <summary>每名人物最多设立的物质设施锚点。</summary>
    public const int MaximumOwnedFacilities = 2;

    /// <summary>一处设施最多保留的直接连接。</summary>
    public const int MaximumLinksPerAnchor = 4;

    /// <summary>设施锚点提供牵引的半径。</summary>
    public const float TetherRadius = 160f;

    /// <summary>人物或分念建立锚点、补充香火时必须接近的距离。</summary>
    public const float PresenceRange = 8f;

    /// <summary>设立设施锚点消耗的最大灵气比例。</summary>
    private const float ConsecrationWakanRatio = 0.15f;

    /// <summary>建立锚点连接消耗的最大灵气比例。</summary>
    private const float ConnectionWakanRatio = 0.05f;

    /// <summary>主动补充香火消耗的最大灵气比例。</summary>
    private const float IncenseOfferingWakanRatio = 0.04f;


    /// <summary>稳定句柄到当前锚点实体的运行时解析表。</summary>
    private static readonly Dictionary<YuanshenAnchorHandle, Entity> Anchors = new();

    /// <summary>物质建筑编号到锚点句柄的一对一映射。</summary>
    private static readonly Dictionary<long, YuanshenAnchorHandle> BuildingAnchors = new();

    /// <summary>防止保护击杀事件重复注册。</summary>
    private static bool initialized;

    /// <summary>注册由真实保护击杀产生香火的事件入口，并挂接世界清理的内嵌系统。</summary>
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        ActorExtend.RegisterActionOnKill(OnProtectionKill);
        ModClass.I.GeneralLogicSystems.Add(new ClearStateSystem());
    }

    /// <summary>在人物或其明确分念实际抵达的建筑上设立一处设施锚点。</summary>
    /// <param name="actor">设立者和资源支付者。</param>
    /// <param name="point">玩家或人工智能明确指定的建筑位置。</param>
    /// <param name="kind">元神台或香火坛。</param>
    /// <returns>元神层数、归属、容量、到场和资源均满足时返回真。</returns>
    public static bool TryConsecrate(ActorExtend actor, Vector2 point, YuanshenAnchorKind kind)
    {
        if (!CanManageNetwork(actor) || !TryGetBuildingAt(point, out Building building) ||
            !TryResolveCollective(actor, building, kind, out long collectiveId) ||
            !HasPresenceNear(actor, building.current_position) ||
            !TryPruneOwned(actor, out List<YuanshenAnchorHandle> owned) ||
            owned.Count >= MaximumOwnedFacilities ||
            BuildingAnchors.TryGetValue(building.data.id, out YuanshenAnchorHandle existing) &&
            TryResolve(existing, out _)) return false;
        if (!WakanResourceService.TrySpendMaximumRatio(actor, ConsecrationWakanRatio)) return false;

        ref YuanshenAnchorNetworkRuntime network = ref actor.GetOrAddComponent<YuanshenAnchorNetworkRuntime>();
        network.next_generation = network.next_generation == int.MaxValue
            ? 1
            : Mathf.Max(0, network.next_generation) + 1;
        Sprite sprite = SpriteTextureLoader.getSprite(kind == YuanshenAnchorKind.SectPlatform
            ? "cultiway/icons/artifact_atoms/spirit_ding"
            : "cultiway/icons/artifact_atoms/soul_banner");
        if (sprite == null) throw new InvalidOperationException("缺少元神设施锚点可用贴图。");
        double now = Now;
        Entity anchor = ModClass.I.W.CreateEntity(
            new YuanshenAnchorState
            {
                owner_actor_id = actor.Base.data.id,
                building_id = building.data.id,
                generation = network.next_generation,
                kind = kind,
                collective_id = collectiveId,
                established_at = now,
                load_capacity = kind == YuanshenAnchorKind.SectPlatform ? 100f : 80f,
                incense_capacity = kind == YuanshenAnchorKind.CityAltar ? 100f : 0f,
                last_building_health = building.getHealth(),
                link_handles = new List<YuanshenAnchorHandle>(2)
            },
            new Position(building.current_position.x, building.current_position.y, 0.65f),
            new Scale(kind == YuanshenAnchorKind.SectPlatform ? 0.24f : 0.2f),
            new AnimData { frames = [sprite] },
            new AnimBindRenderer(),
            new AnimTint(kind == YuanshenAnchorKind.SectPlatform
                ? new Color(0.42f, 0.92f, 0.84f, 0.72f)
                : new Color(1f, 0.78f, 0.32f, 0.72f)));
        YuanshenAnchorState state = anchor.GetComponent<YuanshenAnchorState>();
        YuanshenAnchorHandle handle = new(anchor.Id, in state);
        Anchors[handle] = anchor;
        BuildingAnchors[building.data.id] = handle;
        network.owned_anchors ??= new List<YuanshenAnchorHandle>(MaximumOwnedFacilities);
        network.owned_anchors.Add(handle);
        network.selected_anchor = handle;
        YuanshenTravelService.NotifyMindStateChanged(actor);
        return true;
    }

    /// <summary>在明确到场后选定建立连接的第一处授权锚点。</summary>
    /// <param name="actor">网络使用者。</param>
    /// <param name="point">明确点选位置。</param>
    /// <returns>点中可用授权锚点且本人或分念已经到场时返回真。</returns>
    public static bool TrySelectForConnection(ActorExtend actor, Vector2 point)
    {
        if (!TryGetAuthorizedAtPoint(actor, point, out YuanshenAnchorHandle handle, out Entity anchor) ||
            !anchor.TryGetComponent(out Position position) || !HasPresenceNear(actor, position.v2)) return false;
        actor.GetOrAddComponent<YuanshenAnchorNetworkRuntime>().selected_anchor = handle;
        return true;
    }

    /// <summary>把已选锚点与明确点选并到场的第二处授权锚点双向连接。</summary>
    /// <param name="actor">连接建立者和资源支付者。</param>
    /// <param name="point">第二处锚点的明确位置。</param>
    /// <returns>两端有效、不同、到场且连接容量允许时返回真。</returns>
    public static bool TryConnectSelected(ActorExtend actor, Vector2 point)
    {
        if (actor == null || !actor.TryGetComponent(out YuanshenAnchorNetworkRuntime network) ||
            !TryGetUsableAuthorized(actor, network.selected_anchor, out Entity source, out _) ||
            !TryGetAuthorizedAtPoint(actor, point, out YuanshenAnchorHandle destinationHandle,
                out Entity destination) ||
            destinationHandle == network.selected_anchor ||
            !destination.TryGetComponent(out Position destinationPosition) ||
            !HasPresenceNear(actor, destinationPosition.v2) ||
            !CanAddLink(source, destinationHandle) || !CanAddLink(destination, network.selected_anchor) ||
            !WakanResourceService.TrySpendMaximumRatio(actor, ConnectionWakanRatio)) return false;
        AddLink(source, destinationHandle);
        AddLink(destination, network.selected_anchor);
        ref YuanshenAnchorNetworkRuntime mutable = ref actor.GetComponent<YuanshenAnchorNetworkRuntime>();
        mutable.selected_anchor = destinationHandle;
        return true;
    }

    /// <summary>判断人物命魂或当前聚焦节点是否已经抵达指定锚点。</summary>
    /// <param name="actor">网络使用者。</param>
    /// <param name="handle">目标授权锚点。</param>
    /// <returns>锚点可用且有明确人物心神在场时返回真。</returns>
    public static bool IsPresenceAtAnchor(ActorExtend actor, YuanshenAnchorHandle handle)
    {
        return TryGetUsableAuthorized(actor, handle, out _, out Vector2 position) &&
               HasPresenceNear(actor, position);
    }

    /// <summary>在人物明确抵达后为授权香火坛补充有限愿力。</summary>
    /// <param name="actor">供奉者和资源支付者。</param>
    /// <param name="point">明确设施位置。</param>
    /// <returns>目标为授权香火坛且到场、资源和愿力容量满足时返回真。</returns>
    public static bool TryOfferIncense(ActorExtend actor, Vector2 point)
    {
        if (!TryGetAuthorizedAtPoint(actor, point, out _, out Entity anchor) ||
            !anchor.TryGetComponent(out YuanshenAnchorState state) ||
            state.kind != YuanshenAnchorKind.CityAltar ||
            !anchor.TryGetComponent(out Position position) || !HasPresenceNear(actor, position.v2)) return false;
        if (state.incense_capacity <= 0f || state.incense >= state.incense_capacity - 0.001f ||
            !WakanResourceService.TrySpendMaximumRatio(actor, IncenseOfferingWakanRatio)) return false;
        state.incense = Mathf.Min(state.incense_capacity, state.incense + 8f);
        return true;
    }

    /// <summary>人物到场且锚点没有任何负载时解除本人设施寄托。</summary>
    /// <param name="actor">设施设立者。</param>
    /// <param name="point">明确设施位置。</param>
    /// <returns>本人锚点无负载并完成解除时返回真。</returns>
    public static bool TryDismantle(ActorExtend actor, Vector2 point)
    {
        if (!TryGetAnchorAtPoint(point, out YuanshenAnchorHandle handle, out Entity anchor) ||
            handle.OwnerActorId != actor?.Base?.data?.id ||
            !anchor.TryGetComponent(out Position position) || !HasPresenceNear(actor, position.v2) ||
            !anchor.TryGetComponent(out YuanshenAnchorState state) || state.current_load > 0.001f) return false;
        Collapse(handle, false);
        return true;
    }

    /// <summary>从稳定句柄解析仍存在的锚点实体。</summary>
    /// <param name="handle">锚点稳定句柄。</param>
    /// <param name="anchor">返回当前实体。</param>
    /// <returns>全部身份字段仍一致时返回真。</returns>
    public static bool TryResolve(YuanshenAnchorHandle handle, out Entity anchor)
    {
        anchor = default;
        if (!handle.IsValid || !Anchors.TryGetValue(handle, out Entity candidate) || candidate.IsNull ||
            candidate.Tags.Has<TagRecycle>() ||
            !candidate.TryGetComponent(out YuanshenAnchorState state) ||
            new YuanshenAnchorHandle(candidate.Id, in state) != handle)
        {
            Anchors.Remove(handle);
            return false;
        }
        anchor = candidate;
        return true;
    }

    /// <summary>解析物质载体仍存活且归属有效的锚点。</summary>
    /// <param name="handle">锚点句柄。</param>
    /// <param name="anchor">返回锚点实体。</param>
    /// <param name="position">返回当前建筑位置。</param>
    /// <returns>锚点可承担网络功能时返回真。</returns>
    public static bool TryGetUsable(
        YuanshenAnchorHandle handle,
        out Entity anchor,
        out Vector2 position)
    {
        position = default;
        if (!TryResolve(handle, out anchor) ||
            !anchor.TryGetComponent(out YuanshenAnchorState state) ||
            !TryGetMaterial(state, out Building building) || !IsMaterialOwnershipValid(state, building))
            return false;
        position = building.current_position;
        return true;
    }

    /// <summary>解析一枚仍可用且人物当前获授权的锚点。</summary>
    /// <param name="actor">网络使用者。</param>
    /// <param name="handle">待解析句柄。</param>
    /// <param name="anchor">返回锚点实体。</param>
    /// <param name="position">返回建筑位置。</param>
    /// <returns>设施有效且人物归属获授权时返回真。</returns>
    public static bool TryGetUsableAuthorized(
        ActorExtend actor,
        YuanshenAnchorHandle handle,
        out Entity anchor,
        out Vector2 position)
    {
        return TryGetUsable(handle, out anchor, out position) &&
               IsAuthorized(actor, anchor.GetComponent<YuanshenAnchorState>());
    }

    /// <summary>在明确点选建筑上解析人物当前获授权的锚点。</summary>
    /// <param name="actor">网络使用者。</param>
    /// <param name="point">明确点选位置。</param>
    /// <param name="handle">返回锚点句柄。</param>
    /// <param name="anchor">返回锚点实体。</param>
    /// <returns>点选建筑确实承载授权锚点时返回真。</returns>
    public static bool TryGetAuthorizedAtPoint(
        ActorExtend actor,
        Vector2 point,
        out YuanshenAnchorHandle handle,
        out Entity anchor)
    {
        handle = default;
        anchor = default;
        return TryGetAnchorAtPoint(point, out handle, out anchor) &&
               TryGetUsableAuthorized(actor, handle, out anchor, out _);
    }

    /// <summary>统计人物当前仍有效的本人设施锚点。</summary>
    /// <param name="actor">设施锚点所有者。</param>
    /// <returns>有效设施数量，最高为二。</returns>
    public static int CountOwnedFacilities(ActorExtend actor)
    {
        return TryPruneOwned(actor, out List<YuanshenAnchorHandle> owned) ? owned.Count : 0;
    }

    /// <summary>按人物已有设施与明确连接计算有上限的持续神识负荷比例。</summary>
    /// <param name="actor">设施网络所属人物。</param>
    /// <returns>设施与连接合计负荷比例，最高三成二。</returns>
    public static float ResolveOwnedNetworkLoadRatio(ActorExtend actor)
    {
        if (actor == null || !actor.TryGetComponent(out YuanshenAnchorNetworkRuntime network) ||
            network.owned_anchors == null) return 0f;
        float ratio = 0f;
        int facilities = Mathf.Min(MaximumOwnedFacilities, network.owned_anchors.Count);
        for (var i = 0; i < facilities; i++)
        {
            if (!TryGetUsableAuthorized(actor, network.owned_anchors[i], out Entity anchor, out _)) continue;
            ratio += 0.08f;
            if (!anchor.TryGetComponent(out YuanshenAnchorState anchorState) || anchorState.link_handles == null) continue;
            ratio += Mathf.Min(MaximumLinksPerAnchor, anchorState.link_handles.Count) * 0.02f;
        }
        return Mathf.Min(0.32f, ratio);
    }

    /// <summary>判断一枚具体节点是否处于其驻留设施或人物其他合法锚点牵引内。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="node">待检查节点。</param>
    /// <param name="point">待检查位置。</param>
    /// <returns>节点驻留设施或人物锚点覆盖该点时返回真。</returns>
    public static bool IsNodeWithinTether(ActorExtend actor, Entity node, Vector2 point)
    {
        if (!node.IsNull && node.TryGetComponent(out YuanshenAnchorResidence residence) &&
            TryGetUsableAuthorized(actor, residence.anchor, out _, out Vector2 anchorPosition) &&
            Vector2.Distance(anchorPosition, point) <= TetherRadius) return true;
        return YuanshenArtifactAnchorService.IsWithinTether(actor, point);
    }

    /// <summary>判断位置是否处于人物当前授权且可用的设施网络牵引内。</summary>
    /// <param name="actor">网络使用者。</param>
    /// <param name="point">待检查位置。</param>
    /// <returns>本人有界锚点或已明确驻留锚点覆盖该点时返回真。</returns>
    public static bool IsWithinAuthorizedNetwork(ActorExtend actor, Vector2 point)
    {
        if (actor == null) return false;
        if (TryPruneOwned(actor, out List<YuanshenAnchorHandle> owned))
        {
            for (var i = 0; i < owned.Count; i++)
                if (TryGetUsableAuthorized(actor, owned[i], out _, out Vector2 position) &&
                    Vector2.Distance(position, point) <= TetherRadius) return true;
        }
        if (YuanshenThoughtService.TryGetFocused(actor, out YuanshenNodeHandle focused, out _) &&
            YuanshenNodeLockService.TryResolve(focused, out Entity node) &&
            node.TryGetComponent(out YuanshenAnchorResidence residence) &&
            TryGetUsableAuthorized(actor, residence.anchor, out _, out Vector2 focusedAnchor) &&
            Vector2.Distance(focusedAnchor, point) <= TetherRadius) return true;
        return false;
    }

    /// <summary>判断节点所在锚点能否通过明确连接抵达目标锚点。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="nodePosition">节点当前位置。</param>
    /// <param name="destination">目标授权锚点。</param>
    /// <param name="source">返回明确起点锚点；从人物主位置出发时为空。</param>
    /// <returns>起点、终点和连接均有效时返回真。</returns>
    public static bool CanTransit(
        ActorExtend actor,
        Vector2 nodePosition,
        YuanshenAnchorHandle destination,
        out YuanshenAnchorHandle source)
    {
        source = default;
        if (!TryGetUsableAuthorized(actor, destination, out _, out _)) return false;
        if (destination.OwnerActorId == actor.Base.data.id &&
            YuanshenTravelService.TryGetMainSoulPosition(actor, out Vector3 mainPosition) &&
            Vector2.Distance(nodePosition, mainPosition) <= PresenceRange) return true;
        if (!TryGetAnchorNearPosition(actor, nodePosition, PresenceRange, out source, out Entity sourceEntity) ||
            source == destination) return source == destination;
        return HasDirectLink(sourceEntity, destination);
    }

    /// <summary>判断一处锚点当前容量能否再承载指定心神份额。</summary>
    /// <param name="handle">目标锚点。</param>
    /// <param name="share">计划增加的份额。</param>
    /// <returns>锚点可用且剩余容量足够时返回真。</returns>
    public static bool CanAcceptLoad(YuanshenAnchorHandle handle, float share)
    {
        return share >= 0f && TryGetUsable(handle, out Entity anchor, out _) &&
               anchor.TryGetComponent(out YuanshenAnchorState state) &&
               state.current_load + share <= state.load_capacity + 0.001f;
    }

    /// <summary>把一枚节点的锚点负载从旧设施切换到新设施。</summary>
    /// <param name="node">需要更新驻留关系的节点。</param>
    /// <param name="destination">新设施锚点。</param>
    /// <returns>目标容量允许并完成负载变更时返回真。</returns>
    public static bool TrySetResidence(Entity node, YuanshenAnchorHandle destination)
    {
        if (node.IsNull || !node.TryGetComponent(out YuanshenNodeState nodeState) ||
            !TryGetUsable(destination, out Entity target, out _) ||
            !target.TryGetComponent(out YuanshenAnchorState targetState)) return false;
        YuanshenAnchorHandle previous = node.TryGetComponent(out YuanshenAnchorResidence current)
            ? current.anchor
            : default;
        if (previous == destination) return true;
        float share = Mathf.Max(0f, nodeState.mind_share);
        if (targetState.current_load + share > targetState.load_capacity + 0.001f) return false;
        ReleaseResidence(node);
        ref YuanshenAnchorState destinationState = ref target.GetComponent<YuanshenAnchorState>();
        destinationState.current_load = Mathf.Min(destinationState.load_capacity,
            destinationState.current_load + share);
        var residence = new YuanshenAnchorResidence
        {
            anchor = destination,
            reserved_load = share
        };
        if (node.HasComponent<YuanshenAnchorResidence>()) node.GetComponent<YuanshenAnchorResidence>() = residence;
        else node.AddComponent(residence);
        return true;
    }

    /// <summary>释放一枚节点当前占用的锚点容量。</summary>
    /// <param name="node">节点实体。</param>
    public static void ReleaseResidence(Entity node)
    {
        if (node.IsNull || !node.TryGetComponent(out YuanshenAnchorResidence residence)) return;
        float share = Mathf.Max(0f, residence.reserved_load);
        if (TryResolve(residence.anchor, out Entity anchor) && anchor.HasComponent<YuanshenAnchorState>())
        {
            ref YuanshenAnchorState state = ref anchor.GetComponent<YuanshenAnchorState>();
            state.current_load = Mathf.Max(0f, state.current_load - share);
        }
        node.RemoveComponent<YuanshenAnchorResidence>();
    }

    /// <summary>消耗指定锚点的有限愿力，为显圣维持提供折扣。</summary>
    /// <param name="handle">愿力来源锚点。</param>
    /// <param name="requested">希望消耗的愿力。</param>
    /// <param name="consumed">返回实际消耗量。</param>
    /// <returns>至少消耗一点愿力时返回真。</returns>
    public static bool TryConsumeIncense(
        YuanshenAnchorHandle handle,
        float requested,
        out float consumed)
    {
        consumed = 0f;
        if (requested <= 0f || !TryGetUsable(handle, out Entity anchor, out _) ||
            !anchor.TryGetComponent(out YuanshenAnchorState state) ||
            state.kind != YuanshenAnchorKind.CityAltar) return false;
        consumed = Mathf.Min(state.incense, requested);
        state.incense -= consumed;
        return consumed > 0f;
    }

    /// <summary>解析无身元神当前所在地得到的有限香火恢复倍率。</summary>
    /// <param name="actor">正在恢复的元神人物。</param>
    /// <returns>一至一点五之间的恢复倍率。</returns>
    public static float ResolveRecoveryMultiplier(ActorExtend actor)
    {
        if (actor == null || !YuanshenLifecycleService.IsBodiless(actor) ||
            !TryPruneOwned(actor, out List<YuanshenAnchorHandle> owned)) return 1f;
        for (var i = 0; i < owned.Count; i++)
        {
            if (!TryGetUsable(owned[i], out Entity anchor, out Vector2 position) ||
                !anchor.TryGetComponent(out YuanshenAnchorState state) ||
                state.kind != YuanshenAnchorKind.CityAltar ||
                Vector2.Distance(actor.Base.current_position, position) > PresenceRange) continue;
            float consumed = Mathf.Min(0.02f, state.incense);
            state.incense -= consumed;
            return 1f + consumed * 25f;
        }
        return 1f;
    }

    /// <summary>取得人物第一处仍可用的本人设施，供受限人工智能使用。</summary>
    /// <param name="actor">设施设立者。</param>
    /// <param name="handle">返回稳定句柄。</param>
    /// <param name="position">返回设施位置。</param>
    /// <returns>至少存在一处设施时返回真。</returns>
    public static bool TryGetFirstOwned(
        ActorExtend actor,
        out YuanshenAnchorHandle handle,
        out Vector2 position)
    {
        handle = default;
        position = default;
        if (!TryPruneOwned(actor, out List<YuanshenAnchorHandle> owned)) return false;
        for (var i = 0; i < owned.Count; i++)
        {
            if (!TryGetUsableAuthorized(actor, owned[i], out _, out position)) continue;
            handle = owned[i];
            return true;
        }
        return false;
    }

    /// <summary>取得人物近期实际受袭的本人设施。</summary>
    /// <param name="actor">设施设立者。</param>
    /// <param name="maximumAge">允许距今的最长世界秒数。</param>
    /// <param name="handle">返回设施句柄。</param>
    /// <param name="position">返回设施位置。</param>
    /// <returns>有一处可用设施近期受损时返回真。</returns>
    public static bool TryGetRecentlyAttackedOwned(
        ActorExtend actor,
        double maximumAge,
        out YuanshenAnchorHandle handle,
        out Vector2 position)
    {
        handle = default;
        position = default;
        if (!TryPruneOwned(actor, out List<YuanshenAnchorHandle> owned)) return false;
        double threshold = Now - Math.Max(0d, maximumAge);
        for (var i = 0; i < owned.Count; i++)
        {
            if (!TryGetUsableAuthorized(actor, owned[i], out Entity anchor, out position) ||
                !anchor.TryGetComponent(out YuanshenAnchorState state) || state.last_attacked_at < threshold)
                continue;
            handle = owned[i];
            return true;
        }
        return false;
    }

    /// <summary>低频同步物质建筑位置、受损、归属和失效状态。</summary>
    /// <param name="handle">待校验锚点。</param>
    /// <returns>物质建筑仍存在且归属有效时返回真。</returns>
    public static bool UpdateMaterialState(YuanshenAnchorHandle handle)
    {
        if (!TryResolve(handle, out Entity anchor) ||
            !anchor.TryGetComponent(out YuanshenAnchorState state) ||
            !TryGetMaterial(state, out Building building) || !IsMaterialOwnershipValid(state, building))
        {
            Collapse(handle, true);
            return false;
        }
        if (anchor.TryGetComponent(out Position _))
            anchor.GetComponent<Position>().v2 = building.current_position;
        float health = building.getHealth();
        if (state.last_building_health > 0f && health + 0.01f < state.last_building_health)
            state.last_attacked_at = Now;
        state.last_building_health = health;
        return true;
    }

    /// <summary>收集当前全部已登记锚点句柄，供低频系统逐项推进。</summary>
    /// <param name="output">接收稳定句柄的集合。</param>
    public static void CollectRegistered(ICollection<YuanshenAnchorHandle> output)
    {
        if (output == null) return;
        foreach (YuanshenAnchorHandle handle in Anchors.Keys) output.Add(handle);
    }

    /// <summary>设施物质毁灭或主动解除后拆除连接、记录和渲染实体。</summary>
    /// <param name="handle">待拆除锚点。</param>
    /// <param name="backlash">是否对设立者施加锚点毁灭反噬。</param>
    public static void Collapse(YuanshenAnchorHandle handle, bool backlash)
    {
        if (!TryResolve(handle, out Entity anchor)) return;
        YuanshenAnchorState state = anchor.GetComponent<YuanshenAnchorState>();
        if (state.link_handles != null)
        {
            for (var i = 0; i < state.link_handles.Count; i++)
                RemoveLink(state.link_handles[i], handle);
        }
        var residents = new List<Entity>();
        ModClass.I.W.Query<YuanshenAnchorResidence>().ForEachEntity((
            ref YuanshenAnchorResidence residence,
            Entity node) =>
        {
            if (!node.Tags.Has<TagRecycle>() && residence.anchor == handle) residents.Add(node);
        });
        for (var i = 0; i < residents.Count; i++)
        {
            Entity resident = residents[i];
            if (!resident.TryGetComponent(out YuanshenNodeState nodeState)) continue;
            Actor nodeOwner = World.world?.units?.get(nodeState.owner_actor_id);
            if (nodeOwner == null || nodeOwner.isRekt())
            {
                ReleaseResidence(resident);
                YuanshenNodeLockService.UnregisterNode(resident);
                if (!resident.Tags.Has<TagRecycle>()) resident.AddTag<TagRecycle>();
                continue;
            }
            YuanshenAdvancedNodeService.Disperse(nodeOwner.GetExtend(), resident, backlash ? 0.25f : 0f);
        }
        Actor ownerBase = World.world?.units?.get(state.owner_actor_id);
        if (ownerBase != null && !ownerBase.isRekt())
        {
            ActorExtend owner = ownerBase.GetExtend();
            if (owner.TryGetComponent(out YuanshenAnchorNetworkRuntime _))
            {
                ref YuanshenAnchorNetworkRuntime network = ref owner.GetComponent<YuanshenAnchorNetworkRuntime>();
                RemoveHandle(network.owned_anchors, handle);
                if (network.selected_anchor == handle) network.selected_anchor = default;
            }
            if (backlash) YuanshenTravelService.LockMainMindShare(owner, 5f);
            YuanshenTravelService.NotifyMindStateChanged(owner);
        }
        Anchors.Remove(handle);
        BuildingAnchors.Remove(state.building_id);
        if (!anchor.Tags.Has<TagRecycle>()) anchor.AddTag<TagRecycle>();
    }

    /// <summary>世界切换时清空全部运行时锚点解析。</summary>
    private static void ClearWorldState()
    {
        Anchors.Clear();
        BuildingAnchors.Clear();
    }

    /// <summary>判断人物是否具备管理高阶锚点网络的元神层数。</summary>
    private static bool CanManageNetwork(ActorExtend actor)
    {
        return YuanshenNodeCombatService.CanUseSoulAbilities(actor) &&
               actor.TryGetComponent(out Yuanshen yuanshen) && yuanshen.stage >= 7;
    }

    /// <summary>解析明确建筑能否作为指定设施，并返回集体编号。</summary>
    private static bool TryResolveCollective(
        ActorExtend actor,
        Building building,
        YuanshenAnchorKind kind,
        out long collectiveId)
    {
        collectiveId = 0L;
        if (building == null || building.isRekt() || !building.isAlive() || building.isUnderConstruction() ||
            building.asset == null || building.data == null) return false;
        if (kind == YuanshenAnchorKind.SectPlatform)
        {
            Sect sect = actor.sect;
            if (sect == null || sect.isRekt() || !building.asset.IsSectBuilding()) return false;
            building.data.get(BuildingDataKeys.SectID_Long, out long buildingSectId, -1L);
            if (buildingSectId != sect.getID()) return false;
            collectiveId = buildingSectId;
            return true;
        }
        City city = actor.Base.city;
        if (city == null || city.isRekt() || building.city != city || !IsTemple(building)) return false;
        collectiveId = city.data.id;
        return true;
    }

    /// <summary>判断锚点物质建筑当前是否仍属于建立时集体。</summary>
    private static bool IsMaterialOwnershipValid(in YuanshenAnchorState identity, Building building)
    {
        if (building == null || building.isRekt() || !building.isAlive() || building.isUnderConstruction())
            return false;
        if (identity.kind == YuanshenAnchorKind.SectPlatform)
        {
            building.data.get(BuildingDataKeys.SectID_Long, out long sectId, -1L);
            return sectId == identity.collective_id && building.asset != null && building.asset.IsSectBuilding();
        }
        return building.city != null && !building.city.isRekt() &&
               building.city.data.id == identity.collective_id && IsTemple(building);
    }

    /// <summary>判断人物当前身份是否获得锚点集体授权。</summary>
    private static bool IsAuthorized(ActorExtend actor, in YuanshenAnchorState identity)
    {
        if (actor == null || actor.Base == null || actor.Base.isRekt()) return false;
        if (identity.owner_actor_id == actor.Base.data.id) return true;
        return identity.kind switch
        {
            YuanshenAnchorKind.SectPlatform => actor.sect != null && !actor.sect.isRekt() &&
                                               actor.sect.getID() == identity.collective_id,
            YuanshenAnchorKind.CityAltar => actor.Base.city != null && !actor.Base.city.isRekt() &&
                                            actor.Base.city.data.id == identity.collective_id,
            _ => false
        };
    }

    /// <summary>从明确地面点直接取得承载建筑，不遍历其他设施。</summary>
    private static bool TryGetBuildingAt(Vector2 point, out Building building)
    {
        building = null;
        WorldTile tile = World.world?.GetTileSimple(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y));
        if (tile == null || !tile.hasBuilding() || tile.building == null || tile.building.isRekt()) return false;
        building = tile.building;
        return true;
    }

    /// <summary>从明确点选建筑的一对一映射取得锚点。</summary>
    private static bool TryGetAnchorAtPoint(
        Vector2 point,
        out YuanshenAnchorHandle handle,
        out Entity anchor)
    {
        handle = default;
        anchor = default;
        return TryGetBuildingAt(point, out Building building) &&
               BuildingAnchors.TryGetValue(building.data.id, out handle) && TryResolve(handle, out anchor);
    }

    /// <summary>在一枚节点当前位置附近验证授权锚点，不把结果写入敌方发现记录。</summary>
    private static bool TryGetAnchorNearPosition(
        ActorExtend actor,
        Vector2 position,
        float radius,
        out YuanshenAnchorHandle handle,
        out Entity anchor)
    {
        handle = default;
        anchor = default;
        WorldTile tile = World.world?.GetTileSimple(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
        if (tile?.building != null &&
            BuildingAnchors.TryGetValue(tile.building.data.id, out YuanshenAnchorHandle exact) &&
            TryGetUsableAuthorized(actor, exact, out anchor, out Vector2 exactPosition) &&
            Vector2.Distance(position, exactPosition) <= radius)
        {
            handle = exact;
            return true;
        }
        if (!TryPruneOwned(actor, out List<YuanshenAnchorHandle> owned)) return false;
        float best = Mathf.Max(0f, radius);
        for (var i = 0; i < owned.Count; i++)
        {
            if (!TryGetUsableAuthorized(actor, owned[i], out Entity candidate, out Vector2 candidatePosition))
                continue;
            float distance = Vector2.Distance(position, candidatePosition);
            if (distance > best) continue;
            best = distance;
            handle = owned[i];
            anchor = candidate;
        }
        return handle.IsValid;
    }

    /// <summary>判断人物或当前聚焦分念是否明确抵达目标位置。</summary>
    /// <param name="actor">需要检查的人物。</param>
    /// <param name="position">目标世界位置。</param>
    /// <returns>命魂或聚焦节点在到场距离内时返回真。</returns>
    internal static bool HasPresenceNear(ActorExtend actor, Vector2 position)
    {
        if (actor == null) return false;
        if (YuanshenTravelService.TryGetMainSoulPosition(actor, out Vector3 mainPosition) &&
            Vector2.Distance(mainPosition, position) <= PresenceRange) return true;
        return YuanshenThoughtService.TryGetFocused(actor, out _, out Vector2 focusedPosition) &&
               Vector2.Distance(focusedPosition, position) <= PresenceRange;
    }

    /// <summary>读取锚点的物质建筑。</summary>
    private static bool TryGetMaterial(in YuanshenAnchorState identity, out Building building)
    {
        building = identity.building_id > 0L ? World.world?.buildings?.get(identity.building_id) : null;
        return building != null && !building.isRekt() && building.isAlive() && building.data != null;
    }

    /// <summary>判断现有建筑是否为城市香火坛允许使用的庙宇。</summary>
    private static bool IsTemple(Building building)
    {
        string id = building?.asset?.id;
        string type = building?.asset?.type;
        return !string.IsNullOrEmpty(id) && id.IndexOf("temple", StringComparison.OrdinalIgnoreCase) >= 0 ||
               !string.IsNullOrEmpty(type) && type.IndexOf("temple", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>整理人物本人锚点列表并返回可修改集合。</summary>
    private static bool TryPruneOwned(ActorExtend actor, out List<YuanshenAnchorHandle> owned)
    {
        owned = null;
        if (actor == null) return false;
        ref YuanshenAnchorNetworkRuntime network = ref actor.GetOrAddComponent<YuanshenAnchorNetworkRuntime>();
        network.owned_anchors ??= new List<YuanshenAnchorHandle>(MaximumOwnedFacilities);
        for (var i = network.owned_anchors.Count - 1; i >= 0; i--)
            if (!TryResolve(network.owned_anchors[i], out _)) network.owned_anchors.RemoveAt(i);
        owned = network.owned_anchors;
        return true;
    }

    /// <summary>判断节点还能追加一条指定连接。</summary>
    private static bool CanAddLink(Entity anchor, YuanshenAnchorHandle target)
    {
        if (!anchor.TryGetComponent(out YuanshenAnchorState state)) return false;
        state.link_handles ??= new List<YuanshenAnchorHandle>(2);
        for (var i = 0; i < state.link_handles.Count; i++)
            if (state.link_handles[i] == target) return true;
        return state.link_handles.Count < MaximumLinksPerAnchor;
    }

    /// <summary>向锚点追加一条不重复的连接。</summary>
    private static void AddLink(Entity anchor, YuanshenAnchorHandle target)
    {
        ref YuanshenAnchorState state = ref anchor.GetComponent<YuanshenAnchorState>();
        state.link_handles ??= new List<YuanshenAnchorHandle>(2);
        for (var i = 0; i < state.link_handles.Count; i++)
            if (state.link_handles[i] == target) return;
        state.link_handles.Add(target);
    }

    /// <summary>判断锚点是否与目标直接连接。</summary>
    private static bool HasDirectLink(Entity anchor, YuanshenAnchorHandle target)
    {
        if (!anchor.TryGetComponent(out YuanshenAnchorState state) || state.link_handles == null) return false;
        for (var i = 0; i < state.link_handles.Count; i++)
            if (state.link_handles[i] == target && TryResolve(target, out _)) return true;
        return false;
    }

    /// <summary>从指定锚点删除一条反向连接。</summary>
    private static void RemoveLink(YuanshenAnchorHandle anchorHandle, YuanshenAnchorHandle target)
    {
        if (!TryResolve(anchorHandle, out Entity anchor) ||
            !anchor.TryGetComponent(out YuanshenAnchorState state) || state.link_handles == null) return;
        for (var i = state.link_handles.Count - 1; i >= 0; i--)
            if (state.link_handles[i] == target) state.link_handles.RemoveAt(i);
    }

    /// <summary>从句柄列表移除指定项。</summary>
    private static void RemoveHandle(List<YuanshenAnchorHandle> handles, YuanshenAnchorHandle target)
    {
        if (handles == null) return;
        for (var i = handles.Count - 1; i >= 0; i--)
            if (handles[i] == target) handles.RemoveAt(i);
    }

    /// <summary>真实保护击杀为附近本人香火坛增加有容量、递减的愿力。</summary>
    private static void OnProtectionKill(ActorExtend killer, Actor victim, Kingdom victimKingdom)
    {
        if (killer == null || victim == null ||
            !TryPruneOwned(killer, out List<YuanshenAnchorHandle> owned)) return;
        for (var i = 0; i < owned.Count; i++)
        {
            if (!TryGetUsable(owned[i], out Entity anchor, out Vector2 position) ||
                !anchor.TryGetComponent(out YuanshenAnchorState state) ||
                state.kind != YuanshenAnchorKind.CityAltar ||
                Vector2.Distance(victim.current_position, position) > 48f) continue;
            City city = World.world?.cities?.get(state.collective_id);
            if (city == null || city.isRekt() || city.kingdom == null || victimKingdom == null ||
                !city.kingdom.isEnemy(victimKingdom) || !IsAuthorized(killer, state)) continue;
            if (state.last_attacked_at < Now - 2d * TimeScales.SecPerMonth) continue;
            float missing = Mathf.Max(0f, state.incense_capacity - state.incense);
            float gain = Mathf.Min(6f, 1f + Mathf.Sqrt(Mathf.Max(0f, victim.getMaxHealth())) * 0.05f);
            gain = Mathf.Min(gain, missing * 0.2f + 0.25f);
            state.incense = Mathf.Min(state.incense_capacity, state.incense + gain);
        }
    }

    /// <summary>当前世界时间。</summary>
    private static double Now => World.world?.getCurWorldTime() ?? 0d;

    /// <summary>世界切换时清空锚点静态解析的内嵌系统。</summary>
    private sealed class ClearStateSystem : BaseSystem, IWorldStateClearable
    {
        /// <summary>世界切换时清空全部锚点解析映射。</summary>
        void IWorldStateClearable.ClearWorldState()
        {
            ClearWorldState();
        }
    }
}
