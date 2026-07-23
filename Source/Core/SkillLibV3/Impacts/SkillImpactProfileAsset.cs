namespace Cultiway.Core.SkillLibV3.Impacts;

public enum SkillImpactKind
{
    Projectile,
    Piercing,
    Wave,
    Explosion,
    HeavySkyfall,
    GroundManifest,
    GroundWave,
    Field,
    PulseBeam,
    ChannelBeam,
    Chain,
    Wall,
    Shield
}

/// <summary>
/// 法术实体的通用命中、持续时间和战场占位参数。
/// </summary>
public class SkillImpactProfileAsset : Asset
{
    public SkillImpactKind Kind;
    public float CollisionRadius;
    public float EffectRadius;
    public float DamageMultiplier = 1f;
    public bool RecycleOnHit;
    public bool ContinueAfterHit;
    public bool HitOncePerTarget;
    public float RepeatHitInterval;
    public float Lifetime = 5f;
    public float LinearForward;
    public float LinearBackward;
    public int MaxTargets = 1;
    public float JumpRadius;
    public float JumpDamageFalloff = 1f;
    public float BarrierLength;
    public float BarrierWidth;
    public float DurabilityMultiplier;
    public bool ContactDamage;
    public int PersistentLimit;
    public float CostMultiplier = 1f;
    public float ExpectedTargets = 1f;

    public bool IsAreaImpact => Kind is SkillImpactKind.Explosion or SkillImpactKind.HeavySkyfall
        or SkillImpactKind.GroundManifest or SkillImpactKind.GroundWave;

    public bool CanResolveAtPosition => Kind is SkillImpactKind.Explosion or SkillImpactKind.HeavySkyfall
        or SkillImpactKind.GroundManifest;

    public bool IsField => Kind == SkillImpactKind.Field;

    public bool IsBeam => Kind is SkillImpactKind.PulseBeam or SkillImpactKind.ChannelBeam;

    public bool IsBarrier => Kind is SkillImpactKind.Wall or SkillImpactKind.Shield;
}
