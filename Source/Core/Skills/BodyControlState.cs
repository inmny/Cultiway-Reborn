using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>一次实际取得的身体部位；重复释放或旧世界释放不能影响其他施放。</summary>
public sealed class BodyControlLease : IDisposable
{
    /// <summary>原人物稳定编号。</summary>
    internal readonly long Actor;

    /// <summary>本次实际取得的部位。</summary>
    public BodyOccupancy Parts { get; }

    /// <summary>本次实际独立器官或内部部件的稳定路径，普通人形部位为空。</summary>
    public string PartPath { get; }

    /// <summary>同一身体并行肢体共用的朝向，单手瞄准不另转动另一套身体。</summary>
    public Vector3 Forward { get; }

    /// <summary>本次是否由共同支撑过程推进实际离地高度。</summary>
    public bool ControlsHeight { get; }

    /// <summary>由主线程成功取得控制时建立。</summary>
    /// <param name="actor">原人物编号。</param><param name="parts">实际部位。</param>
    /// <param name="forward">本组身体的共同朝向。</param><param name="controlsHeight">是否控制离地高度。</param>
    /// <param name="partPath">实际独立部位路径。</param>
    internal BodyControlLease(long actor, BodyOccupancy parts, Vector3 forward, bool controlsHeight, string partPath)
    {
        Actor = actor;
        Parts = parts;
        PartPath = partPath;
        Forward = forward;
        ControlsHeight = controlsHeight;
    }

    /// <summary>回调之后仍须核对同一原凭据，不能替代后来取得的控制。</summary>
    internal bool IsHeld => BodyControlState.Owns(this);

    /// <summary>主线程只释放本次仍持有的控制，不销毁身体或持有物。</summary>
    public void Dispose() => BodyControlState.Release(this);
}

/// <summary>主线程取得及释放控制，其他线程只读取已发布的不可变占用集合。</summary>
public static class BodyControlState
{
    /// <summary>数组发布后不改动，所有者凭据互不替代。</summary>
    private static readonly ConcurrentDictionary<long, BodyControlLease[]> occupied = new();

    /// <summary>并行的下一只手沿用正在控制的身体朝向，瞄准仍由各自路径决定。</summary>
    /// <param name="actor">真实身体编号。</param><param name="fallback">首个动作的朝向。</param>
    public static Vector3 Facing(long actor, Vector3 fallback) =>
        occupied.TryGetValue(actor, out BodyControlLease[] leases) ? leases[0].Forward : fallback;

    /// <summary>查询需要的部位是否正被其他动作使用。</summary>
    /// <param name="actor">人物稳定编号。</param><param name="parts">需要的部位。</param>
    /// <param name="partPath">实际独立部位路径。</param>
    public static bool IsBusy(long actor,
        BodyOccupancy parts = BodyOccupancy.RightArm | BodyOccupancy.LeftArm | BodyOccupancy.Stance,
        string partPath = null)
    {
        if (!occupied.TryGetValue(actor, out BodyControlLease[] leases)) return false;
        foreach (BodyControlLease lease in leases)
            if (Conflicts(lease.Parts, lease.PartPath, parts, partPath))
                return true;
        return false;
    }

    /// <summary>读取实际已经取得的部位；查询支撑时不会把局部手臂动作误判成全身停步。</summary>
    /// <param name="actor">人物稳定编号。</param><param name="parts">要读取的实际部位。</param>
    public static bool HasControl(long actor, BodyOccupancy parts)
    {
        if (!occupied.TryGetValue(actor, out BodyControlLease[] leases)) return false;
        foreach (BodyControlLease lease in leases)
            if ((lease.Parts & parts) != 0)
                return true;
        return false;
    }

    /// <summary>原版下落只让位给实际取得高度控制的动作，普通原地动作仍保留原下落。</summary>
    /// <param name="actor">实际人物编号。</param>
    public static bool ControlsHeight(long actor) => occupied.TryGetValue(actor, out BodyControlLease[] leases) &&
                                                     Array.Exists(leases, lease => lease.ControlsHeight);

