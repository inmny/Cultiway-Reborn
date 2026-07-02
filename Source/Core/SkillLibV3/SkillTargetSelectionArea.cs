using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public readonly struct SkillTargetSelectionArea
{
    public static readonly SkillTargetSelectionArea Inactive = new(false, default, 0f);

    public readonly bool Active;
    public readonly Vector3 Center;
    public readonly float Radius;

    public SkillTargetSelectionArea(bool active, Vector3 center, float radius)
    {
        Active = active;
        Center = center;
        Radius = radius;
    }
}
