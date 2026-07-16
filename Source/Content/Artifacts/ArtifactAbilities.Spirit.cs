using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using NeoModLoader.General;
using strings;
using UnityEngine;
using Cultiway.Content.Visuals;
using Friflo.Engine.ECS;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    private const string SpiritDamageRatio = "spirit_damage_ratio";
    private const string SpiritHealthRatio = "spirit_health_ratio";
    private const string SpiritArmorBonus = "spirit_armor_bonus";
    private const string SpiritRecoveryDuration = "spirit_recovery_duration";
    private const string SpiritGrowthMultiplier = "spirit_growth_multiplier";

    /// <summary>器灵成长被动；唤醒可恢复的持久器灵化身，并通过伤害、击杀和施法积累经验与羁绊。</summary>
    public static ArtifactAbilityAsset ArtifactSpiritAwakening { get; private set; }

    private static void ConfigureSpiritAbilities()
    {
        ConfigureArtifactSpiritAwakening();
    }

    private static void ConfigureArtifactSpiritAwakening()
    {
        ArtifactSpiritAwakening.name_key = "Cultiway.ArtifactAbility.ArtifactSpiritAwakening";
        ArtifactSpiritAwakening.tags = ["passive", "support", "summon", "spirit", "growth"];
        ArtifactSpiritAwakening.exclusive_group = "artifact_spirit";
        ArtifactSpiritAwakening.manifestation_cost = 1.45f;
        ArtifactSpiritAwakening.synergy_tags = ["soul", "spirituality", "sustain"];
        ArtifactSpiritAwakening.minimum_score = 1f;
        ArtifactSpiritAwakening.use_profile = new ArtifactUseProfile { offensive = 0.45f, support = 0.55f };
        ArtifactSpiritAwakening.control_complexity = 0.42f;
        ArtifactSpiritAwakening.thread_cost = 1;
        ArtifactSpiritAwakening.parameter_schema =
        [
            NumberSpec(SpiritDamageRatio),
            NumberSpec(SpiritHealthRatio),
            NumberSpec(SpiritArmorBonus),
            NumberSpec(SpiritRecoveryDuration),
            NumberSpec(SpiritGrowthMultiplier),
        ];
        ArtifactSpiritAwakening.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.ArtifactSpirit) *
            (0.58f + context.GetTrait(ArtifactMaterialTraits.Soul) * 0.38f +
             context.GetTrait(ArtifactMaterialTraits.Spirituality) * 0.28f +
             context.GetTrait(ArtifactMaterialTraits.Sustain) * 0.12f);
        ArtifactSpiritAwakening.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(
                SpiritDamageRatio,
                0.16f + context.scales.Potency * 0.045f +
                Mathf.Min(8f, context.GetTrait(ArtifactMaterialTraits.Soul)) * 0.018f),
            ArtifactAbilityValue.Number(
                SpiritHealthRatio,
                0.22f + context.scales.Capacity * 0.055f +
                Mathf.Min(8f, context.GetTrait(ArtifactMaterialTraits.Sustain)) * 0.02f),
            ArtifactAbilityValue.Number(
                SpiritArmorBonus,
                0.5f + context.scales.Potency * 0.5f +
                Mathf.Min(8f, context.GetTrait(ArtifactMaterialTraits.Hardness)) * 0.24f),
            ArtifactAbilityValue.Number(
                SpiritRecoveryDuration,
                Mathf.Max(8f, 42f / context.scales.Efficiency)),
            ArtifactAbilityValue.Number(
                SpiritGrowthMultiplier,
                0.8f + context.scales.Precision * 0.2f),
        ];
        ArtifactSpiritAwakening.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.ArtifactSpiritAwakening.Description"),
            ability.GetNumber(SpiritDamageRatio),
            ability.GetNumber(SpiritHealthRatio),
            ability.GetNumber(SpiritArmorBonus),
            ability.GetNumber(SpiritRecoveryDuration));
        ArtifactSpiritAwakening.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            event_minimum_state = ArtifactControlState.Ready,
            OnAttached = AwakenArtifactSpirit,
        });
        ArtifactSpiritAwakening.AwakenSpirit(new ArtifactSpiritAbilityProfile
        {
            minimum_state = ArtifactControlState.Ready,
            ResolveDamageRatio = (ability, _) => ability.GetNumber(SpiritDamageRatio),
            ResolveHealthRatio = (ability, _) => ability.GetNumber(SpiritHealthRatio),
            ResolveArmorBonus = (ability, _) => ability.GetNumber(SpiritArmorBonus),
            ResolveRecoveryDuration = (ability, _) => ability.GetNumber(SpiritRecoveryDuration),
        });
        ArtifactSpiritAwakening
            .Handle<ArtifactDamageDealtEvent>(GrowSpiritFromDamage)
            .Handle<ArtifactKillEvent>(GrowSpiritFromKill)
            .Handle<ArtifactSkillCastEvent>(GrowSpiritFromSkill);
    }

    private static void AwakenArtifactSpirit(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime)
    {
        if (!ArtifactSpiritService.Awaken(context.artifact)) return;
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            "spirit_awaken",
            intensity: ability.GetNumber(SpiritGrowthMultiplier));
    }

    private static void GrowSpiritFromDamage(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry _,
        ArtifactDamageDealtEvent evt)
    {
        float growth = ability.GetNumber(SpiritGrowthMultiplier);
        ArtifactSpiritService.AddExperience(
            context.artifact,
            Mathf.Sqrt(Mathf.Max(0f, evt.Damage)) * 0.018f * growth,
            0.001f * growth);
    }

    private static void GrowSpiritFromKill(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactKillEvent _)
    {
        float growth = ability.GetNumber(SpiritGrowthMultiplier);
        ArtifactSpiritService.AddExperience(context.artifact, 1.5f * growth, 0.06f * growth);
        ArtifactAbilityVisuals.Emit(context, ability, runtime, "spirit_growth", intensity: growth);
    }

    private static void GrowSpiritFromSkill(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactSkillCastEvent evt)
    {
        float growth = ability.GetNumber(SpiritGrowthMultiplier);
        ArtifactSpiritService.AddExperience(
            context.artifact,
            Mathf.Max(1, evt.EmittedCount) * 0.12f * growth,
            0.008f * growth);
        ArtifactAbilityVisuals.Emit(context, ability, runtime, "spirit_growth", intensity: growth * 0.7f);
    }

    private static void ConfigureArtifactSpiritAbilityVisuals()
    {
        ArtifactGlyphVisualCue spiritGlyph = Glyph(
                    ArtifactVisualAnchorKind.Artifact,
                    context => 0.28f * ArtifactAbilityVisuals.ResolveActorScale(context),
                    ArtifactVisualColorRole.Secondary,
                    ArtifactVfxStyles.Spirit,
                    matchActorScale: true);
        spiritGlyph.sides = 6;
        spiritGlyph.rotation_speed = 28f;
        ArtifactCompositeVisualCue awakening = new(
            ExpandingArea(
                ArtifactVisualAnchorKind.Artifact,
                context => 0.72f * ArtifactAbilityVisuals.ResolveActorScale(context),
                ArtifactVisualColorRole.Glow,
                ArtifactVfxStyles.Spirit),
            Burst(ArtifactVisualAnchorKind.Artifact, ArtifactVisualColorRole.Glow, 8, 0.45f),
            Pulse(1.32f, 1f, 0.18f));
        ArtifactCompositeVisualCue manifestation = new(
            ExpandingArea(
                ArtifactVisualAnchorKind.Point,
                _ => 0.62f,
                ArtifactVisualColorRole.Glow,
                ArtifactVfxStyles.Soul),
            Burst(ArtifactVisualAnchorKind.Point, ArtifactVisualColorRole.Primary, 7, 0.38f));
        ArtifactSpiritAwakening.Visualize(Theme(SkillVfxElements.Neg.AccentColor)
            .Loop(
                "spirit_glyph",
                spiritGlyph,
                context => context.artifact.GetComponent<ArtifactSpiritState>().awakened,
                "artifact.spirit.awakened",
                ArtifactVisualStackPolicy.Independent)
            .Signal("spirit_awaken", awakening, 0.9f)
            .Signal("spirit_manifest", manifestation, 0.7f)
            .Signal(
                "spirit_growth",
                new ArtifactCompositeVisualCue(
                    Sparkle(ArtifactVisualAnchorKind.Artifact, 0.07f, 0.9f, loop: false),
                    Pulse(1.12f, 1f, 0.1f)),
                0.42f,
                "artifact.spirit.growth",
                ArtifactVisualStackPolicy.SinglePerController));
    }
}
