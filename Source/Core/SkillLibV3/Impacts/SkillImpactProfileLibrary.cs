namespace Cultiway.Core.SkillLibV3.Impacts;

public sealed class SkillImpactProfileLibrary : AssetLibrary<SkillImpactProfileAsset>
{
    public static SkillImpactProfileAsset NormalProjectile { get; private set; }
    public static SkillImpactProfileAsset Piercing { get; private set; }
    public static SkillImpactProfileAsset Wave { get; private set; }
    public static SkillImpactProfileAsset Explosion { get; private set; }
    public static SkillImpactProfileAsset HeavySkyfall { get; private set; }
    public static SkillImpactProfileAsset GroundManifest { get; private set; }
    public static SkillImpactProfileAsset GroundWave { get; private set; }
    public static SkillImpactProfileAsset Field { get; private set; }
    public static SkillImpactProfileAsset PulseBeam { get; private set; }
    public static SkillImpactProfileAsset ChannelBeam { get; private set; }
    public static SkillImpactProfileAsset Chain { get; private set; }
    public static SkillImpactProfileAsset Wall { get; private set; }
    public static SkillImpactProfileAsset Shield { get; private set; }

    public override void init()
    {
        base.init();

        NormalProjectile = Add("NormalProjectile", SkillImpactKind.Projectile, 0.8f, 0f, 1f, 1f, 1f);
        NormalProjectile.RecycleOnHit = true;

        Piercing = Add("Piercing", SkillImpactKind.Piercing, 0.65f, 0f, 0.85f, 1f, 1.6f);
        Piercing.HitOncePerTarget = true;
        Piercing.ContinueAfterHit = true;
        Piercing.Lifetime = 0.9f;
        Piercing.LinearForward = 0.65f;
        Piercing.LinearBackward = 0.25f;

        Wave = Add("Wave", SkillImpactKind.Wave, 1.2f, 0f, 0.9f, 1.1f, 2f);
        Wave.HitOncePerTarget = true;
        Wave.ContinueAfterHit = true;
        Wave.Lifetime = 0.75f;

        Explosion = Add("Explosion", SkillImpactKind.Explosion, 0.8f, 2f, 1f, 1.1f, 3f);
        Explosion.RecycleOnHit = true;

        HeavySkyfall = Add("HeavySkyfall", SkillImpactKind.HeavySkyfall, 0.9f, 2.5f, 1.2f, 1.3f, 4f);
        HeavySkyfall.RecycleOnHit = true;

        GroundManifest = Add("GroundManifest", SkillImpactKind.GroundManifest, 1.4f, 1.4f, 1f, 1.3f, 2f);
        GroundManifest.RecycleOnHit = true;
        GroundManifest.Lifetime = 0.9f;

        GroundWave = Add("GroundWave", SkillImpactKind.GroundWave, 1.1f, 1.1f, 0.8f, 1.3f, 2.5f);
        GroundWave.HitOncePerTarget = true;
        GroundWave.ContinueAfterHit = true;
        GroundWave.Lifetime = 0.9f;

        Field = Add("Field", SkillImpactKind.Field, 2.5f, 2.5f, 0.25f, 1.5f, 4f);
        Field.RepeatHitInterval = 0.4f;
        Field.Lifetime = 2.4f;
        Field.ContinueAfterHit = true;
        Field.PersistentLimit = 2;

        PulseBeam = Add("PulseBeam", SkillImpactKind.PulseBeam, 0.6f, 0f, 1f, 1.2f, 2f);
        PulseBeam.HitOncePerTarget = true;
        PulseBeam.ContinueAfterHit = true;
        PulseBeam.Lifetime = 0.16f;

        ChannelBeam = Add("ChannelBeam", SkillImpactKind.ChannelBeam, 0.45f, 0f, 0.2f, 1.5f, 3f);
        ChannelBeam.RepeatHitInterval = 0.12f;
        ChannelBeam.ContinueAfterHit = true;
        ChannelBeam.Lifetime = 0.6f;

        Chain = Add("Chain", SkillImpactKind.Chain, 1f, 0f, 1f, 1.6f, 3f);
        Chain.RecycleOnHit = true;
        Chain.MaxTargets = 4;
        Chain.JumpRadius = 4f;
        Chain.JumpDamageFalloff = 0.8f;

        Wall = Add("Wall", SkillImpactKind.Wall, 0.4f, 0f, 0.2f, 1.5f, 1f);
        Wall.Lifetime = 4f;
        Wall.BarrierLength = 5f;
        Wall.BarrierWidth = 0.4f;
        Wall.DurabilityMultiplier = 5f;
        Wall.PersistentLimit = 1;
        Wall.ContinueAfterHit = true;
        Wall.RepeatHitInterval = 0.35f;

        Shield = Add("Shield", SkillImpactKind.Shield, 0f, 0f, 0f, 1.4f, 1f);
        Shield.Lifetime = 3f;
        Shield.BarrierLength = 2.8f;
        Shield.BarrierWidth = 0.5f;
        Shield.DurabilityMultiplier = 3f;
        Shield.PersistentLimit = 1;
        Shield.ContinueAfterHit = true;
    }

    private SkillImpactProfileAsset Add(string name, SkillImpactKind kind, float collisionRadius,
        float effectRadius, float damageMultiplier, float costMultiplier, float expectedTargets)
    {
        return add(new SkillImpactProfileAsset
        {
            id = $"Cultiway.SkillImpactProfile.{name}",
            Kind = kind,
            CollisionRadius = collisionRadius,
            EffectRadius = effectRadius,
            DamageMultiplier = damageMultiplier,
            CostMultiplier = costMultiplier,
            ExpectedTargets = expectedTargets
        });
    }
}
