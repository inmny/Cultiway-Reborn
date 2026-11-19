namespace Cultiway.Core.SkillLibV3.Visuals;

/// <summary>
/// 从明确锚点发射时的空间粒子参数，两种视角共用。
/// </summary>
public struct SkillParticleStyle
{
    public int ParticlesPerEmission;
    public float EmissionInterval;
    public float MinSize;
    public float MaxSize;
    public float MinLifetime;
    public float MaxLifetime;
    public float MinRiseSpeed;
    public float MaxRiseSpeed;
    public float HorizontalDrift;
    public float Alpha;

    public static SkillParticleStyle Default => new()
    {
        ParticlesPerEmission = 1,
        EmissionInterval = 0.18f,
        MinSize = 0.12f,
        MaxSize = 0.22f,
        MinLifetime = 0.35f,
        MaxLifetime = 0.55f,
        MinRiseSpeed = 1.4f,
        MaxRiseSpeed = 2.6f,
        HorizontalDrift = 0.45f,
        Alpha = 0.78f
    };
}
