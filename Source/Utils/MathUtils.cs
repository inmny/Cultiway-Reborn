using UnityEngine;

namespace Cultiway.Utils;

public static class MathUtils
{
    public static float CosineSimilarity(float[] a, float[] b)
    {
        float ab = 0;
        float a_v = 0;
        float b_v = 0;
        int len = a.Length;
        for (int i = 0; i < len; i++)
        {
            ab += a[i]  * b[i];
            a_v += a[i] * a[i];
            b_v += b[i] * b[i];
        }

        return ab / Mathf.Sqrt(a_v * b_v);
    }
}