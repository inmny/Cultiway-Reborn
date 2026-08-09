using System;
using System.Collections.Generic;
using Cultiway.Utils.Extension;

namespace Cultiway.Core.Combat.Tactical;

/// <summary>
/// 按地图区块保存近期真实敌对行为。和平角色只需查询邻近区块即可判断是否需要进入完整战术规划。
/// </summary>
internal static class CombatSpatialAwarenessIndex
{
    private const int MaxSignalsPerChunk = 24;
    private const int MaxCopiedSignals = 64;
    private const int MaxActorsWokenPerPublish = 64;
    private const int MaxActorsScannedPerPublish = 256;
    private const int MaxWakeScopesPerChunk = 24;
    private const double NearbyWakeInterval = 0.25d;
    private const int ChunkSize = 16;

    private static readonly Dictionary<int, SignalBucket> Buckets = new();

    /// <summary>清除当前世界的区块战斗信号。</summary>
    internal static void Clear()
    {
        Buckets.Clear();
    }

    /// <summary>
    /// 发布受袭信号，并以受害者最后位置为中心唤醒附近同阵营单位的休眠探测。
    /// </summary>
    internal static void Publish(
        CombatThreatSignal signal,
        double now,
        ICombatGroupProvider groupProvider,
        in CombatGroupKey groupKey)
    {
        if (signal == null ||
            signal.Victim.isRekt() ||
            signal.Victim.current_tile == null ||
            signal.Victim.kingdom == null ||
            !CombatWorldService.IsGroupHostileTarget(signal.Victim, signal.Attacker))
            return;

        MapChunk chunk = signal.Victim.current_tile.chunk;
        if (!Buckets.TryGetValue(chunk.id, out SignalBucket bucket))
        {
            bucket = new SignalBucket();
            Buckets.Add(chunk.id, bucket);
        }

        RemoveExpired(bucket.Signals, now);
        bool hasGroup = groupProvider != null;
        Upsert(bucket.Signals, signal, hasGroup, groupKey);
        var wakeScope = new WakeScopeKey(signal.Victim.kingdom.id, hasGroup, groupKey);
        WakeState wakeState = GetOrCreateWakeState(bucket, wakeScope);
        if (now - wakeState.LastWakeAt < NearbyWakeInterval) return;
        wakeState.LastWakeAt = now;
        WakeResponsiveAllies(
            signal.Victim,
            signal.VictimPosition,
            groupProvider,
            groupKey,
            wakeState);
    }

    /// <summary>判断角色附近是否存在仍可响应的同阵营受袭信号。</summary>
    internal static bool HasRelevantThreat(Actor actor, double now)
    {
        if (actor?.current_tile == null || actor.kingdom == null) return false;
        bool hasGroup = CombatWorldService.TryResolveCombatGroupKey(
            actor,
            out CombatGroupKey groupKey);
        float radius = hasGroup
            ? Math.Max(
                TacticalCombatSettings.NearbyAssistRadius,
                TacticalCombatSettings.GroupAssistRadius)
            : TacticalCombatSettings.NearbyAssistRadius;
        int range = ResolveChunkRange(radius);
        MapChunk origin = actor.current_tile.chunk;
        for (int y = origin.y - range; y <= origin.y + range; y++)
        {
            for (int x = origin.x - range; x <= origin.x + range; x++)
            {
                MapChunk chunk = World.world.map_chunk_manager.get(x, y);
                if (chunk == null || !Buckets.TryGetValue(chunk.id, out SignalBucket bucket))
                    continue;
                RemoveExpired(bucket.Signals, now);
                if (bucket.Signals.Count == 0)
                {
                    Buckets.Remove(chunk.id);
                    continue;
                }
                for (int i = 0; i < bucket.Signals.Count; i++)
                {
                    if (IsRelevant(actor, bucket.Signals[i], hasGroup, groupKey, now))
                        return true;
                }
            }
        }
        return false;
    }

