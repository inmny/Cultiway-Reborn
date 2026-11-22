using Cultiway.Core.Skills;
using Cultiway.Core.Visuals;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Components;

/// <summary>记录器物的前后姿态，用真实刃面或旧器身的双端范围确认扫掠。</summary>
internal struct ColliderSweptBody(Vector3 start, Vector3 end, float radius, AbilityMeshSurface surface,
    in SpatialVisualMeshInstance pose) : IComponent
{
    private Vector3 previousStart = start, previousEnd = end;
    private Vector3 currentStart = start, currentEnd = end;
    private float previousRadius = radius, currentRadius = radius;
    private SpatialVisualMeshInstance previousPose = pose, currentPose = pose;
    private readonly AbilityMeshSurface surface = surface;

    internal void Move(in ColliderSweptBody next)
    {
        previousStart = currentStart;
        previousEnd = currentEnd;
        previousRadius = currentRadius;
        previousPose = currentPose;
        currentStart = next.currentStart;
        currentEnd = next.currentEnd;
        currentRadius = next.currentRadius;
        currentPose = next.currentPose;
    }

    internal readonly AbilityCapsule Before(float scale) => new(0, previousStart, previousEnd, previousRadius * scale, null);
    internal readonly AbilityCapsule After(float scale) => new(0, currentStart, currentEnd, currentRadius * scale, null);
    internal readonly AbilityMeshSweep Sweep(AbilityHeightField terrain = null) =>
        surface == null ? default : new AbilityMeshSweep(surface, previousPose, currentPose, terrain);
}
