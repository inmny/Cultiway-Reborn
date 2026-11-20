using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>在共同腕点上满足各条固定臂长及本步位移，使用有限球面交点。</summary>
internal struct BodyReachProjector
{
    /// <summary>两只手在原版及三维呈现的内外边界，以及可选的本步移动边界。</summary>
    private Sphere firstInner, firstOuter, secondInner, secondOuter, thirdInner, thirdOuter, fourthInner, fourthOuter, motion;

    /// <summary>实际加入的臂长边界数量。</summary>
    private int armBounds;

    /// <summary>本轮采用的全部边界数量。</summary>
    private int count;

    /// <summary>请求点、稳定的对称选择方向以及当前最近可用点。</summary>
    private Vector3 requested, preference, best;

    /// <summary>最近点的请求距离，正无穷表示尚未找到可用点。</summary>
    private float score;

    /// <summary>在单位体高坐标中求最近共同可达点；前次点限定本步距离，肩点穿越也不瞬跳。</summary>
    /// <param name="desired">请求腕点，相对第一肩点。</param><param name="other">第二肩点。</param>
    /// <param name="upper">第一套上臂长度。</param><param name="lower">第一套前臂长度。</param>
    /// <param name="otherUpper">第二套上臂长度。</param><param name="otherLower">第二套前臂长度。</param>
    /// <param name="pole">对称情况下沿用的弯曲方向。</param><param name="previous">本步身体坐标中的前次腕点。</param>
    /// <param name="distance">本步允许的移动距离。</param><param name="result">共同腕点。</param>
    internal static bool TryProject(Vector3 desired, Vector3 other, float upper, float lower, float otherUpper,
        float otherLower, Vector3 pole, Vector3? previous, float distance, out Vector3 result)
    {
        var solver = new BodyReachProjector();
        solver.AddArm(Vector3.zero, upper, lower, BodyJointSolver.Extension);
        // 给三维骨架的取点误差留余量。
        solver.AddArm(other, otherUpper, otherLower, .96f);
        return solver.Project(desired, pole, previous, distance, out result);
    }

    /// <summary>把一条手臂的可达边界换算到同一个主手腕点。</summary>
    /// <param name="centre">已扣除双手握距的肩点。</param><param name="upper">归一化上臂长。</param>
    /// <param name="lower">归一化前臂长。</param><param name="extension">该臂允许的最大伸展比例。</param>
    internal void AddArm(Vector3 centre, float upper, float lower, float extension)
    {
        var inner = new Sphere(centre, BodyJointSolver.MinimumReach(upper, lower), true);
        var outer = new Sphere(centre, (upper + lower) * extension, false);
        switch (armBounds)
        {
            case 0: firstInner = inner; firstOuter = outer; break;
            case 2: secondInner = inner; secondOuter = outer; break;
            case 4: thirdInner = inner; thirdOuter = outer; break;
            case 6: fourthInner = inner; fourthOuter = outer; break;
        }
        armBounds += 2;
    }

    /// <summary>求各手臂都能到达的最近腕点，普通可达请求直接采用。</summary>
    /// <param name="desired">请求点。</param><param name="pole">相等距离时采用的弯曲方向。</param>
    /// <param name="previous">同一局部坐标的前次点。</param><param name="distance">本次最大移动距离。</param>
    /// <param name="result">求得的共同腕点。</param>
    internal bool Project(Vector3 desired, Vector3 pole, Vector3? previous, float distance, out Vector3 result)
    {
        requested = desired;
        preference = pole;
        score = float.PositiveInfinity;
        count = armBounds + (previous.HasValue ? 1 : 0);
        if (previous.HasValue)
        {
            motion = new Sphere(previous.Value, distance, false);
            Consider(previous.Value);
        }

        Consider(desired);
        for (int i = 0; score > 0f && i < count; i++)
        {
            Sphere a = At(i);
            Vector3 ray = desired - a.Centre;
            Consider(a.Centre + Direction(ray, pole) * a.Radius);
            for (int j = i + 1; j < count; j++) Circle(a, At(j), j);
        }

        result = best;
        return !float.IsPositiveInfinity(score);
    }

