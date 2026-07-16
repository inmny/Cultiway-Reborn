using System;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content.Artifacts;/// <summary>法器能力共用的目标枚举与空间筛选原语。</summary>
public static class ArtifactTargeting
{
    public enum TargetDisposition
    {
        Any,
        Hostile,
        Friendly,
    }

    public static void ForEachActor(
        Actor source,
        Vector2 center,
        float radius,
        TargetDisposition disposition,
        Action<Actor> action)
    {
        WorldTile tile = World.world.GetTile(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.y));
        if (tile == null) return;

        float radiusSquared = radius * radius;
        int chunkRadius = Mathf.CeilToInt(radius / 16f) + 1;
        foreach (Actor target in Finder.getUnitsFromChunk(tile, chunkRadius))
        {
            if (target == null || target.isRekt()) continue;
            bool hostile = target != source && source.canAttackTarget(target);
            if (disposition == TargetDisposition.Hostile && !hostile) continue;
            if (disposition == TargetDisposition.Friendly &&
                target != source && target.kingdom != source.kingdom) continue;
            if (Toolbox.SquaredDistVec2Float(center, target.current_position) > radiusSquared) continue;
            action(target);
        }
    }

    public static void ForEachHostile(
        Actor source,
        Vector2 center,
        float radius,
        Action<Actor> action)
    {
        ForEachActor(source, center, radius, TargetDisposition.Hostile, action);
    }

    public static void ForEachFriendly(
        Actor source,
        Vector2 center,
        float radius,
        Action<Actor> action)
    {
        ForEachActor(source, center, radius, TargetDisposition.Friendly, action);
    }

    /// <summary>按阵营关系枚举敌人，不经过目标可见性判定，供侦破和显形能力使用。</summary>
    public static void ForEachEnemyIncludingConcealed(
        Actor source,
        Vector2 center,
        float radius,
        Action<Actor> action)
    {
        WorldTile tile = World.world.GetTile(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.y));
        if (tile == null) return;

        float radiusSquared = radius * radius;
        int chunkRadius = Mathf.CeilToInt(radius / 16f) + 1;
        foreach (Actor target in Finder.getUnitsFromChunk(tile, chunkRadius))
        {
            if (target == null || target.isRekt() || target == source) continue;
            bool hostile = source.kingdom?.isEnemy(target.kingdom) ?? source.canAttackTarget(target);
            if (!hostile || Toolbox.SquaredDistVec2Float(center, target.current_position) > radiusSquared) continue;
            action(target);
        }
    }

    /// <summary>枚举给定朝向扇形内的目标，角度使用完整张角。</summary>
    public static void ForEachActorInSector(
        Actor source,
        Vector2 center,
        Vector2 direction,
        float radius,
        float angle,
        TargetDisposition disposition,
        Action<Actor> action)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
        direction.Normalize();
        float minimumDot = Mathf.Cos(Mathf.Clamp(angle, 0f, 360f) * 0.5f * Mathf.Deg2Rad);
        ForEachActor(source, center, radius, disposition, target =>
        {
            Vector2 offset = target.current_position - center;
            if (offset.sqrMagnitude > 0.0001f && Vector2.Dot(direction, offset.normalized) < minimumDot) return;
            action(target);
        });
    }
}
