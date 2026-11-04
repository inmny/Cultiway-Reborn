using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>用真实两段长度连接关节；弯曲方向随上一姿态平顺转移，不在特殊方向突然翻面。</summary>
internal static class BodyJointSolver
{
    /// <summary>保留轻微弯曲，避免完全伸直附近的关节角度对位置误差过分敏感。</summary>
    internal const float Extension = .98f;

    /// <summary>折叠端也保留弯曲余量，避免两段接近平行时肘点对伸缩误差过分敏感。</summary>
    /// <param name="upper">第一段实际长度。</param><param name="lower">第二段实际长度。</param>
    internal static float MinimumReach(float upper, float lower)
    {
        float difference = upper - lower;
        return Mathf.Sqrt(difference * difference + 2f * upper * lower * (1f - Extension));
    }

    /// <summary>先约束伸缩与转向，再沿连续弯曲面求解两段关节。</summary>
    /// <param name="shoulder">本次起点。</param><param name="desired">请求的末端。</param>
    /// <param name="upper">实际第一段长度。</param><param name="lower">实际第二段长度。</param>
    /// <param name="pole">没有前态时采用的弯曲方向。</param>
    /// <param name="previousDirection">已经转到本次身体方向的前次末端方向。</param>
    /// <param name="previousBend">已经转到本次身体方向的前次弯曲面，空值为首次姿态。</param>
    /// <param name="previousReach">按当前体型换算的前次伸展距离。</param>
    /// <param name="angle">本次允许转过的弧度，负值不限制。</param>
    /// <param name="distance">本次允许伸缩的距离，负值不限制。</param>
    /// <param name="elbow">连续连接的中间关节。</param><param name="hand">约束后的末端。</param>
    /// <param name="bend">独立保存的弯曲面，接近伸直时也不会丢失方向。</param>
    internal static void Solve(Vector3 shoulder, Vector3 desired, float upper, float lower, Vector3 pole,
        Vector3 previousDirection, Vector3? previousBend, float previousReach, float angle, float distance,
        out Vector3 elbow, out Vector3 hand, out Vector3 bend)
    {
        float total = upper + lower, epsilon = Mathf.Min(total * .00001f, Mathf.Min(upper, lower) * .1f);
        float minimum = MinimumReach(upper, lower);
        float maximum = Mathf.Max(minimum, total * Extension);
        Vector3 delta = desired - shoulder;
        float length = delta.magnitude;
        Vector3 direction = length > epsilon ? delta / length : previousDirection;
        // 已经在转角预算内时直接采用目标方向，避免小角度再次旋转造成固定点停滞。
        if (previousBend.HasValue && angle >= 0f && angle < Mathf.PI &&
            Vector3.Dot(previousDirection, direction) < Mathf.Cos(angle))
            direction = Vector3.RotateTowards(previousDirection, direction, angle, 0f).normalized;
        float reach = Mathf.Clamp(length, minimum, maximum);
        if (previousBend.HasValue && distance >= 0f)
            reach = Mathf.Clamp(Mathf.MoveTowards(previousReach, reach, distance), minimum, maximum);
        hand = shoulder + direction * reach;
        bend = previousBend.HasValue
            ? Quaternion.FromToRotation(previousDirection, direction) * previousBend.Value
            : pole;
        bend -= direction * Vector3.Dot(bend, direction);
        if (bend.sqrMagnitude < .00000001f) bend = Vector3.Cross(direction, Vector3.up);
        if (bend.sqrMagnitude < .00000001f) bend = Vector3.Cross(direction, Vector3.right);
        bend.Normalize();
        float along = (upper * upper - lower * lower + reach * reach) / (2f * reach);
        elbow = shoulder + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
    }
}