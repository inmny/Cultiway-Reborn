using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Motions;
using UnityEngine;
using FormTag = Cultiway.Core.SkillLibV3.SkillTags.Form;
using MotionTag = Cultiway.Core.SkillLibV3.SkillTags.Motion;

namespace Cultiway.Content;

[Dependency(typeof(SkillTrajectories))]
public class SkillMotionProfiles : ExtendLibrary<SkillMotionProfileAsset, SkillMotionProfiles>
{
    public static SkillMotionProfileAsset Swift { get; private set; }
    public static SkillMotionProfileAsset Projectile { get; private set; }
    public static SkillMotionProfileAsset Patterned { get; private set; }
    public static SkillMotionProfileAsset Sustained { get; private set; }
    public static SkillMotionProfileAsset Snap { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.SkillMotionProfile";

    protected override void OnInit()
    {
        Swift.Configure(78f, 180f, 0.38f, 720f, 0.045f, 1.25f, 1f, 0.07f,
                Afterimage(6, 0.085f, 0.5f, 0.42f, 0.02f))
            .MatchAny(50, FormTag.Slash, MotionTag.Direct, MotionTag.Homing);

        // 落石、落木虽采用竖直坠落几何，节奏仍按高速弹丸处理。
        Projectile.Configure(62f, 150f, 0.48f, 540f, 0.055f, 1.2f, 1f, 0.08f,
                Afterimage(4, 0.075f, 0.45f, 0.34f, 0.025f))
            .MatchAny(60, FormTag.Ball, FormTag.Pierce, MotionTag.Falling, MotionTag.Rain);

        Patterned.Configure(52f, 120f, 0.72f, 600f, 0.055f, 1.15f, 1f, 0.09f,
                Afterimage(5, 0.075f, 0.45f, 0.34f, 0.02f))
            .MatchAny(70, MotionTag.Wave, MotionTag.Zigzag, MotionTag.Spiral, MotionTag.Orbit, MotionTag.Return);

        Sustained.Configure(28f, 65f, 1.1f, 320f, 0.075f, 1.1f, 1f, 0.12f,
                Afterimage(3, 0.055f, 0.35f, 0.22f, 0.02f))
            .MatchAny(80, FormTag.Sustain, MotionTag.Vortex, MotionTag.Ground);

        Snap.Configure(100f, 220f, 0.2f, 1080f, 0.035f, 1f, 1f, 0f,
                Afterimage(6, 0.09f, 0.5f, 0.45f, 0.015f))
            .MatchAny(100, MotionTag.Snap, MotionTag.Appear);
    }

    private static AnimAfterimage Afterimage(int count, float spacingRatio, float minSpacing, float newestAlpha,
        float oldestAlpha)
    {
        return new AnimAfterimage
        {
            Count = count,
            SpacingRatio = spacingRatio,
            MinSpacing = minSpacing,
            NewestAlpha = newestAlpha,
            OldestAlpha = oldestAlpha,
            LocalDirection = Vector2.left,
            Tint = Color.white
        };
    }
}
