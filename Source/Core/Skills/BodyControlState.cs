using System;
using System.Collections.Concurrent;

namespace Cultiway.Core.Skills;

/// <summary>一次实际取得的身体部位；重复释放或旧世界释放不能影响其他施放。</summary>
public sealed class BodyControlLease : IDisposable
{
    /// <summary>原人物稳定编号。</summary>
    internal readonly long Actor;

    /// <summary>本次实际取得的部位。</summary>
    public BodyOccupancy Parts { get; }

    /// <summary>由主线程成功取得控制时建立。</summary>
    /// <param name="actor">原人物编号。</param><param name="parts">实际部位。</param>
    internal BodyControlLease(long actor, BodyOccupancy parts)
    {
        Actor = actor;
        Parts = parts;
    }

    /// <summary>主线程只释放本次仍持有的控制，不销毁身体或持有物。</summary>
    public void Dispose() => BodyControlState.Release(this);
}

/// <summary>主线程取得及释放控制，其他线程只读取已发布的不可变占用集合。</summary>
public static class BodyControlState
{
    /// <summary>数组发布后不改动，所有者凭据互不替代。</summary>
    private static readonly ConcurrentDictionary<long, BodyControlLease[]> occupied = new();

    /// <summary>查询需要的部位是否正被其他动作使用。</summary>
    /// <param name="actor">人物稳定编号。</param><param name="parts">需要的部位。</param>
    public static bool IsBusy(long actor,
        BodyOccupancy parts = BodyOccupancy.RightArm | BodyOccupancy.LeftArm | BodyOccupancy.Stance)
    {
        if (!occupied.TryGetValue(actor, out BodyControlLease[] leases)) return false;
        foreach (BodyControlLease lease in leases)
            if ((ControlledParts(lease.Parts) & ControlledParts(parts)) != 0)
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

    /// <summary>主线程为本次施放取得部位，冲突时不改变已有控制。</summary>
    /// <param name="actor">人物稳定编号。</param><param name="parts">本次部位。</param>
    public static BodyControlLease TryAcquire(long actor, BodyOccupancy parts)
    {
        occupied.TryGetValue(actor, out BodyControlLease[] current);
        if (current != null)
            foreach (BodyControlLease held in current)
                if ((ControlledParts(held.Parts) & ControlledParts(parts)) != 0)
                    return null;
        var lease = new BodyControlLease(actor, parts);
        if (parts == BodyOccupancy.None) return lease;
        var next = new BodyControlLease[(current?.Length ?? 0) + 1];
        if (current != null) Array.Copy(current, next, current.Length);
        next[next.Length - 1] = lease;
        occupied[actor] = next;
        return lease;
    }

    /// <summary>全身支撑会带动两侧肢体，不能与独立肢体动作争用同一个身体。</summary>
    /// <param name="parts">动作声明的部位。</param>
    private static BodyOccupancy ControlledParts(BodyOccupancy parts) =>
        (parts & BodyOccupancy.Stance) != 0
            ? BodyOccupancy.Stance | BodyOccupancy.LeftArm | BodyOccupancy.RightArm
            : parts;

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