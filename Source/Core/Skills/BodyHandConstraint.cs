using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>真实接触取得的手部约束；数值取样可在工作线程完成，不读取人物。</summary>
public readonly struct BodyHandConstraint
{
    /// <summary>共同世界坐标中的实际连接点。</summary>
    public readonly Vector3 Point;

    /// <summary>连接点相对手部姿态的局部偏移，持物也以真实握持手换算。</summary>
    public readonly Vector3 Local;

    /// <summary>维持时为一，解除后逐渐归还自由动作。</summary>
    public readonly float Weight;

    /// <summary>主线程确认真实联系后，冻结共同姿态所需的纯数值。</summary>
    /// <param name="point">真实世界连接点。</param><param name="local">手部局部连接。</param>
    /// <param name="weight">约束的当前权重。</param>
    public BodyHandConstraint(Vector3 point, Vector3 local, float weight = 1f)
    {
        Point = point;
        Local = local;
        Weight = Mathf.Clamp01(weight);
    }

    /// <summary>保持手部旋转，换算腕点；骨长和共同可达范围仍由原求解器处理。</summary>
    /// <param name="free">本时刻自由动作请求。</param><param name="rotation">本时刻实际手部方向。</param>
    internal Vector3 Hand(Vector3 free, Quaternion rotation) => Vector3.Lerp(free, Point - rotation * Local, Weight);
}