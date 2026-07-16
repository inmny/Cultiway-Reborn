using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Visuals;
using Cultiway.Core.Components;
using UnityEngine;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    private static void ConfigureVisuals()
    {
        ConfigureFlyingSwordVisuals();
        ConfigureDingAlchemyVisuals();
        ConfigureGuardianWardVisuals();
        ConfigureMirrorInsightVisuals();
        ConfigureVitalityRenewalVisuals();
        ConfigureSpiritReservoirVisuals();
        ConfigureSuppressionFieldVisuals();
    }

    private static void ConfigureFlyingSwordVisuals()
    {
        ArtifactAnimVisualCue flash = Sparkle(
            ArtifactVisualAnchorKind.Artifact,
            0.055f,
            0.9f,
            loop: false);
        FlyingSwordAttack.Visualize(new ArtifactAbilityVisualProfile
            {
                fallback_theme = ArtifactVisualTheme.FromPrimary(SkillVfxElements.Metal.AccentColor),
            }
            .Signal(ArtifactVisualChannels.Trigger, flash, 0.35f)
            .Signal(ArtifactVisualChannels.End, flash, 0.35f));
    }

    private static void ConfigureDingAlchemyVisuals()
    {
        ArtifactAnimVisualCue flame = new("effects/fx_status_burning_t_3")
        {
            anchor = ArtifactVisualAnchorKind.Artifact,
            color_role = ArtifactVisualColorRole.Primary,
            offset = new Vector3(0f, 0.08f, 0.15f),
            scale = 0.05f,
            frame_interval = 0.09f,
            alpha = 0.82f,
            scale_pulse_amplitude = 0.06f,
            loop = true,
        };
        ArtifactCompositeVisualCue result = new(
            Sparkle(ArtifactVisualAnchorKind.Artifact, 0.07f, 1f, loop: false),
            new ArtifactAreaVisualCue
            {
                anchor = ArtifactVisualAnchorKind.Artifact,
                color_role = ArtifactVisualColorRole.Glow,
                ResolveRadius = context => 0.65f * ArtifactAbilityVisuals.ResolveActorScale(context),
                line_alpha = 0.65f,
                fill_alpha = 0.04f,
                start_scale = 0.2f,
                end_scale = 1.15f,
                fade_out = true,
                show_inner_ring = false,
            });
        DingAlchemyAssist.Visualize(new ArtifactAbilityVisualProfile
            {
                fallback_theme = ArtifactVisualTheme.FromPrimary(SkillVfxElements.Fire.AccentColor),
            }
            .Signal(
                ArtifactVisualChannels.CraftStep,
                flame,
                1f,
                "artifact.alchemy.flame",
                ArtifactVisualStackPolicy.MergeIntensity)
            .Signal(
                ArtifactVisualChannels.CraftResult,
                result,
                0.55f,
                "artifact.alchemy.result",
                ArtifactVisualStackPolicy.MergeIntensity));
    }

    private static void ConfigureGuardianWardVisuals()
    {
        ArtifactAreaVisualCue readyRune = new()
        {
            anchor = ArtifactVisualAnchorKind.Artifact,
            color_role = ArtifactVisualColorRole.Glow,
            ResolveRadius = context => 0.3f * ArtifactAbilityVisuals.ResolveActorScale(context),
            line_alpha = 0.16f,
            fill_alpha = 0f,
            line_width = 0.045f,
            pulse_amplitude = 0.04f,
            pulse_speed = 2.2f,
            show_inner_ring = false,
        };
        ArtifactAnimVisualCue shield = new("effects/fx_status_shield_t")
        {
            anchor = ArtifactVisualAnchorKind.Controller,
            color_role = ArtifactVisualColorRole.Glow,
            offset = new Vector3(0f, 0.08f, 0.2f),
            scale = 0.1f,
            frame_interval = 0.075f,
            alpha = 0.95f,
            loop = false,
            visual_rotation = VisualRotation.FollowRotation(),
        };
        GuardianWard.Visualize(new ArtifactAbilityVisualProfile
            {
                fallback_theme = ArtifactVisualTheme.FromPrimary(SkillVfxElements.Metal.AccentColor),
            }
            .Loop(
                "ready_rune",
                readyRune,
                context => ArtifactAbilityLifecycle.MeetsState(context.control_state, ArtifactControlState.Ready),
                "artifact.guardian.ready",
                ArtifactVisualStackPolicy.Strongest,
                context => context.ability.GetNumber(DamageReduction))
            .Signal(
                ArtifactVisualChannels.Guard,
                shield,
                0.48f,
                "artifact.guardian.guard",
                ArtifactVisualStackPolicy.Strongest));
    }

    private static void ConfigureMirrorInsightVisuals()
    {
        ArtifactAnimVisualCue glint = Sparkle(
            ArtifactVisualAnchorKind.Artifact,
            0.035f,
            0.36f,
            loop: true);
        glint.frame_interval = 0.16f;
        glint.alpha_pulse_period = 2.8f;
        glint.alpha_pulse_floor = 0.02f;
        MirrorInsight.Visualize(new ArtifactAbilityVisualProfile
            {
                fallback_theme = ArtifactVisualTheme.FromPrimary(SkillVfxElements.Pos.AccentColor),
            }
            .Loop(
                "insight_glint",
                glint,
                context => ArtifactAbilityLifecycle.MeetsState(
                    context.control_state,
                    ArtifactControlState.Operating),
                "artifact.insight.glint",
                ArtifactVisualStackPolicy.Strongest,
                context => context.ability.GetNumber(AccuracyBonus) * 0.04f +
                           context.ability.GetNumber(CriticalBonus)));
    }

    private static void ConfigureVitalityRenewalVisuals()
    {
        ArtifactCompositeVisualCue healPulse = new(
            new ArtifactAreaVisualCue
            {
                anchor = ArtifactVisualAnchorKind.Controller,
                color_role = ArtifactVisualColorRole.Primary,
                ResolveRadius = context => 0.85f * ArtifactAbilityVisuals.ResolveActorScale(context),
                line_alpha = 0.72f,
                fill_alpha = 0.055f,
                start_scale = 0.18f,
                end_scale = 1.12f,
                fade_out = true,
                show_inner_ring = false,
            },
            new ArtifactAnimVisualCue("effects/fx_status_taking_roots_t")
            {
                anchor = ArtifactVisualAnchorKind.Controller,
                color_role = ArtifactVisualColorRole.Glow,
                offset = new Vector3(0f, 0f, 0.12f),
                scale = 0.065f,
                frame_interval = 0.08f,
                alpha = 0.62f,
                loop = false,
            });
        VitalityRenewal.Visualize(new ArtifactAbilityVisualProfile
            {
                fallback_theme = ArtifactVisualTheme.FromPrimary(SkillVfxElements.Wood.AccentColor),
            }
            .Signal(
                ArtifactVisualChannels.Tick,
                healPulse,
                0.62f,
                "artifact.vitality.heal",
                ArtifactVisualStackPolicy.MergeIntensity));
    }

    private static void ConfigureSpiritReservoirVisuals()
    {
        ArtifactCompositeVisualCue reservoir = new(
            new ArtifactAreaVisualCue
            {
                anchor = ArtifactVisualAnchorKind.Controller,
                color_role = ArtifactVisualColorRole.Primary,
                ResolveRadius = context => 0.68f * ArtifactAbilityVisuals.ResolveActorScale(context),
                line_alpha = 0.18f,
                fill_alpha = 0.022f,
                line_width = 0.055f,
                inner_radius_ratio = 0.58f,
                inner_rotation_speed = -16f,
                pulse_amplitude = 0.045f,
                pulse_speed = 1.8f,
            },
            new ArtifactAnimVisualCue("effects/fx_status_motivated_t")
            {
                anchor = ArtifactVisualAnchorKind.Controller,
                color_role = ArtifactVisualColorRole.Glow,
                offset = new Vector3(0f, 0.2f, 0.1f),
                scale = 0.06f,
                frame_interval = 0.13f,
                alpha = 0.3f,
                alpha_pulse_period = 1.9f,
                alpha_pulse_floor = 0.12f,
                loop = true,
            });
        SpiritReservoir.Visualize(new ArtifactAbilityVisualProfile
            {
                fallback_theme = ArtifactVisualTheme.FromPrimary(SkillVfxElements.Pos.AccentColor),
            }
            .Loop(
                "spirit_reservoir",
                reservoir,
                context => ArtifactAbilityLifecycle.MeetsState(
                    context.control_state,
                    ArtifactControlState.Operating),
                "artifact.spirit_reservoir",
                ArtifactVisualStackPolicy.MergeIntensity,
                ResolveSpiritReservoirVisualIntensity));
    }

    private static void ConfigureSuppressionFieldVisuals()
    {
        ArtifactAreaVisualCue field = FieldArea(0.52f, 0.065f);
        field.pulse_amplitude = 0.025f;
        field.pulse_speed = 2.8f;
        field.inner_rotation_speed = -12f;

        ArtifactAreaVisualCue deploy = FieldArea(0.72f, 0.08f);
        deploy.start_scale = 0.15f;
        deploy.end_scale = 1f;

        ArtifactAreaVisualCue pulse = FieldArea(0.66f, 0.025f);
        pulse.start_scale = 0.62f;
        pulse.end_scale = 1f;
        pulse.fade_out = true;

        ArtifactAreaVisualCue collapse = FieldArea(0.78f, 0.07f);
        collapse.anchor = ArtifactVisualAnchorKind.Point;
        collapse.start_scale = 1f;
        collapse.end_scale = 0.12f;
        collapse.fade_out = true;

        SuppressionField.Visualize(new ArtifactAbilityVisualProfile
            {
                fallback_theme = ArtifactVisualTheme.FromPrimary(SkillVfxElements.Gravity.AccentColor),
            }
            .Loop(
                "suppression_field",
                field,
                context => context.runtime.activity_kind == ArtifactAbilityActivityKind.Deployment)
            .Signal(ArtifactVisualChannels.Trigger, deploy, 0.42f)
            .Signal(
                ArtifactVisualChannels.Tick,
                pulse,
                0.45f)
            .Signal(ArtifactVisualChannels.End, collapse, 0.5f));
    }

    private static ArtifactAreaVisualCue FieldArea(float lineAlpha, float fillAlpha)
    {
        return new ArtifactAreaVisualCue
        {
            anchor = ArtifactVisualAnchorKind.DeploymentOrigin,
            color_role = ArtifactVisualColorRole.Primary,
            ResolveRadius = context => context.ability.GetNumber(FieldRadius),
            line_alpha = lineAlpha,
            fill_alpha = fillAlpha,
            line_width = 0.09f,
            inner_radius_ratio = 0.7f,
            show_inner_ring = true,
        };
    }

    private static ArtifactAnimVisualCue Sparkle(
        ArtifactVisualAnchorKind anchor,
        float scale,
        float alpha,
        bool loop)
    {
        return new ArtifactAnimVisualCue("effects/tile_effects/enchanted_sparkle")
        {
            anchor = anchor,
            color_role = ArtifactVisualColorRole.Glow,
            offset = new Vector3(0f, 0.05f, 0.18f),
            scale = scale,
            frame_interval = 0.08f,
            alpha = alpha,
            scale_pulse_amplitude = 0.08f,
            loop = loop,
        };
    }

    private static float ResolveSpiritReservoirVisualIntensity(ArtifactAbilityVisualContext context)
    {
        ActorBinder binder = context.controller.GetComponent<ActorBinder>();
        if (!binder.AE.HasCultisys<Xian>()) return 0.2f;
        ref Xian xian = ref binder.AE.GetCultisys<Xian>();
        float capacity = Mathf.Max(1f, binder.Actor.stats[BaseStatses.MaxWakan.id]);
        return Mathf.Lerp(0.2f, 1f, 1f - Mathf.Clamp01(xian.wakan / capacity));
    }
}
