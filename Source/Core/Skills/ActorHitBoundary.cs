using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>各类技能判断人物受击时共用的身体边界。</summary>
internal static class ActorHitBoundary
{
    /// <summary>胶囊覆盖脚底至头顶，半径按实际体高缩放。</summary>
    /// <param name="foot">共同空间中的脚下位置。</param>
    /// <param name="height">当前身体高度。</param>
    /// <param name="torso">实际身体动作带来的整体偏移。</param>
    internal static AbilityCapsule Create(Vector3 foot, float height, Vector3 torso)
    {
        float radius = height * .24f;
        Vector3 origin = foot + torso;
        return new AbilityCapsule(0, origin + Vector3.forward * radius,
            origin + Vector3.forward * (height - radius), radius, null);
    }
}
