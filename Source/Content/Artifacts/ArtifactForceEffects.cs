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

namespace Cultiway.Content.Artifacts;/// <summary>法器能力共用的推拉力结算原语。</summary>
public static class ArtifactForceEffects
{
    public static void ApplyRadialForce(
        Actor source,
        Actor target,
        Vector2 center,
        float force,
        bool pull)
    {
        Vector2 direction = target.current_position - center;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
        direction.Normalize();
        if (pull) direction = -direction;
        target.GetExtend().GetForce(source, direction.x * force, direction.y * force, 0f);
    }
}
