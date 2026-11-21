using Cultiway.Core.Skills;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Components;

/// <summary>以实际器物两端的前后位置确认扫掠。尺寸仍由同一技能的碰撞厚度提供。</summary>
internal struct ColliderSweptBody(Vector3 start, Vector3 end, float radius) : IComponent
{
    private Vector3 previousStart = start, previousEnd = end;
    private Vector3 currentStart = start, currentEnd = end;
    private float previousRadius = radius, currentRadius = radius;

    internal void Move(Vector3 start, Vector3 end, float radius)
    {
        previousStart = currentStart;
        previousEnd = currentEnd;
        previousRadius = currentRadius;
        currentStart = start;
        currentEnd = end;
        currentRadius = radius;
    }

    internal readonly AbilityCapsule Before(float scale) => new(0, previousStart, previousEnd, previousRadius * scale, null);
    internal readonly AbilityCapsule After(float scale) => new(0, currentStart, currentEnd, currentRadius * scale, null);
}
