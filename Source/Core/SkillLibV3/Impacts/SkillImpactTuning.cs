namespace Cultiway.Core.SkillLibV3.Impacts;

/// <summary>
/// 单个法术实体在共享命中 Profile 之上的少量差异参数。
/// </summary>
public sealed class SkillImpactTuning
{
    public float DamageMultiplier = 1f;
    public float EffectRadiusMultiplier = 1f;
    public float LifetimeMultiplier = 1f;
    public float BarrierLengthMultiplier = 1f;
    public bool ContactDamage;
    public float ContactForce;
}
