namespace Cultiway.Core.SkillLibV3.Visuals;

/// <summary>
/// 法术掠过地面时的上升粒子参数。所有粒子由共享 ParticleSystem 批量发射。
/// </summary>
public struct SkillFlyOverParticleStyle
{
    public int ParticlesPerEmission;
    public float EmissionDuration;
    public float EmissionInterval;
    public float MinSize;
    public float MaxSize;
    public float MinLifetime;
    public float MaxLifetime;
    public float MinRiseSpeed;
    public float MaxRiseSpeed;
    public float HorizontalDrift;
    public float Alpha;

    public static SkillFlyOverParticleStyle Default => new()
    {
        ParticlesPerEmission = 1,
        EmissionDuration = 1f,
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
