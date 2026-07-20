using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>内容系统共用的推拉力结算原语。</summary>
public static class CombatForceEffects
{
    /// <summary>以指定中心向外推开或向内牵引目标。</summary>
    public static void ApplyRadialForce(Actor source, Actor target, Vector2 center, float force, bool pull)
    {
        if (source == null || target == null || target.isRekt()) return;
        Vector2 direction = target.current_position - center;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
        direction.Normalize();
        if (pull) direction = -direction;
        target.GetExtend().GetForce(source, direction.x * force, direction.y * force, 0f);
    }
}
