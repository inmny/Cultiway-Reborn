using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Content.Combat;

internal sealed partial class GroundWeaponService
{
    /// <summary>一次确认最多保留八个最近的候选，原物件仍由地面系统维护。</summary>
    private static readonly List<(Entry Entry, float Distance)> airflow = new(8);

    /// <summary>只给实际风场内的无主物件速度；风场消退后由原重力和拾取规则接管。</summary>
    /// <param name="centre">本段真实风心的绝对坐标。</param>
    /// <param name="radius">和人物受力共用的世界半径。</param>
    /// <param name="speed">中心处有方向的水平速度。</param>
    /// <param name="limit">每段最多卷起的原件数量。</param>
    internal static void ApplyAirflow(Vector3 centre, float radius, float speed, int limit)
    {
        float radiusSquared = radius * radius;
        foreach (Entry entry in entries.Values)
        {
            Item item = entry.Item;
            if (!item.isAlive() || item.shouldbe_removed || item.hasActor() || item.hasCity()) continue;
            Vector3 offset = Vector(entry.Data.Position) + Vector3.forward * entry.Bottom - centre;
            float distance = offset.sqrMagnitude;
            if (distance >= radiusSquared) continue;
            int index = airflow.Count;
            while (index > 0 && (distance < airflow[index - 1].Distance ||
                                 distance == airflow[index - 1].Distance && entry.Data.ItemId < airflow[index - 1].Entry.Data.ItemId))
                index--;
            if (index == limit) continue;
            if (airflow.Count == limit) airflow.RemoveAt(limit - 1);
            airflow.Insert(index, (entry, distance));
        }

        foreach (var candidate in airflow)
        {
            Entry entry = candidate.Entry;
            Vector3 point = Vector(entry.Data.Position);
            Vector2 radial = point - centre;
            Vector2 outwards = radial.sqrMagnitude < .000001f ? Vector2.right : radial.normalized;
            Vector2 around = new(-outwards.y, outwards.x);
            float pressure = (1f - Mathf.Sqrt(candidate.Distance) / radius) * Mathf.Min(8f, Mathf.Abs(speed));
            Vector2 velocity = pressure * (outwards * (.46f * Mathf.Sign(speed)) + around * .62f);
            // 上升速度随实际高度衰减，不在风顶反复起跳。原系统从现有位置继续积分。
            float height = Mathf.Max(0f, point.z + entry.Bottom - entry.Ground);
            float lift = Mathf.Min(4f, pressure * .70f) - height * 2.4f;
            entry.Data.Velocity = Values(new Vector3(velocity.x, velocity.y, lift));
            entry.Data.Resting = false;
        }
        airflow.Clear();
    }
}
