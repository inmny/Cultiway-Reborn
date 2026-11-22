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

    /// <summary>登记时只发布真实范围；全图条件只保留一份记录。</summary>
    private static void RegisterWorldCondition(RunningCast running, Vector3 center)
    {
        var condition = new ActiveWorldCondition(running, center);
        running.WorldCondition = condition;
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
    private void AppendWorldCondition(RunningCast cast, double now)
    {
        if (cast.WorldCondition is not { } condition || !condition.IsLive(now)) return;
        if (condition.Rule.EntireWorld || cast.WorldScope != null) return;
        Vector3 point = condition.Center;
        point.z = (terrain?.Sample(point.x, point.y) ?? point.z) + .045f;
        float radius = condition.Rule.Radius;
        var model = FormResources.Load("truth_mirror");
        visuals.Add(new FormVisualInstance(model,
            Matrix4x4.TRS(point, Quaternion.identity, new Vector3(radius, radius, .7f)),
            tint: new Color(.59f, .86f, .69f, .43f), terrain: terrain));
    }

    private static Vector2Int ConditionRegion(Vector2 point) =>
        new(Mathf.FloorToInt(point.x / WorldConditionRegionSize), Mathf.FloorToInt(point.y / WorldConditionRegionSize));

    private sealed class ActiveWorldCondition
    {
        private readonly RunningCast cast;
        internal readonly CompiledWorldCondition Rule;
        internal readonly Vector3 Center;
        internal readonly Vector2Int First, Last;

        internal ActiveWorldCondition(RunningCast cast, Vector3 center)
        {
            this.cast = cast;
            Rule = cast.Cast.Plan.WorldCondition;
            Center = center;
            First = ConditionRegion((Vector2)center - Vector2.one * Rule.Radius);
            Last = ConditionRegion((Vector2)center + Vector2.one * Rule.Radius);
        }

        internal bool IsLive(double now) =>
            !cast.Replaced && !cast.Cast.Finished && now < cast.StartedAt + cast.Cast.Plan.Lifetime &&
            cast.Source.data?.id == cast.SourceId && cast.Source.isAlive() && cast.Source.hasHealth();

        internal bool Allows(WorldTile tile) =>
            cast.WorldScope == null || cast.WorldScope.IsValid() && cast.WorldScope.Allows("fire.prevent", tile);
    }
}