    /// <summary>主线程为本次施放取得部位，冲突时不改变已有控制。</summary>
    /// <param name="actor">人物稳定编号。</param><param name="parts">本次部位。</param>
    /// <param name="forward">首个动作的身体朝向，缺省向地图横向。</param>
    /// <param name="controlsHeight">本次是否实际接管支撑过程中的高度。</param>
    /// <param name="partPath">实际独立部位路径，整器官与其内部部件互相冲突。</param>
    public static BodyControlLease TryAcquire(long actor, BodyOccupancy parts, Vector3? forward = null,
        bool controlsHeight = false, string partPath = null)
    {
        occupied.TryGetValue(actor, out BodyControlLease[] current);
        if (current != null)
            foreach (BodyControlLease held in current)
                if (Conflicts(held.Parts, held.PartPath, parts, partPath))
                    return null;
        var lease = new BodyControlLease(actor, parts, Facing(actor, forward ?? Vector3.right), controlsHeight, partPath);
        if (parts == BodyOccupancy.None && partPath == null) return lease;
        var next = new BodyControlLease[(current?.Length ?? 0) + 1];
        if (current != null) Array.Copy(current, next, current.Length);
        next[next.Length - 1] = lease;
        occupied[actor] = next;
        return lease;
    }

    /// <summary>先核对共用肢体与全身，再按真实路径核对同一器官及其父子部件。</summary>
    /// <param name="left">已有肢体。</param><param name="leftPath">已有独立部位。</param>
    /// <param name="right">请求肢体。</param><param name="rightPath">请求独立部位。</param>
    private static bool Conflicts(BodyOccupancy left, string leftPath, BodyOccupancy right, string rightPath) =>
        (ControlledParts(left) & ControlledParts(right)) != 0 ||
        leftPath != null && (right & BodyOccupancy.Stance) != 0 ||
        rightPath != null && (left & BodyOccupancy.Stance) != 0 ||
        leftPath != null && rightPath != null && (leftPath == rightPath ||
            ChildOf(leftPath, rightPath) || ChildOf(rightPath, leftPath));

    /// <summary>只认完整路径段，不把相近器官名称当成同一部位，也不在查询时拼接字符串。</summary>
    /// <param name="child">可能的子部件。</param><param name="parent">可能的完整器官或父部件。</param>
    private static bool ChildOf(string child, string parent) => child.Length > parent.Length &&
        child[parent.Length] == '/' && child.StartsWith(parent, StringComparison.Ordinal);

    /// <summary>全身支撑会带动两侧肢体，不能与独立肢体动作争用同一个身体。</summary>
    /// <param name="parts">动作声明的部位。</param>
    private static BodyOccupancy ControlledParts(BodyOccupancy parts) =>
        (parts & BodyOccupancy.Stance) != 0
            ? BodyOccupancy.Stance | BodyOccupancy.LeftArm | BodyOccupancy.RightArm | BodyOccupancy.Head
            : parts;

    /// <summary>按原凭据身份核对仍实际持有，不用人物忙碌冒充原控制还在。</summary>
    /// <param name="lease">原本实际取得的凭据。</param>
    internal static bool Owns(BodyControlLease lease) => occupied.TryGetValue(lease.Actor, out BodyControlLease[] held) &&
        Array.IndexOf(held, lease) >= 0;

    /// <summary>只移除本次凭据；旧世界及已释放凭据均不会命中后来的占用。</summary>
    /// <param name="lease">实际取得者。</param>
    internal static void Release(BodyControlLease lease)
    {
        if (!occupied.TryGetValue(lease.Actor, out BodyControlLease[] current)) return;
        int index = Array.IndexOf(current, lease);
        if (index < 0) return;
        if (current.Length == 1)
        {
            occupied.TryRemove(lease.Actor, out _);
            return;
        }

        var next = new BodyControlLease[current.Length - 1];
        Array.Copy(current, 0, next, 0, index);
        Array.Copy(current, index + 1, next, index, current.Length - index - 1);
        occupied[lease.Actor] = next;
    }

    /// <summary>世界结束时清除全部占用，旧凭据失去释放新动作的能力。</summary>
    public static void Clear() => occupied.Clear();
}
