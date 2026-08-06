using System;
using UnityEngine;

namespace Cultiway.Core.Coordination;

/// <summary>位置订单采用的锚点类型。</summary>
public enum CoordinationAnchorKind
{
    /// <summary>没有移动要求。</summary>
    None,

    /// <summary>以固定地块为锚点。</summary>
    Tile,

    /// <summary>以另一个角色的当前位置为动态锚点。</summary>
    Actor
}

/// <summary>可比较的位置订单；服务仅在订单变化后重新请求路径。</summary>
public readonly struct CoordinationPlacementOrder : IEquatable<CoordinationPlacementOrder>
{
    /// <summary>创建一个完整的位置订单。</summary>
    public CoordinationPlacementOrder(
        CoordinationAnchorKind anchorKind,
        int tileId,
        long actorId,
        Vector2Int offset,
        float arrivalRadius,
        float repathDistance,
        bool holdPosition,
        bool suspendWhileInCombat)
    {
        AnchorKind = anchorKind;
        TileId = tileId;
        ActorId = actorId;
        Offset = offset;
        ArrivalRadius = Mathf.Max(0f, arrivalRadius);
        RepathDistance = Mathf.Max(0.25f, repathDistance);
        HoldPosition = holdPosition;
        SuspendWhileInCombat = suspendWhileInCombat;
    }

    /// <summary>订单锚点类型。</summary>
    public CoordinationAnchorKind AnchorKind { get; }

    /// <summary>固定地块 ID。</summary>
    public int TileId { get; }

    /// <summary>动态锚点角色 ID。</summary>
    public long ActorId { get; }

    /// <summary>相对锚点的整数地块偏移。</summary>
    public Vector2Int Offset { get; }

    /// <summary>进入该半径后视为到场。</summary>
    public float ArrivalRadius { get; }

    /// <summary>动态锚点移动超过该距离后才重建路径。</summary>
    public float RepathDistance { get; }

    /// <summary>到场后是否停止由当前订单产生的移动。</summary>
    public bool HoldPosition { get; }

    /// <summary>角色处于战斗任务时是否暂停该位置订单。</summary>
    public bool SuspendWhileInCombat { get; }

    /// <summary>创建一个没有移动约束的订单。</summary>
    public static CoordinationPlacementOrder None => new(
        CoordinationAnchorKind.None,
        -1,
        0,
        default,
        0f,
        1f,
        false,
        true);

    /// <summary>创建一个固定地块订单。</summary>
    public static CoordinationPlacementOrder AtTile(
        int tileId,
        Vector2Int offset,
        float arrivalRadius,
        bool holdPosition = true,
        bool suspendWhileInCombat = true)
    {
        return new CoordinationPlacementOrder(
            CoordinationAnchorKind.Tile,
            tileId,
            0,
            offset,
            arrivalRadius,
            1f,
            holdPosition,
            suspendWhileInCombat);
    }

    /// <summary>创建一个跟随角色锚点的动态订单。</summary>
    public static CoordinationPlacementOrder FollowActor(
        long actorId,
        Vector2Int offset,
        float arrivalRadius,
        float repathDistance = 1.5f,
        bool suspendWhileInCombat = true)
    {
        return new CoordinationPlacementOrder(
            CoordinationAnchorKind.Actor,
            -1,
            actorId,
            offset,
            arrivalRadius,
            repathDistance,
            false,
            suspendWhileInCombat);
    }

    /// <inheritdoc />
    public bool Equals(CoordinationPlacementOrder other)
    {
        return AnchorKind == other.AnchorKind &&
               TileId == other.TileId &&
               ActorId == other.ActorId &&
               Offset == other.Offset &&
               Mathf.Approximately(ArrivalRadius, other.ArrivalRadius) &&
               Mathf.Approximately(RepathDistance, other.RepathDistance) &&
               HoldPosition == other.HoldPosition &&
               SuspendWhileInCombat == other.SuspendWhileInCombat;
    }

    /// <inheritdoc />
    public override bool Equals(object obj)
    {
        return obj is CoordinationPlacementOrder other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            (int)AnchorKind,
            TileId,
            ActorId,
            Offset,
            ArrivalRadius,
            RepathDistance,
            HoldPosition,
            SuspendWhileInCombat);
    }
}
