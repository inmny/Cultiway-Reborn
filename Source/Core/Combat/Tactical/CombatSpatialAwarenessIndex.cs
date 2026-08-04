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
    internal static void Publish(CombatThreatSignal signal, double now)
    {
        if (signal == null ||
            signal.Victim.isRekt() ||
            signal.Victim.current_tile == null ||
            signal.Victim.kingdom == null ||
            signal.Attacker.kingdom == signal.Victim.kingdom)
            return;

        MapChunk chunk = signal.Victim.current_tile.chunk;
        if (!Buckets.TryGetValue(chunk.id, out SignalBucket bucket))
        {
            bucket = new SignalBucket();
            Buckets.Add(chunk.id, bucket);
        }

        RemoveExpired(bucket.Signals, now);
        Upsert(bucket.Signals, signal);
        if (now - bucket.LastWakeAt < NearbyWakeInterval) return;
        bucket.LastWakeAt = now;
        WakeNearbyAllies(signal.Victim, signal.VictimPosition);
    }

    /// <summary>判断角色附近是否存在仍可响应的同阵营受袭信号。</summary>
    internal static bool HasRelevantThreat(Actor actor, double now)
    {
        if (actor?.current_tile == null || actor.kingdom == null) return false;
        int range = ResolveChunkRange(TacticalCombatSettings.NearbyAssistRadius);
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
                    if (IsRelevant(actor, bucket.Signals[i], now)) return true;
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
        int range = ResolveChunkRange(TacticalCombatSettings.NearbyAssistRadius);
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
                    CombatThreatSignal signal = bucket.Signals[i];
                    if (!IsRelevant(actor, signal, now)) continue;
                    output.Add(signal);
                    added++;
                    if (added >= MaxCopiedSignals) return added;
                }
            }
        }
        return added;
    }

    /// <summary>将同一对攻击者与受害者归并为一条最新信号，并回收最旧记录。</summary>
    private static void Upsert(List<CombatThreatSignal> signals, CombatThreatSignal signal)
    {
        for (int i = 0; i < signals.Count; i++)
        {
            CombatThreatSignal current = signals[i];
            if (current.AttackerId != signal.AttackerId || current.VictimId != signal.VictimId)
                continue;
            CopySignal(signal, current);
            return;
        }

        if (signals.Count >= MaxSignalsPerChunk)
        {
            int oldestIndex = 0;
            double oldestTime = signals[0].LastThreatAt;
            for (int i = 1; i < signals.Count; i++)
            {
                if (signals[i].LastThreatAt >= oldestTime) continue;
                oldestIndex = i;
                oldestTime = signals[i].LastThreatAt;
            }
            signals.RemoveAt(oldestIndex);
        }
        var snapshot = new CombatThreatSignal();
        CopySignal(signal, snapshot);
        signals.Add(snapshot);
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
    private static bool IsRelevant(Actor actor, CombatThreatSignal signal, double now)
    {
        if (signal == null ||
            signal.Attacker.isRekt() ||
            signal.Victim.isRekt() ||
            now - signal.LastThreatAt > TacticalCombatSettings.ThreatLifetime ||
            signal.Victim.kingdom != actor.kingdom ||
            signal.Attacker.kingdom == actor.kingdom)
            return false;

        float radiusSquared = TacticalCombatSettings.NearbyAssistRadius *
                              TacticalCombatSettings.NearbyAssistRadius;
        return Toolbox.SquaredDistVec2Float(actor.current_position, signal.VictimPosition) <=
               radiusSquared ||
               Toolbox.SquaredDistVec2Float(actor.current_position, signal.AttackerPosition) <=
               radiusSquared;
    }

    /// <summary>惰性移除过期或已经失效的对象引用。</summary>
    private static void RemoveExpired(List<CombatThreatSignal> signals, double now)
    {
        for (int i = signals.Count - 1; i >= 0; i--)
        {
            CombatThreatSignal signal = signals[i];
            if (signal == null ||
                signal.Attacker.isRekt() ||
                signal.Victim.isRekt() ||
                now - signal.LastThreatAt > TacticalCombatSettings.ThreatLifetime)
                signals.RemoveAt(i);
        }
    }

    /// <summary>只在真实威胁发布时扫描同阵营区块列表，使附近休眠角色立即参加下一次敌情探测。</summary>
    private static void WakeNearbyAllies(Actor victim, UnityEngine.Vector2 position)
    {
        if (victim.kingdom == null || victim.current_tile == null) return;
        int range = ResolveChunkRange(TacticalCombatSettings.NearbyAssistRadius);
        float radiusSquared = TacticalCombatSettings.NearbyAssistRadius *
                              TacticalCombatSettings.NearbyAssistRadius;
        long kingdomId = victim.kingdom.id;
        MapChunk origin = victim.current_tile.chunk;
        int woken = 0;
        for (int y = origin.y - range; y <= origin.y + range; y++)
        {
            for (int x = origin.x - range; x <= origin.x + range; x++)
            {
                MapChunk chunk = World.world.map_chunk_manager.get(x, y);
                if (chunk == null || !chunk.objects.kingdoms.Contains(kingdomId)) continue;
                List<Actor> units = chunk.objects.getUnits(kingdomId);
                for (int i = 0; i < units.Count; i++)
                {
                    Actor ally = units[i];
                    if (ally.isRekt() ||
                        ally == victim ||
                        ally.current_tile == null ||
                        Toolbox.SquaredDistVec2Float(position, ally.current_position) >
                        radiusSquared)
                        continue;
                    CombatWorldService.WakeForNearbyThreat(ally);
                    woken++;
                    if (woken >= MaxActorsWokenPerPublish) return;
                }
            }
        }
    }

    /// <summary>将世界距离换算为需要访问的区块坐标半径。</summary>
    private static int ResolveChunkRange(float radius)
    {
        return Math.Max(1, UnityEngine.Mathf.CeilToInt(radius / ChunkSize));
    }

    /// <summary>单一区块的有界信号集合及事件唤醒节流时间。</summary>
    private sealed class SignalBucket
    {
        internal readonly List<CombatThreatSignal> Signals = new(MaxSignalsPerChunk);
        internal double LastWakeAt = double.MinValue;
    }
}
