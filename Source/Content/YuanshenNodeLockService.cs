using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>登记活动元神节点，并维护神魂接触产生的数秒目标锁定。</summary>
public static class YuanshenNodeLockService
{
    /// <summary>防止内嵌系统重复注册。</summary>
    private static bool initialized;

    /// <summary>注册驱动本服务的内嵌逻辑系统。</summary>
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        ModClass.I.GeneralLogicSystems.Add(new UpdateSystem());
    }

    /// <summary>一次明确神魂接触授予的锁定持续秒数。</summary>
    private const double LockDuration = 7d;

    /// <summary>单个人物最多同时持有的节点锁定。</summary>
    private const int MaximumLocksPerActor = 32;

    /// <summary>活动节点句柄到当前实体的解析表。</summary>
    private static readonly Dictionary<YuanshenNodeHandle, Entity> Nodes = new();

    /// <summary>人物编号到当前短时锁定的映射。</summary>
    private static readonly Dictionary<long, List<NodeLock>> Locks = new();

    /// <summary>低频清理空锁定集合时复用的人物编号。</summary>
    private static readonly List<long> EmptyLockOwners = new();

    /// <summary>节点创建后登记稳定句柄。</summary>
    /// <param name="node">刚创建的元神节点。</param>
    public static void RegisterNode(Entity node)
    {
        if (node.IsNull || !node.TryGetComponent(out YuanshenNodeState state)) return;
        Nodes[state.GetHandle()] = node;
    }

    /// <summary>节点归返或击破后移除稳定解析关系。</summary>
    /// <param name="node">即将失效的元神节点。</param>
    public static void UnregisterNode(Entity node)
    {
        if (node.IsNull || !node.TryGetComponent(out YuanshenNodeState state)) return;
        Nodes.Remove(state.GetHandle());
    }

    /// <summary>每次使用稳定句柄时重新校验全部身份字段。</summary>
    /// <param name="handle">需要解析的节点句柄。</param>
    /// <param name="node">返回仍然有效的当前实体。</param>
    /// <returns>句柄仍精确指向原节点时返回真。</returns>
    public static bool TryResolve(YuanshenNodeHandle handle, out Entity node)
    {
        node = default;
        if (!Nodes.TryGetValue(handle, out Entity candidate) || candidate.IsNull ||
            candidate.Tags.Has<TagRecycle>() ||
            !candidate.TryGetComponent(out YuanshenNodeState state) ||
            state.GetHandle() != handle)
        {
            Nodes.Remove(handle);
            return false;
        }
        node = candidate;
        return true;
    }

    /// <summary>由一次攻击或其他明确神魂接触授予短时节点锁定。</summary>
    /// <param name="holder">取得锁定的人物。</param>
    /// <param name="handle">被锁定节点的稳定句柄。</param>
    /// <returns>人物和节点均有效且锁定已经写入时返回真。</returns>
    public static bool GrantLock(Actor holder, YuanshenNodeHandle handle)
    {
        if (!CanHoldLocks(holder) || handle.OwnerActorId == holder.data.id || !TryResolve(handle, out _))
            return false;
        double now = Now;
        List<NodeLock> locks = GetOrCreate(holder.data.id);
        PurgeExpired(locks, now);
        for (var i = 0; i < locks.Count; i++)
        {
            if (locks[i].handle != handle) continue;
            locks[i] = new NodeLock(handle, now + LockDuration);
            return true;
        }
        if (locks.Count >= MaximumLocksPerActor)
        {
            int earliest = 0;
            for (var i = 1; i < locks.Count; i++)
                if (locks[i].expires_at < locks[earliest].expires_at) earliest = i;
            locks.RemoveAt(earliest);
        }
        locks.Add(new NodeLock(handle, now + LockDuration));
        return true;
    }

    /// <summary>从人物当前锁定中选择点击点附近的一枚活动节点。</summary>
    /// <param name="holder">持有锁定的人物。</param>
    /// <param name="point">玩家点击位置。</param>
    /// <param name="maximumDistance">允许偏离节点当前位置的距离。</param>
    /// <param name="handle">返回稳定节点句柄。</param>
    /// <returns>存在未过期锁定且节点仍有效时返回真。</returns>
    public static bool TryGetLockedNear(
        Actor holder,
        Vector2 point,
        float maximumDistance,
        out YuanshenNodeHandle handle)
    {
        handle = default;
        if (!TryGetLocks(holder, out List<NodeLock> locks)) return false;
        float bestDistance = Mathf.Max(0f, maximumDistance);
        bool found = false;
        for (var i = 0; i < locks.Count; i++)
        {
            if (!TryResolve(locks[i].handle, out Entity node) ||
                !node.TryGetComponent(out Position position)) continue;
            float distance = Vector2.Distance(point, position.v2);
            if (distance > bestDistance) continue;
            if (found && Mathf.Approximately(distance, bestDistance) &&
                CompareHandles(locks[i].handle, handle) >= 0) continue;
            found = true;
            bestDistance = distance;
            handle = locks[i].handle;
        }
        return found;
    }

    /// <summary>判断人物是否仍持有指定节点的短时锁定。</summary>
    /// <param name="holder">持有锁定的人物。</param>
    /// <param name="handle">需要确认的节点句柄。</param>
    /// <returns>锁定未过期且节点仍有效时返回真。</returns>
    public static bool HasLock(Actor holder, YuanshenNodeHandle handle)
    {
        if (!TryGetLocks(holder, out List<NodeLock> locks)) return false;
        for (var i = 0; i < locks.Count; i++)
            if (locks[i].handle == handle && TryResolve(handle, out _)) return true;
        return false;
    }

    /// <summary>收集人物当前仍有效的节点锁定。</summary>
    /// <param name="holder">持有锁定的人物。</param>
    /// <param name="output">接收节点句柄的集合。</param>
    public static void CollectLocks(Actor holder, ICollection<YuanshenNodeHandle> output)
    {
        if (output == null || !TryGetLocks(holder, out List<NodeLock> locks)) return;
        for (var i = 0; i < locks.Count; i++)
            if (TryResolve(locks[i].handle, out _)) output.Add(locks[i].handle);
    }

    /// <summary>低频清理全部过期锁定和已经失效的人物键。</summary>
    public static void PruneExpiredLocks()
    {
        double now = Now;
        EmptyLockOwners.Clear();
        foreach (KeyValuePair<long, List<NodeLock>> entry in Locks)
        {
            Actor holder = World.world?.units?.get(entry.Key);
            PurgeExpired(entry.Value, now);
            if (!CanHoldLocks(holder) || entry.Value.Count == 0) EmptyLockOwners.Add(entry.Key);
        }
        for (var i = 0; i < EmptyLockOwners.Count; i++) Locks.Remove(EmptyLockOwners[i]);
        EmptyLockOwners.Clear();
    }

    /// <summary>世界切换时清空全部节点解析和短时锁定。</summary>
    private static void ClearWorldState()
    {
        Nodes.Clear();
        Locks.Clear();
        EmptyLockOwners.Clear();
    }

    /// <summary>读取人物锁定并在查询时清理过期项。</summary>
    /// <param name="holder">持有锁定的人物。</param>
    /// <param name="locks">返回仍有效的锁定列表。</param>
    /// <returns>人物至少持有一项有效锁定时返回真。</returns>
    private static bool TryGetLocks(Actor holder, out List<NodeLock> locks)
    {
        locks = null;
        if (!CanHoldLocks(holder) || !Locks.TryGetValue(holder.data.id, out List<NodeLock> found)) return false;
        PurgeExpired(found, Now);
        if (found.Count == 0)
        {
            Locks.Remove(holder.data.id);
            return false;
        }
        locks = found;
        return true;
    }

    /// <summary>取得或创建人物的有界锁定列表。</summary>
    /// <param name="holderId">持有人物编号。</param>
    /// <returns>可修改锁定列表。</returns>
    private static List<NodeLock> GetOrCreate(long holderId)
    {
        if (Locks.TryGetValue(holderId, out List<NodeLock> locks)) return locks;
        locks = new List<NodeLock>(4);
        Locks.Add(holderId, locks);
        return locks;
    }

    /// <summary>移除过期或指向失效节点的锁定。</summary>
    /// <param name="locks">需要清理的锁定列表。</param>
    /// <param name="now">当前世界时间。</param>
    private static void PurgeExpired(List<NodeLock> locks, double now)
    {
        for (var i = locks.Count - 1; i >= 0; i--)
            if (locks[i].expires_at <= now || !TryResolve(locks[i].handle, out _)) locks.RemoveAt(i);
    }

    /// <summary>判断人物是否可以持有神魂接触锁定。</summary>
    /// <param name="holder">候选人物。</param>
    /// <returns>人物有效且存活时返回真。</returns>
    private static bool CanHoldLocks(Actor holder)
    {
        return holder != null && !holder.isRekt() && holder.isAlive();
    }

    /// <summary>稳定比较两个节点句柄用于同距离决胜。</summary>
    /// <param name="left">左句柄。</param>
    /// <param name="right">右句柄。</param>
    /// <returns>负数表示左句柄优先。</returns>
    private static int CompareHandles(YuanshenNodeHandle left, YuanshenNodeHandle right)
    {
        int owner = left.OwnerActorId.CompareTo(right.OwnerActorId);
        if (owner != 0) return owner;
        int session = left.SessionId.CompareTo(right.SessionId);
        if (session != 0) return session;
        int logical = left.LogicalId.CompareTo(right.LogicalId);
        return logical != 0 ? logical : left.Generation.CompareTo(right.Generation);
    }

    /// <summary>当前世界时间。</summary>
    private static double Now => World.world?.getCurWorldTime() ?? 0d;

    /// <summary>一项只在神魂接触后短暂存在的节点锁定。</summary>
    private readonly struct NodeLock
    {
        /// <summary>锁定指向的稳定节点句柄。</summary>
        public readonly YuanshenNodeHandle handle;

        /// <summary>锁定失效的世界时间。</summary>
        public readonly double expires_at;

        /// <summary>创建一项短时节点锁定。</summary>
        /// <param name="handle">稳定节点句柄。</param>
        /// <param name="expiresAt">失效世界时间。</param>
        public NodeLock(YuanshenNodeHandle handle, double expiresAt)
        {
            this.handle = handle;
            expires_at = expiresAt;
        }
    }

    /// <summary>驱动锁定低频清理并在世界切换时清空静态状态的内嵌系统。</summary>
    private sealed class UpdateSystem : ThrottledSystem
    {
        /// <summary>每秒清理一次过期锁定。</summary>
        protected override float IntervalSeconds => 1f;

        /// <summary>低频清理已经失效的锁定，不获取任何新目标。</summary>
        protected override void OnThrottledUpdate()
        {
            PruneExpiredLocks();
        }

        /// <summary>世界切换时清空全部节点解析和短时锁定。</summary>
        protected override void OnThrottleWorldStateCleared()
        {
            ClearWorldState();
        }
    }
}