    /// <summary>固定数值边界，无逐步数组分配。</summary>
    /// <param name="index">本轮有效索引。</param>
    private Sphere At(int index) => index == armBounds ? motion : index switch
    {
        0 => firstInner, 1 => firstOuter, 2 => secondInner, 3 => secondOuter,
        4 => thirdInner, 5 => thirdOuter, 6 => fourthInner, _ => fourthOuter
    };

    /// <summary>每个候选同时核对全部内外约束，只保存最近点。</summary>
    /// <param name="point">有限几何求解得到的候选。</param>
    private void Consider(Vector3 point)
    {
        for (int i = 0; i < count; i++)
        {
            Sphere sphere = At(i);
            float square = (point - sphere.Centre).sqrMagnitude, limit = sphere.Radius * sphere.Radius;
            if (sphere.Inner ? square < limit - .000001f : square > limit + .000001f) return;
        }

        float candidate = (point - requested).sqrMagnitude;
        if (candidate >= score) return;
        best = point;
        score = candidate;
    }

    /// <summary>两球面相交时，最近候选在交圆；被第三球面截断的部分再枚举三球交点。</summary>
    /// <param name="a">第一球面。</param><param name="b">第二球面。</param>
    /// <param name="after">后续第三球面的起始索引。</param>
    private void Circle(Sphere a, Sphere b, int after)
    {
        Vector3 delta = b.Centre - a.Centre;
        float distance = delta.magnitude;
        if (distance < .0000001f || distance > a.Radius + b.Radius + .000001f ||
            distance < Mathf.Abs(a.Radius - b.Radius) - .000001f) return;
        Vector3 normal = delta / distance;
        float along = (a.Radius * a.Radius - b.Radius * b.Radius + distance * distance) / (2f * distance);
        Vector3 centre = a.Centre + normal * along;
        float square = Mathf.Max(0f, a.Radius * a.Radius - along * along), radius = Mathf.Sqrt(square);
        Vector3 tangent = requested - centre;
        tangent -= normal * Vector3.Dot(tangent, normal);
        Vector3 fallback = preference - normal * Vector3.Dot(preference, normal);
        if (fallback.sqrMagnitude < .00000001f) fallback = Vector3.Cross(normal, Vector3.up);
        if (fallback.sqrMagnitude < .00000001f) fallback = Vector3.Cross(normal, Vector3.right);
        Consider(centre + Direction(tangent, fallback) * radius);
        for (int k = after + 1; k < count; k++)
        {
            Sphere c = At(k);
            Vector3 toThird = c.Centre - centre;
            Vector3 projected = toThird - normal * Vector3.Dot(toThird, normal);
            float planar = projected.magnitude;
            if (planar < .0000001f) continue;
            float x = (square + toThird.sqrMagnitude - c.Radius * c.Radius) / (2f * planar);
            if (x * x > square + .000001f) continue;
            Vector3 axis = projected / planar, side = Vector3.Cross(normal, axis);
            Vector3 middle = centre + axis * x, height = side * Mathf.Sqrt(Mathf.Max(0f, square - x * x));
            Consider(middle + height);
            Consider(middle - height);
        }
    }

    /// <summary>单位体高空间内采用明确的退化方向，避免零向量导致球心跳边。</summary>
    /// <param name="value">目标方向。</param><param name="fallback">此前保留的弯曲方向。</param>
    private static Vector3 Direction(Vector3 value, Vector3 fallback) => value.sqrMagnitude > .000000000001f
        ? value / value.magnitude
        : fallback / fallback.magnitude;

    /// <summary>单个球内或球外边界。</summary>
    private readonly struct Sphere
    {
        /// <summary>按单位体高表示的球心与半径。</summary>
        internal readonly Vector3 Centre;

        /// <summary>边界半径。</summary>
        internal readonly float Radius;

        /// <summary>真表示不得进入内部，假表示不得离开外部。</summary>
        internal readonly bool Inner;

        /// <summary>接收已经由身体接入解析的数值。</summary>
        /// <param name="centre">球心。</param><param name="radius">半径。</param><param name="inner">是否为内侧禁入区。</param>
        internal Sphere(Vector3 centre, float radius, bool inner)
        {
            Centre = centre;
            Radius = radius;
            Inner = inner;
        }
    }
}
