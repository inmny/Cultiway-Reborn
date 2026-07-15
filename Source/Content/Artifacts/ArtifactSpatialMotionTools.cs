using Cultiway.Core.Components;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 法器空间能力共用的连续转向、逐帧位移和扫掠检测计算。
/// </summary>
internal static class ArtifactSpatialMotionTools
{
    public static float Advance(
        ref Position position,
        ref Rotation rotation,
        ref Vector2 direction,
        Vector2 desiredDirection,
        float speed,
        float turnRate,
        float deltaTime)
    {
        Vector2 desired = Normalize(desiredDirection, direction);
        Vector2 current = Normalize(direction, desired);
        Vector3 turned = Vector3.RotateTowards(
            current,
            desired,
            Mathf.Max(0f, turnRate) * Mathf.Deg2Rad * deltaTime,
            0f);
        direction = Normalize(turned, desired);

        float distance = Mathf.Max(0f, speed) * deltaTime;
        position.v2 += direction * distance;
        rotation.z = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        return distance;
    }

    public static Vector2 DirectionTo(Vector2 source, Vector2 target, Vector2 fallback)
    {
        return Normalize(target - source, fallback);
    }

    public static Vector2 ResolveCruiseDirection(
        Vector2 ownerPosition,
        Vector2 artifactPosition,
        Vector2 currentDirection,
        float orbitRadius,
        float orbitSign)
    {
        Vector2 offset = artifactPosition - ownerPosition;
        if (offset.sqrMagnitude < 0.01f)
        {
            Vector2 initial = Normalize(currentDirection, Vector2.right);
            return new Vector2(-initial.y, initial.x) * orbitSign;
        }

        float radius = Mathf.Max(1f, orbitRadius);
        float distance = offset.magnitude;
        Vector2 radial = offset / distance;
        Vector2 tangent = new(-radial.y * orbitSign, radial.x * orbitSign);
        float radialError = (distance - radius) / radius;
        return Normalize(tangent - radial * radialError * 1.6f, tangent);
    }

    public static bool SegmentIntersectsCircle(
        Vector2 start,
        Vector2 end,
        Vector2 center,
        float radius)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        float t = lengthSquared > 0.0001f
            ? Mathf.Clamp01(Vector2.Dot(center - start, segment) / lengthSquared)
            : 0f;
        Vector2 closest = start + segment * t;
        return (center - closest).sqrMagnitude <= radius * radius;
    }

    private static Vector2 Normalize(Vector2 value, Vector2 fallback)
    {
        if (value.sqrMagnitude > 0.0001f) return value.normalized;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector2.right;
    }
}
