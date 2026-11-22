using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    private const int WorldConditionRegionSize = 16;
    private static readonly List<ActiveWorldCondition> worldFireConditions = new();
    private static readonly Dictionary<Vector2Int, List<ActiveWorldCondition>> regionalFireConditions = new();
    private static readonly List<ActiveWorldCondition> restoredFireConditions = new();

    /// <summary>登记时只发布真实范围；全图条件只保留一份记录。</summary>
    private static void RegisterWorldCondition(RunningCast running, Vector3 center)
    {
        var condition = new ActiveWorldCondition(running.Source, running.SourceId, running.Cast.Plan.WorldCondition,
            center, running.StartedAt + running.Cast.Plan.Lifetime, running);
        running.WorldCondition = condition;
        IndexWorldCondition(condition);
    }

    private static void IndexWorldCondition(ActiveWorldCondition condition)
    {
        if (condition.Rule.EntireWorld)
        {
            worldFireConditions.Add(condition);
            return;
        }
        for (int y = condition.First.y; y <= condition.Last.y; y++)
            for (int x = condition.First.x; x <= condition.Last.x; x++)
            {
                var key = new Vector2Int(x, y);
                if (!regionalFireConditions.TryGetValue(key, out var entries))
                    regionalFireConditions.Add(key, entries = new List<ActiveWorldCondition>());
                entries.Add(condition);
            }
    }

    /// <summary>只卸下本次登记，重叠区域的其他来源继续有效。</summary>
    private static void RemoveWorldCondition(RunningCast running)
    {
        if (running.WorldCondition is not { } condition) return;
        running.WorldCondition = null;
        UnindexWorldCondition(condition);
    }

    private static void UnindexWorldCondition(ActiveWorldCondition condition)
    {
        if (condition.Rule.EntireWorld)
        {
            worldFireConditions.Remove(condition);
            return;
        }
        for (int y = condition.First.y; y <= condition.Last.y; y++)
            for (int x = condition.First.x; x <= condition.Last.x; x++)
            {
                var key = new Vector2Int(x, y);
                if (regionalFireConditions.TryGetValue(key, out var entries) && entries.Remove(condition) && entries.Count == 0)
                    regionalFireConditions.Remove(key);
            }
    }

    /// <summary>原版尝试点火时，只读取全图记录和该地块所在小区内的实际条件。</summary>
    internal static bool PreventsNewFire(WorldTile tile)
    {
        if (worldFireConditions.Count == 0 && regionalFireConditions.Count == 0) return false;
        double now = World.world.getCurWorldTime();
        foreach (var condition in worldFireConditions)
            if (condition.IsLive(now) && condition.Allows(tile)) return true;

        Vector2 point = new(tile.x + .5f, tile.y + .5f);
        if (!regionalFireConditions.TryGetValue(ConditionRegion(point), out var entries)) return false;
        foreach (var condition in entries)
            if (condition.IsLive(now) && condition.Allows(tile) &&
                (point - (Vector2)condition.Center).sqrMagnitude <= condition.Rule.Radius * condition.Rule.Radius) return true;
        return false;
    }

    /// <summary>只显示已经登记的条件，以同一半径绘制区域。</summary>
    private void AppendWorldCondition(ActiveWorldCondition condition, double now)
    {
        if (condition == null || !condition.IsLive(now) || condition.Rule.EntireWorld || condition.HasScope) return;
        Vector3 point = condition.Center;
        point.z = (terrain?.Sample(point.x, point.y) ?? point.z) + .045f;
        float radius = condition.Rule.Radius;
        var model = FormResources.Load("truth_mirror");
        visuals.Add(new FormVisualInstance(model,
            Matrix4x4.TRS(point, Quaternion.identity, new Vector3(radius, radius, .7f)),
            tint: new Color(.59f, .86f, .69f, .43f), terrain: terrain));
    }

    /// <summary>读回的条件沿同一世界时钟结束，保留与实时条件相同的点火和显示范围。</summary>
    private void UpdateRestoredConditions(double now)
    {
        for (int i = restoredFireConditions.Count - 1; i >= 0; i--)
        {
            ActiveWorldCondition condition = restoredFireConditions[i];
            if (condition.IsLive(now))
                AppendWorldCondition(condition, now);
            else
            {
                UnindexWorldCondition(condition);
                restoredFireConditions.RemoveAt(i);
            }
        }
    }

    private static Vector2Int ConditionRegion(Vector2 point) =>
        new(Mathf.FloorToInt(point.x / WorldConditionRegionSize), Mathf.FloorToInt(point.y / WorldConditionRegionSize));

    private sealed class ActiveWorldCondition
    {
        private readonly RunningCast cast;
        private readonly Actor source;
        internal readonly long SourceId;
        internal readonly CompiledWorldCondition Rule;
        internal readonly Vector3 Center;
        internal readonly Vector2Int First, Last;
        internal readonly double EndsAt;
        internal bool HasScope => cast?.WorldScope != null;

        internal ActiveWorldCondition(Actor source, long sourceId, CompiledWorldCondition rule, Vector3 center,
            double endsAt, RunningCast cast = null)
        {
            this.cast = cast;
            this.source = source;
            SourceId = sourceId;
            Rule = rule;
            Center = center;
            EndsAt = endsAt;
            First = ConditionRegion((Vector2)center - Vector2.one * Rule.Radius);
            Last = ConditionRegion((Vector2)center + Vector2.one * Rule.Radius);
        }

        internal bool IsLive(double now) =>
            (cast == null || !cast.Replaced && !cast.Cast.Finished) && now < EndsAt &&
            source.data?.id == SourceId && source.isAlive() && source.hasHealth();

        internal bool Allows(WorldTile tile) =>
            !HasScope || cast.WorldScope.IsValid() && cast.WorldScope.Allows("fire.prevent", tile);
    }
}
