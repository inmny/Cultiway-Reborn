using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>真实身体接触处的撞入速度和动能；只读确认运动，不读取图片或预设落点。</summary>
internal readonly struct BodyImpactKinetics
{
    /// <summary>身体接触点的完整世界速度，包含转动。</summary>
    internal readonly Vector3 Velocity;

    /// <summary>朝向环境的法向速度；擦过和离开不产生撞伤。</summary>
    internal readonly float ClosingSpeed;

    /// <summary>尚待正式防御处理的基础撞伤。</summary>
    internal readonly float Damage;

    /// <summary>沿同一世界坐标的实际接触计算。</summary>
    /// <param name="center">身体当前面积中心。</param><param name="linear">中心速度。</param>
    /// <param name="angular">世界角速度。</param><param name="point">身体真实接触点。</param>
    /// <param name="normal">环境朝向身体的法线。</param><param name="mass">实际质量。</param>
    /// <param name="scale">构造撞伤倍率。</param>
    internal BodyImpactKinetics(Vector3 center, Vector3 linear, Vector3 angular, Vector3 point, Vector3 normal,
        float mass, float scale)
    {
        Velocity = linear + Vector3.Cross(angular, point - center);
        ClosingSpeed = Mathf.Max(0f, -Vector3.Dot(Velocity, normal.normalized));
        Damage = .5f * mass * ClosingSpeed * ClosingSpeed * scale;
    }
}
