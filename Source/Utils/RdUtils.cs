using MathNet.Numerics.Distributions;

namespace Cultiway.Utils;

public static class RdUtils
{
    private static readonly Normal _std_normal_rng = new(0, 1);

    private static readonly Normal _normal_0_6_rng = new(0, 6);

    public static float NextStdNormal()
    {
        return (float)_std_normal_rng.Sample();
    }

    public static float NextNormal_0_6()
    {
        return (float)_normal_0_6_rng.Sample();
    }

    public static float NextNormal(float mean, float std)
    {
        return (float)new Normal(mean, std).Sample();
    }

    public static int RandomIndexWithAccumWeight(int[] accum_weights)
    {
        var rand = UnityEngine.Random.Range(0, accum_weights[accum_weights.Length-1]);
        for(int i = 0; i < accum_weights.Length; i++)
        {
            if (rand < accum_weights[i])
                return i;
        }

        return 0;
    }
}