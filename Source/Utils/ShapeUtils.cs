using UnityEngine;

namespace Cultiway.Utils;

public static class ShapeUtils
{
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