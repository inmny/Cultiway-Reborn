using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Utils;

/// <summary>
/// 动画实体的视觉姿态工具。所有会复刻动画外观的系统都应复用这里的角度计算。
/// </summary>
public static class AnimVisualUtils
{
    public static float GetVisualAngle(Entity entity, ref Rotation rot)
    {
        var rotationAngle = GetRotationAngle(ref rot);
        if (!entity.HasComponent<VisualRotation>())
        {
            return rotationAngle;
        }

        ref var visualRotation = ref entity.GetComponent<VisualRotation>();
        return visualRotation.Mode switch
        {
            VisualRotationMode.FixedUpright => visualRotation.FixedAngle + visualRotation.AngleOffset,
            VisualRotationMode.KeepInitial => GetInitialVisualAngle(ref visualRotation, rotationAngle),
            VisualRotationMode.Spin => visualRotation.FixedAngle + visualRotation.AngleOffset +
                                       Time.time * visualRotation.SpinSpeed,
            _ => rotationAngle + visualRotation.AngleOffset
        };
    }

    public static float GetRotationAngle(ref Rotation rot)
    {
        return Vector2.SignedAngle(Vector2.right, rot.in_plane + new Vector2(0, rot.z));
    }

    public static Vector2 GetLocalBackDirection(Entity entity, ref Rotation rot)
    {
        var visualAngle = GetVisualAngle(entity, ref rot);
        var movementAngle = GetRotationAngle(ref rot);
        var worldBack = Quaternion.Euler(0f, 0f, movementAngle) * Vector3.left;
        var localBack = Quaternion.Euler(0f, 0f, -visualAngle) * worldBack;
        return new Vector2(localBack.x, localBack.y);
    }

    private static float GetInitialVisualAngle(ref VisualRotation visualRotation, float rotationAngle)
    {
        if (!visualRotation.HasInitialAngle)
        {
            visualRotation.InitialAngle = rotationAngle;
            visualRotation.HasInitialAngle = true;
        }

        return visualRotation.InitialAngle + visualRotation.AngleOffset;
    }
}
