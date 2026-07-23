using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Motions;
using UnityEngine;
using DeliveryTag = Cultiway.Core.Semantics.SkillSemantics.Delivery;
using FormTag = Cultiway.Core.Semantics.SkillSemantics.Form;
using MotionTag = Cultiway.Core.Semantics.SkillSemantics.Motion;

namespace Cultiway.Content;

[Dependency(typeof(SkillTrajectories))]
public class SkillMotionProfiles : ExtendLibrary<SkillMotionProfileAsset, SkillMotionProfiles>
{
    private const float MovementSpeedMultiplier = 0.5f;

    public static SkillMotionProfileAsset Swift { get; private set; }
    public static SkillMotionProfileAsset Projectile { get; private set; }
    public static SkillMotionProfileAsset Patterned { get; private set; }
    public static SkillMotionProfileAsset Sustained { get; private set; }
    public static SkillMotionProfileAsset Melee { get; private set; }
    public static SkillMotionProfileAsset Snap { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.SkillMotionProfile";

    protected override void OnInit()
    {
        Swift.Configure(MovementSpeed(78f), MovementSpeed(180f), 0.38f, 720f, 0.045f, 1.25f, 1f, 0.07f,
                Afterimage(0.085f, 0.5f, 0.42f, 0.02f))
            .ConfigureAfterimageDensity(18f)
            .MatchAny(50, MotionTag.Direct, MotionTag.Homing);

        // 落石、落木虽采用竖直坠落几何，节奏仍按高速弹丸处理。
        Projectile.Configure(MovementSpeed(62f), MovementSpeed(150f), 0.48f, 540f, 0.055f, 1.2f, 1f, 0.08f,
                Afterimage(0.075f, 0.45f, 0.34f, 0.025f))
            .ConfigureAfterimageDensity(20f)
            .MatchAny(60, DeliveryTag.Projectile);

        Patterned.Configure(MovementSpeed(52f), MovementSpeed(120f), 0.72f, 600f, 0.055f, 1.15f, 1f, 0.09f,
                Afterimage(0.075f, 0.45f, 0.34f, 0.02f))
            .ConfigureAfterimageDensity(18f)
            .MatchAny(70, MotionTag.Wave, MotionTag.Zigzag, MotionTag.Spiral, MotionTag.Orbit, MotionTag.Return);

        Sustained.Configure(MovementSpeed(28f), MovementSpeed(65f), 1.1f, 320f, 0.075f, 1.1f, 1f, 0.12f,
                Afterimage(0.055f, 0.35f, 0.22f, 0.02f))
            .ConfigureAfterimageDensity(16f)
            .MatchAny(80, DeliveryTag.Field, FormTag.Beam, MotionTag.Vortex, MotionTag.Ground);

        Melee.Configure(0f, 0f, 0f, 0f, 0.04f, 1f, 1f, 0f,
                new AnimAfterimage
                {
                    Layout = AnimAfterimageLayout.Angular,
                    NewestAlpha = 0.38f,
                    OldestAlpha = 0.04f,
                    Tint = Color.white,
                    ArcRadius = 1.35f,
                    ArcDegreesPerLayer = 18f,
                    ArcDirection = 1f
                })
            .ConfigureFixedAfterimageLayers(4)
            .MatchAny(90, DeliveryTag.Melee, MotionTag.MeleeSweep);

        Snap.Configure(MovementSpeed(100f), MovementSpeed(220f), 0.2f, 1080f, 0.035f, 1f, 1f, 0f,
                Afterimage(0.09f, 0.5f, 0.45f, 0.015f))
            .ConfigureAfterimageDensity(20f)
            .MatchAny(100, MotionTag.Snap, MotionTag.Appear, MotionTag.Chain);
    }

    private static float MovementSpeed(float value)
    {
        return value * MovementSpeedMultiplier;
    }

    private static AnimAfterimage Afterimage(float spacingRatio, float minSpacing, float newestAlpha,
        float oldestAlpha)
    {
        return new AnimAfterimage
        {
            SpacingRatio = spacingRatio,
            MinSpacing = minSpacing,
            NewestAlpha = newestAlpha,
            OldestAlpha = oldestAlpha,
            LocalDirection = Vector2.left,
            Tint = Color.white
        };
    }
}