    /// <summary>复制角色附近仍有效的受袭信号；每个区块和全局数量都受固定上限约束。</summary>
    internal static int CopyRelevantThreats(
        Actor actor,
        double now,
        ICollection<CombatThreatSignal> output)
    {
        if (actor?.current_tile == null || actor.kingdom == null) return 0;
        int added = 0;
        bool hasGroup = CombatWorldService.TryResolveCombatGroupKey(
            actor,
            out CombatGroupKey groupKey);
        float radius = hasGroup
            ? Math.Max(
                TacticalCombatSettings.NearbyAssistRadius,
                TacticalCombatSettings.GroupAssistRadius)
            : TacticalCombatSettings.NearbyAssistRadius;
        int range = ResolveChunkRange(radius);
        MapChunk origin = actor.current_tile.chunk;
        for (int y = origin.y - range; y <= origin.y + range; y++)
        {
            for (int x = origin.x - range; x <= origin.x + range; x++)
            {
                MapChunk chunk = World.world.map_chunk_manager.get(x, y);
                if (chunk == null || !Buckets.TryGetValue(chunk.id, out SignalBucket bucket))
                    continue;
                RemoveExpired(bucket.Signals, now);
                if (bucket.Signals.Count == 0)
                {
                    Buckets.Remove(chunk.id);
                    continue;
                }
                for (int i = 0; i < bucket.Signals.Count; i++)
                {
                    IndexedThreatSignal indexed = bucket.Signals[i];
                    if (!IsRelevant(actor, indexed, hasGroup, groupKey, now)) continue;
                    output.Add(indexed.Signal);
                    added++;
                    if (added >= MaxCopiedSignals) return added;
                }
            }
        }
        return added;
    }

    /// <summary>将同一对攻击者与受害者归并为一条最新信号，并回收最旧记录。</summary>
    private static void Upsert(
        List<IndexedThreatSignal> signals,
        CombatThreatSignal signal,
        bool hasGroup,
        in CombatGroupKey groupKey)
    {
        for (int i = 0; i < signals.Count; i++)
        {
            IndexedThreatSignal current = signals[i];
            if (current.Signal.AttackerId != signal.AttackerId ||
                current.Signal.VictimId != signal.VictimId)
                continue;
            CopySignal(signal, current.Signal);
            current.HasGroup = hasGroup;
            current.GroupKey = groupKey;
            return;
        }

        if (signals.Count >= MaxSignalsPerChunk)
        {
            int oldestIndex = 0;
            double oldestTime = signals[0].Signal.LastThreatAt;
            for (int i = 1; i < signals.Count; i++)
            {
                if (signals[i].Signal.LastThreatAt >= oldestTime) continue;
                oldestIndex = i;
                oldestTime = signals[i].Signal.LastThreatAt;
            }
            signals.RemoveAt(oldestIndex);
        }
        var snapshot = new CombatThreatSignal();
        CopySignal(signal, snapshot);
        signals.Add(new IndexedThreatSignal
        {
            Signal = snapshot,
            HasGroup = hasGroup,
            GroupKey = groupKey
        });
    }

    /// <summary>
    /// 冻结事件发生时的位置与强度，避免受害者移动到新块后反向改写旧区块中的信号。
    /// </summary>
    private static void CopySignal(
        CombatThreatSignal source,
        CombatThreatSignal destination)
    {
        destination.Attacker = source.Attacker;
        destination.Victim = source.Victim;
        destination.AttackerId = source.AttackerId;
        destination.VictimId = source.VictimId;
        destination.AttackerPosition = source.AttackerPosition;
        destination.VictimPosition = source.VictimPosition;
        destination.AttackerHealthRatio = source.AttackerHealthRatio;
        destination.AttackerPower = source.AttackerPower;
        destination.AttackerSize = source.AttackerSize;
        destination.AttackerAirborne = source.AttackerAirborne;
        destination.Confidence = source.Confidence;
        destination.Severity = source.Severity;
        destination.LastThreatAt = source.LastThreatAt;
    }

