using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

public enum VisualRotationMode
{
    FollowRotation,
    FixedUpright,
    KeepInitial,
    Spin
}

public struct VisualRotation : IComponent
{
    public VisualRotationMode Mode;
    public float FixedAngle;
    public float AngleOffset;
    public float SpinSpeed;
    public bool HasInitialAngle;
    public float InitialAngle;

    public static VisualRotation FollowRotation(float angleOffset = 0f)
    {
        return new VisualRotation
        {
            Mode = VisualRotationMode.FollowRotation,
            AngleOffset = angleOffset
        };
    }

    public static VisualRotation FixedUpright(float fixedAngle = 0f)
    {
        return new VisualRotation
        {
            Mode = VisualRotationMode.FixedUpright,
            FixedAngle = fixedAngle
        };
    }

    public static VisualRotation KeepInitial(float angleOffset = 0f)
    {
        return new VisualRotation
        {
            Mode = VisualRotationMode.KeepInitial,
            AngleOffset = angleOffset
        };
    }

    public static VisualRotation Spin(float spinSpeed, float fixedAngle = 0f)
    {
        return new VisualRotation
        {
            Mode = VisualRotationMode.Spin,
            SpinSpeed = spinSpeed,
            FixedAngle = fixedAngle
        };
    }
}
