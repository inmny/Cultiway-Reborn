using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Core.Semantics;

namespace Cultiway.Content;

public partial class SkillEntities
{
    internal static SkillEntityAsset BalefulWindTribulationCenter { get; private set; }
    internal static SkillEntityAsset BalefulWindTribulationWave { get; private set; }

    private static void ConfigureBalefulWindTribulation()
    {
        var wind = new ElementComposition(
            wood: 0.35f,
            water: 0.25f,
            neg: 0.25f,
            entropy: 0.15f,
            normalize: true);
        var centerImpact = new SkillImpactProfileAsset
        {
            id = "Cultiway.SkillImpactProfile.BalefulWindTribulationCenter",
            Kind = SkillImpactKind.Field,
            DamageMultiplier = 0f,
            ContinueAfterHit = true,
            Lifetime = 60f,
            PersistentLimit = 0,
            ExpectedTargets = 0f
        };
        var centerAnimation = SkillEntityAnimation
            .Create(
                "cultiway/effect/tornado/1/runtime",
                0.055f,
                SkillEntityAnimationSettings.Inherit.WithFrameRate(16f))
            .WithAppearance(
                "cultiway/effect/tornado/1/appearance",
                SkillEntityAnimationSettings.Inherit.WithFrameRate(16f),
                SkillAnimationGameplayFlags.Movement)
            .WithDissipation(
                "cultiway/effect/tornado/1/dissipation",
                SkillEntityAnimationSettings.Inherit.WithFrameRate(16f));

        BalefulWindTribulationCenter.Element = wind;
        BalefulWindTribulationCenter.Type = SkillEntityType.Attack;
        BalefulWindTribulationCenter
            .AddSemantics(
                SkillSemantics.Element.Wind,
                SkillSemantics.Form.Aoe,
                SkillSemantics.Form.Sustain,
                SkillSemantics.Motion.Vortex,
                SkillSemantics.Delivery.Field)
            .RequireCastResource(SkillCastResources.Wakan)
            .SetWorldVisual(new SkillWorldVisualProfile())
            .SetupCommonPrefab(centerAnimation, true)
            .SetupVisualRotation(VisualRotation.FixedUpright())
            .SetupImpactProfile(centerImpact, default)
            .SetupDefaultTraj(SkillTrajectories.FieldFollowTarget)
            .SetupUseProfile(SkillUseProfileLibrary.CasterSelf)
            .AcceptTrajectoryDomains(SkillTrajectoryDomain.MobileField)
            .SetDealsBaseDamage(false);

        var waveAnimation = SkillEntityAnimation.Create(
            "cultiway/effect/tornado/3/runtime",
            0.025f,
            SkillEntityAnimationSettings.Inherit.WithFrameRate(16f));
        BalefulWindTribulationWave.Element = wind;
        BalefulWindTribulationWave.Type = SkillEntityType.Attack;
        BalefulWindTribulationWave
            .AddSemantics(
                SkillSemantics.Element.Wind,
                SkillSemantics.Form.Single,
                SkillSemantics.Form.Sustain,
                SkillSemantics.Motion.Vortex,
                SkillSemantics.Effect.Displace,
                SkillSemantics.Delivery.Projectile,
                SkillSemantics.Role.Offensive)
            .RequireCastResource(SkillCastResources.Wakan)
            .SetWorldVisual(new SkillWorldVisualProfile())
            .SetupCommonPrefab(waveAnimation, true)
            .SetupVisualRotation(VisualRotation.FixedUpright())
            .SetupImpactProfile(
                SkillImpactProfileLibrary.NormalProjectile,
                new ColliderConfig
                {
                    Enabled = true,
                    Actor = true,
                    ExplicitTargetOnly = true
                })
            .SetupDefaultTraj(SkillTrajectories.TowardsTarget)
            .SetupUseProfile(SkillUseProfileLibrary.EnemyObjectOrPoint)
            .AcceptTrajectoryDomains(SkillTrajectoryDomain.FlyingBody);
        BalefulWindTribulationWave.OnObjCollision = BalefulWindTribulationSkillService.ResolveWaveImpact;
    }
}