    /// <summary>验证信号与查询者的阵营、距离和有效期关系。</summary>
    private static bool IsRelevant(
        Actor actor,
        IndexedThreatSignal indexed,
        bool hasGroup,
        in CombatGroupKey groupKey,
        double now)
    {
        CombatThreatSignal signal = indexed.Signal;
        if (signal == null ||
            signal.Attacker.isRekt() ||
            signal.Victim.isRekt() ||
            now - signal.LastThreatAt > TacticalCombatSettings.ThreatLifetime ||
            signal.Victim.kingdom != actor.kingdom ||
            !CombatWorldService.IsGroupHostileTarget(actor, signal.Attacker))
            return false;

        float victimDistance = Toolbox.SquaredDistVec2Float(
            actor.current_position,
            signal.VictimPosition);
        float attackerDistance = Toolbox.SquaredDistVec2Float(
            actor.current_position,
            signal.AttackerPosition);
        float nearbyRadiusSquared = TacticalCombatSettings.NearbyAssistRadius *
                                    TacticalCombatSettings.NearbyAssistRadius;
        if (victimDistance <= nearbyRadiusSquared || attackerDistance <= nearbyRadiusSquared)
            return true;
        if (!hasGroup || !indexed.HasGroup || !indexed.GroupKey.Equals(groupKey)) return false;
        float groupRadiusSquared = TacticalCombatSettings.GroupAssistRadius *
                                   TacticalCombatSettings.GroupAssistRadius;
        return victimDistance <= groupRadiusSquared || attackerDistance <= groupRadiusSquared;
    }

    /// <summary>惰性移除过期或已经失效的对象引用。</summary>
    private static void RemoveExpired(List<IndexedThreatSignal> signals, double now)
    {
        for (int i = signals.Count - 1; i >= 0; i--)
        {
            CombatThreatSignal signal = signals[i].Signal;
            if (signal == null ||
                signal.Attacker.isRekt() ||
                signal.Victim.isRekt() ||
                now - signal.LastThreatAt > TacticalCombatSettings.ThreatLifetime)
                signals.RemoveAt(i);
        }
    }

    /// <summary>
    /// 在固定空间窗口内唤醒附近友军，并把较远范围限制为同一战斗群组，避免按群组总人数扫描。
    /// </summary>
    private static void WakeResponsiveAllies(
        Actor victim,
        UnityEngine.Vector2 position,
        ICombatGroupProvider groupProvider,
        in CombatGroupKey groupKey,
        WakeState wakeState)
    {
        if (victim.kingdom == null || victim.current_tile == null) return;
        float nearbyRadiusSquared = TacticalCombatSettings.NearbyAssistRadius *
                                    TacticalCombatSettings.NearbyAssistRadius;
        float groupRadiusSquared = TacticalCombatSettings.GroupAssistRadius *
                                   TacticalCombatSettings.GroupAssistRadius;
        float scanRadius = groupProvider == null
            ? TacticalCombatSettings.NearbyAssistRadius
            : Math.Max(
                TacticalCombatSettings.NearbyAssistRadius,
                TacticalCombatSettings.GroupAssistRadius);
        int range = ResolveChunkRange(scanRadius);
        long kingdomId = victim.kingdom.id;
        MapChunk origin = victim.current_tile.chunk;
        int diameter = range * 2 + 1;
        int chunkCount = diameter * diameter;
        int firstChunkOffset = wakeState.NextChunkOffset % chunkCount;
        int scanned = 0;
        int woken = 0;
        for (int visitedChunks = 0; visitedChunks < chunkCount; visitedChunks++)
        {
            int chunkOffset = (firstChunkOffset + visitedChunks) % chunkCount;
            int x = origin.x - range + chunkOffset % diameter;
            int y = origin.y - range + chunkOffset / diameter;
            MapChunk chunk = World.world.map_chunk_manager.get(x, y);
            if (chunk == null || !chunk.objects.kingdoms.Contains(kingdomId)) continue;
            List<Actor> units = chunk.objects.getUnits(kingdomId);
            if (units.Count == 0) continue;
            int firstUnit = visitedChunks == 0
                ? wakeState.NextUnitOffset % units.Count
                : 0;
            for (int inspectedUnits = 0; inspectedUnits < units.Count; inspectedUnits++)
            {
                int unitIndex = (firstUnit + inspectedUnits) % units.Count;
                Actor ally = units[unitIndex];
                scanned++;
                if (!ally.isRekt() &&
                    ally != victim &&
                    ally.current_tile != null)
                {
                    float distanceSquared = Toolbox.SquaredDistVec2Float(position, ally.current_position);
                    bool shouldWake = distanceSquared <= nearbyRadiusSquared;
                    if (!shouldWake && groupProvider != null && distanceSquared <= groupRadiusSquared)
                    {
                        shouldWake = groupProvider.TryResolveGroup(ally, out CombatGroupKey allyGroup) &&
                                     allyGroup.Equals(groupKey);
                    }
                    if (shouldWake)
                    {
                        CombatWorldService.WakeForNearbyThreat(ally);
                        woken++;
                    }
                }
                if (scanned < MaxActorsScannedPerPublish &&
                    woken < MaxActorsWokenPerPublish)
                    continue;
                if (inspectedUnits + 1 < units.Count)
                {
                    wakeState.NextChunkOffset = chunkOffset;
                    wakeState.NextUnitOffset = (unitIndex + 1) % units.Count;
                }
                else
                {
                    wakeState.NextChunkOffset = (chunkOffset + 1) % chunkCount;
                    wakeState.NextUnitOffset = 0;
                }
                return;
            }
        }
        wakeState.NextChunkOffset = (firstChunkOffset + 1) % chunkCount;
        wakeState.NextUnitOffset = 0;
    }

