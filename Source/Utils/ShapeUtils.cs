using UnityEngine;

namespace Cultiway.Utils;

public static class ShapeUtils
{
    /// <summary>
    /// 从<paramref name="line_start"/>开始沿着<paramref name="direction"/>方向，找到与<paramref name="rect"/>相交的点
    /// </summary>
    /// <remarks>
    /// 假设<paramref name="direction"/>是单位向量, <paramref name="line_start"/>在<paramref name="rect"/>内
    /// </remarks>
    public static Vector2 FindIntersectPoint(Vector2 line_start, Vector2 direction, Rect rect)
    {
        var lb = new Vector2(rect.xMin, rect.yMin);
        var rt = new Vector2(rect.xMax, rect.yMax);

        var x = direction.x > 0 ? rt.x : lb.x;
        var y = direction.y > 0 ? rt.y : lb.y;

        var t = Mathf.Min((x - line_start.x) / direction.x, (y - line_start.y) / direction.y);
        return line_start + direction * t;
    } 
    public static ListPool<Vector2Int> CircleOffsets(Vector2Int center, float radius)
    {
        var list = new ListPool<Vector2Int>();

        int y_top = (int)radius;
        for (int x = 0; x < radius; x++)
        {
            for (int y = y_top; y > 0; y--)
            {
                y_top = y;
                if (Mathf.Sqrt(x * x + y * y) <= radius) break;
            }

            for (int y = y_top; y > 0; y--)
            {
                list.Add(new(center.x + x, center.y + y));
                list.Add(new(center.x - y, center.y + x));
                list.Add(new(center.x - x, center.y - y));
                list.Add(new(center.x + y, center.y - x));
            }
        }

        return list;
    }

    public static bool InRect(Vector2 point, Vector2 lb, Vector2 rt)
    {
        return point.x >= lb.x && point.x <= rt.x && point.y >= lb.y && point.y <= rt.y;
    }
    public static bool OverlapRect(Rect a, Rect b)
    {
        return a.xMax > b.xMin && a.xMin < b.xMax && a.yMax > b.yMin && a.yMin < b.yMax;
    }
}