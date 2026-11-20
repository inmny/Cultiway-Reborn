using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>从实体作用面的水平切片确认闭环，空隙和只从同一侧遮住的人物不算在环内。</summary>
internal sealed class AbilityEnclosure
{
    /// <summary>一块屏障可用于共同围合的作用面预算。</summary>
    internal const int MaximumShapes = 48;

    /// <summary>两个端点对应该胶囊在当前高度的真实球面截口。</summary>
    private readonly Section[] sections = new Section[2 * MaximumShapes];

    /// <summary>同一截口的方位、已走过的角度圈数和复用的遍历队列。</summary>
    private readonly float[] angles = new float[2 * MaximumShapes];
    private readonly int[] turns = new int[2 * MaximumShapes], queue = new int[2 * MaximumShapes];

    /// <summary>尝试从实际露出的截口连成连续路径，检查路径是否绕过目标整整一圈。</summary>
    /// <param name="shapes">同次施放在当前已确认姿态的作用面。</param>
    /// <param name="node">参与围合的真实屏障。</param>
    /// <param name="point">人物在共同世界空间的身体中心。</param>
    internal bool Contains(List<AbilityCapsule> shapes, int node, Vector3 point)
    {
        int count = 0;
        foreach (var shape in shapes)
        {
            if (shape.Node != node || shape.Material?.State != "solid" || shape.Prism != null || shape.IsBox)
                continue;
            float ar = SectionRadius(shape.Radius, shape.A.z - point.z);
            float br = SectionRadius(shape.Radius, shape.B.z - point.z);
            if (ar <= 0f || br <= 0f) continue;
            Vector2 a = shape.A - point, b = shape.B - point;
            Vector2 axis = b - a;
            float at = axis.sqrMagnitude == 0f ? 0f : Mathf.Clamp01(-Vector2.Dot(a, axis) / axis.sqrMagnitude);
            // 落在实体墙片上的人物不是内部目标。保守拒绝接近轴线的点，也避免路径经过测试点。
            float radius = Mathf.Max(ar, br);
            if ((a + axis * at).sqrMagnitude <= radius * radius) return false;
            if (count == sections.Length) return false;
            sections[count++] = new Section(a, ar);
            sections[count++] = new Section(b, br);
        }
        if (count < 6) return false;
        for (int i = 0; i < count; i++)
        {
            angles[i] = Mathf.Atan2(sections[i].Point.y, sections[i].Point.x);
            turns[i] = int.MinValue;
        }
        for (int seed = 0; seed < count; seed++)
        {
            if (turns[seed] != int.MinValue) continue;
            int head = 0, tail = 0;
            turns[seed] = 0;
            queue[tail++] = seed;
            while (head < tail)
            {
                int from = queue[head++];
                for (int to = 0; to < count; to++)
                {
                    if (to == from || to != (from ^ 1) && !sections[from].Overlaps(sections[to])) continue;
                    float angle = angles[to] - angles[from];
                    int turn = turns[from] + (angle > Mathf.PI ? -1 : angle < -Mathf.PI ? 1 : 0);
                    if (turns[to] == turn) continue;
                    // 同一真实截口经两条连续路径到达，只有绕过中心才能相差整圈。
                    if (turns[to] != int.MinValue) return true;
                    turns[to] = turn;
                    queue[tail++] = to;
                }
            }
        }
        return false;
    }

    /// <summary>球体和横截平面相交得到的真实圆半径；悬空或低于平面时没有截口。</summary>
    /// <param name="radius">端点球面半径。</param><param name="height">到测试平面的垂直距离。</param>
    private static float SectionRadius(float radius, float height) => Mathf.Abs(height) >= radius
        ? 0f : Mathf.Sqrt(radius * radius - height * height);

    /// <summary>同一平面内实际存在的端点截口。相连的两个截口间可在屏障实体内部行走。</summary>
    /// <param name="point">相对人物中心的平面位置。</param><param name="radius">球面切口半径。</param>
    private readonly struct Section(Vector2 point, float radius)
    {
        /// <summary>截口的中心。</summary>
        internal readonly Vector2 Point = point;

        /// <summary>实际共同平面半径。</summary>
        private readonly float extent = radius;

        /// <summary>只有实际相交的截口才连成可走路径，切点相贴不算合拢。</summary>
        /// <param name="other">另一处真实截口。</param>
        internal bool Overlaps(in Section other)
        {
            float sum = extent + other.extent;
            return (Point - other.Point).sqrMagnitude < sum * sum;
        }
    }
}
