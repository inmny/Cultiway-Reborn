using UnityEngine;

namespace Cultiway.Utils;

public static class MathUtils
{
    public static float CosineSimilarity(float[] a, float[] b, int len = -1)
    {
        if (len == -1) len = a.Length;

        float ab = 0;
        float a_v = 0;
        float b_v = 0;
        for (int i = 0; i < len; i++)
        {
            ab += a[i]  * b[i];
            a_v += a[i] * a[i];
            b_v += b[i] * b[i];
        }

        return ab / Mathf.Sqrt(a_v * b_v);
    }

    public static float Normal(float x, float mean, float std)
    {
        return Mathf.Exp(-(x - mean) * (x - mean) / (2 * std * std)) / (std * Mathf.Sqrt(2 * Mathf.PI));
    }

    public static Vector2Int NextGrid(Vector2 pos, Vector2 dir)
    {
        return new Vector2Int(
            Mathf.RoundToInt(pos.x + dir.x),
            Mathf.RoundToInt(pos.y + dir.y)
        );
    }
}