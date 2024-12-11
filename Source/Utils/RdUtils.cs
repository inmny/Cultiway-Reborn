using MathNet.Numerics.Distributions;

namespace Cultiway.Utils;

public static class RdUtils
{
    private static readonly Normal _std_normal_rng = new(0, 1);

    public static float NextStdNormal()
    {
        return (float)_std_normal_rng.Sample();
    }
}