    /// <summary>按阵营和战斗群组分别维护区块唤醒节流及公平扫描游标。</summary>
    private static WakeState GetOrCreateWakeState(
        SignalBucket bucket,
        in WakeScopeKey scope)
    {
        if (bucket.WakeStates.TryGetValue(scope, out WakeState state)) return state;
        if (bucket.WakeStates.Count >= MaxWakeScopesPerChunk)
        {
            WakeScopeKey oldestScope = default;
            double oldestWakeAt = double.MaxValue;
            foreach (KeyValuePair<WakeScopeKey, WakeState> pair in bucket.WakeStates)
            {
                if (pair.Value.LastWakeAt >= oldestWakeAt) continue;
                oldestScope = pair.Key;
                oldestWakeAt = pair.Value.LastWakeAt;
            }
            bucket.WakeStates.Remove(oldestScope);
        }
        state = new WakeState();
        bucket.WakeStates.Add(scope, state);
        return state;
    }

    /// <summary>将世界距离换算为需要访问的区块坐标半径。</summary>
    private static int ResolveChunkRange(float radius)
    {
        return Math.Max(1, UnityEngine.Mathf.CeilToInt(radius / ChunkSize));
    }

    /// <summary>空间索引中的信号快照及其发布时战斗群组。</summary>
    private sealed class IndexedThreatSignal
    {
        internal CombatThreatSignal Signal;
        internal bool HasGroup;
        internal CombatGroupKey GroupKey;
    }

    /// <summary>同一区块内互不抑制的阵营与战斗群组唤醒范围。</summary>
    private readonly struct WakeScopeKey : IEquatable<WakeScopeKey>
    {
        internal WakeScopeKey(long kingdomId, bool hasGroup, in CombatGroupKey groupKey)
        {
            KingdomId = kingdomId;
            HasGroup = hasGroup;
            GroupKey = groupKey;
        }

        private long KingdomId { get; }
        private bool HasGroup { get; }
        private CombatGroupKey GroupKey { get; }

        public bool Equals(WakeScopeKey other)
        {
            return KingdomId == other.KingdomId &&
                   HasGroup == other.HasGroup &&
                   (!HasGroup || GroupKey.Equals(other.GroupKey));
        }

        public override bool Equals(object obj)
        {
            return obj is WakeScopeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (KingdomId.GetHashCode() * 397) ^
                       (HasGroup ? GroupKey.GetHashCode() : 0);
            }
        }
    }

    /// <summary>单个唤醒范围的节流时间与下一轮扫描位置。</summary>
    private sealed class WakeState
    {
        internal double LastWakeAt = double.MinValue;
        internal int NextChunkOffset;
        internal int NextUnitOffset;
    }

    /// <summary>单一区块的有界信号集合及分范围唤醒状态。</summary>
    private sealed class SignalBucket
    {
        internal readonly List<IndexedThreatSignal> Signals = new(MaxSignalsPerChunk);
        internal readonly Dictionary<WakeScopeKey, WakeState> WakeStates = new();
    }
}
