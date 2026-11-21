using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using NeoModLoader.General;
using strings;
using UnityEngine;
using System;
using Cultiway.Content.Visuals;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Visuals;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    /// <summary>可部署万魂幡阵；持续伤害敌人、抽取灵气并施加弱化。</summary>
    public static ArtifactAbilityAsset MyriadSoulBannerArray { get; private set; }

    /// <summary>可部署号令法域；持续为范围内友军施加提高伤害的启明增益。</summary>
    public static ArtifactAbilityAsset CommandingBannerField { get; private set; }

    /// <summary>召役主动能力；击杀可储存魂力，施放时消耗魂力与灵气召唤多名限时魂体。</summary>
    public static ArtifactAbilityAsset SpiritHostManifestation { get; private set; }

    private static void ConfigureMyriadSoulBannerArray()
    {
        MyriadSoulBannerArray.name_key = "Cultiway.ArtifactAbility.MyriadSoulBannerArray";
        MyriadSoulBannerArray.SetSemantics(
            ArtifactSemantics.Delivery.Deployment,
            ArtifactSemantics.Delivery.Field,
            ArtifactSemantics.Element.Neg,
            ArtifactSemantics.Theme.Soul);
        MyriadSoulBannerArray.exclusivity = ArtifactAbilityExclusivity.SoulField;
        MyriadSoulBannerArray.manifestation_cost = 1.45f;
        MyriadSoulBannerArray.AddSynergies(
            ArtifactSemantics.Theme.Soul,
            ArtifactSemantics.Effect.Devouring,
            ArtifactSemantics.Delivery.Field);
        MyriadSoulBannerArray.AddConflicts(ArtifactSemanticRules.PurificationField);
        MyriadSoulBannerArray.minimum_score = 1f;
        MyriadSoulBannerArray.use_profile = new ArtifactUseProfile { offensive = 0.8f, support = 0.25f };
        MyriadSoulBannerArray.control_complexity = 0.52f;
        MyriadSoulBannerArray.thread_cost = 2;
        MyriadSoulBannerArray.parameter_schema =
        [
            NumberSpec(AttackRange), NumberSpec(EffectRadius), NumberSpec(EffectDuration),
            NumberSpec(DamageMultiplier), NumberSpec(DrainAmount), NumberSpec(StatusDuration),
            NumberSpec(Cooldown), NumberSpec(ActivationCost), NumberSpec(MaintenanceCost),
        ];
        MyriadSoulBannerArray.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.Soul) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.FieldProjection) * 0.45f +
             context.GetTrait(ArtifactMaterialTraits.Devouring) * 0.25f +
             context.GetTrait(ArtifactMaterialTraits.Neg) * 0.15f);
        MyriadSoulBannerArray.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 8f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectRadius, 3.2f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectDuration, 6f * context.scales.Duration),
            ArtifactAbilityValue.Number(DamageMultiplier, 0.13f * context.scales.Potency),
            ArtifactAbilityValue.Number(DrainAmount, 1.3f * context.scales.Potency),
            ArtifactAbilityValue.Number(StatusDuration, 1.4f * context.scales.Duration),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 15f, 4.5f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 4.5f)),
            ArtifactAbilityValue.Number(MaintenanceCost, ScaledCost(context, 0.48f)),
        ];
        MyriadSoulBannerArray.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.MyriadSoulBannerArray.Description"),
            ability.GetNumber(AttackRange), ability.GetNumber(EffectRadius),
            ability.GetNumber(DamageMultiplier), ability.GetNumber(EffectDuration));
        MyriadSoulBannerArray.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            tick_minimum_state = ArtifactControlState.Operating,
            tick_interval = 0.5f,
            tick_requires_activity = true,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            ResolveMaintenanceCost = (_, ability) => ability.GetNumber(MaintenanceCost),
            Resource = SkillCastResources.Wakan,
            OnTick = ApplyMyriadSoulBannerArray,
            OnActivityEnded = EndPaidArtifactArea,
        });
        MyriadSoulBannerArray.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.Point,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 7,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            ResolveEffectRadius = (_, ability) => ability.GetNumber(EffectRadius),
            CanPrepare = CanPrepareFreeBody,
            CanUse = CanDeployInRange,
            TryUse = DeploySoulBannerArray,
        });
    }

    private static bool DeploySoulBannerArray(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin _)
    {
        return DeployAtTarget(
            context,
            ability,
            ref runtime,
            target,
            ArtifactBodyAnchorRef.Appearance("cloth", "focus"));
    }

    private static void ApplyMyriadSoulBannerArray(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry _,
        float interval)
    {
        float drain = ability.GetNumber(DrainAmount) * context.effect_scale;
        float duration = ability.GetNumber(StatusDuration);
        var carrier = ArtifactAbilityLifecycle.ResolveSkillContext(context);
        var contact = new ArtifactAreaContact((source, target) =>
        {
            using var scope = SkillCasterContextService.Enter(in carrier);
            CombatResourceEffects.DrainWakan(target, drain);
            CombatStatusEffects.ApplyStatus(target, StatusEffects.Weaken, duration, S.multiplier_damage, -.12f, source);
        });
        StartPaidArtifactArea(context, MyriadSoulBannerArray, ability, interval, "myriad_banner", contact,
            SoulComposition, SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier));
    }

    private static void ConfigureCommandingBannerField()
    {
        CommandingBannerField.name_key = "Cultiway.ArtifactAbility.CommandingBannerField";
        CommandingBannerField.SetSemantics(
            ArtifactSemantics.Delivery.Deployment,
            ArtifactSemantics.Delivery.Field,
            ArtifactSemantics.Effect.Amplification);
        CommandingBannerField.exclusivity = ArtifactAbilityExclusivity.CommandField;
        CommandingBannerField.manifestation_cost = 1.25f;
        CommandingBannerField.AddSynergies(
            ArtifactSemantics.Role.Support,
            ArtifactSemantics.Theme.Sound,
            ArtifactSemantics.Form.Sustain);
        CommandingBannerField.AddConflicts(ArtifactSemanticRules.Concealment);
        CommandingBannerField.minimum_score = 1f;
        CommandingBannerField.use_profile = new ArtifactUseProfile { support = 1f, offensive = 0.3f };
        CommandingBannerField.control_complexity = 0.4f;
        CommandingBannerField.thread_cost = 1;
        CommandingBannerField.parameter_schema =
        [
            NumberSpec(AttackRange), NumberSpec(EffectRadius), NumberSpec(EffectDuration),
            NumberSpec(StatusDuration), NumberSpec(DamageBonus), NumberSpec(Cooldown),
            NumberSpec(ActivationCost), NumberSpec(MaintenanceCost),
        ];
        CommandingBannerField.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.FieldProjection) +
             context.GetTrait(ArtifactMaterialTraits.Amplification) * 0.75f) *
            (0.5f + context.GetTrait(ArtifactMaterialTraits.Sound) * 0.25f +
             context.GetTrait(ArtifactMaterialTraits.Sustain) * 0.3f +
             context.GetTrait(ArtifactMaterialTraits.Pos) * 0.16f);
        CommandingBannerField.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 7f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectRadius, 3.4f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectDuration, 7f * context.scales.Duration),
            ArtifactAbilityValue.Number(StatusDuration, 1.5f * context.scales.Duration),
            ArtifactAbilityValue.Number(DamageBonus, Mathf.Clamp(0.08f * context.scales.Potency, 0.08f, 0.42f)),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 16f, 5f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 3.8f)),
            ArtifactAbilityValue.Number(MaintenanceCost, ScaledCost(context, 0.38f)),
        ];
        CommandingBannerField.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.CommandingBannerField.Description"),
            ability.GetNumber(EffectRadius), ability.GetNumber(DamageBonus),
            ability.GetNumber(EffectDuration), ability.GetNumber(Cooldown));
        CommandingBannerField.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            tick_minimum_state = ArtifactControlState.Operating,
            tick_interval = 0.5f,
            tick_requires_activity = true,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            ResolveMaintenanceCost = (_, ability) => ability.GetNumber(MaintenanceCost),
            Resource = SkillCastResources.Wakan,
            OnTick = ApplyCommandingBannerField,
            OnActivityEnded = EndPaidArtifactArea,
        });
        CommandingBannerField.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World,
            target_mode = ActiveAbilityTargetMode.Point,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 5,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            ResolveEffectRadius = (_, ability) => ability.GetNumber(EffectRadius),
            CanPrepare = CanPrepareFreeBody,
            CanUse = CanDeployInRange,
            TryUse = DeployCommandingBannerField,
        });
    }

    private static bool DeployCommandingBannerField(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin _)
    {
        return DeployAtTarget(
            context,
            ability,
            ref runtime,
            target,
            ArtifactBodyAnchorRef.Appearance("cloth", "focus"));
    }

    private static void ApplyCommandingBannerField(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry _,
        float interval)
    {
        float duration = ability.GetNumber(StatusDuration), damageBonus = ability.GetNumber(DamageBonus);
        var carrier = ArtifactAbilityLifecycle.ResolveSkillContext(context);
        var contact = new ArtifactAreaContact((source, target) =>
        {
            using var scope = SkillCasterContextService.Enter(in carrier);
            CombatStatusEffects.ApplyStatus(target, StatusEffects.Enlighten, duration, S.multiplier_damage, damageBonus, source);
        }, CombatTargeting.TargetDisposition.Friendly);
        StartPaidArtifactArea(context, CommandingBannerField, ability, interval, "command_banner", contact,
            ElementComposition.Static.empty);
    }

    private static void ConfigureSpiritHostManifestation()
    {
        SpiritHostManifestation.name_key = "Cultiway.ArtifactAbility.SpiritHostManifestation";
        SpiritHostManifestation.SetSemantics(
            ArtifactSemantics.Effect.Summon,
            ArtifactSemantics.Theme.Soul,
            ArtifactSemantics.Effect.Storage);
        SpiritHostManifestation.exclusivity = ArtifactAbilityExclusivity.SpiritHost;
        SpiritHostManifestation.manifestation_cost = 1.6f;
        SpiritHostManifestation.AddSynergies(
            ArtifactSemantics.Theme.Soul,
            ArtifactSemantics.Effect.Storage,
            ArtifactSemantics.Form.Sustain);
        SpiritHostManifestation.AddConflicts(ArtifactSemanticRules.SoulPurification);
        SpiritHostManifestation.minimum_score = 1f;
        SpiritHostManifestation.use_profile = new ArtifactUseProfile { offensive = 0.75f, support = 0.35f };
        SpiritHostManifestation.control_complexity = 0.65f;
        SpiritHostManifestation.thread_cost = 2;
        SpiritHostManifestation.parameter_schema =
        [
            NumberSpec(AttackRange), NumberSpec(EffectDuration), NumberSpec(StorageCapacity),
            NumberSpec(StorePerTrigger), NumberSpec(StoredResourceCost), IntegerSpec(SummonCount),
            NumberSpec(Cooldown), NumberSpec(ActivationCost),
        ];
        SpiritHostManifestation.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Soul) +
             context.GetTrait(ArtifactMaterialTraits.Storage) * 0.65f) *
            (0.5f + context.GetTrait(ArtifactMaterialTraits.Sustain) * 0.3f +
             context.GetTrait(ArtifactMaterialTraits.Spirituality) * 0.18f);
        SpiritHostManifestation.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 8f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectDuration, 8f * context.scales.Duration),
            ArtifactAbilityValue.Number(StorageCapacity, 8f * context.scales.Capacity),
            ArtifactAbilityValue.Number(StorePerTrigger, Mathf.Max(0.5f, context.scales.Potency * 0.75f)),
            ArtifactAbilityValue.Number(StoredResourceCost, 2f),
            ArtifactAbilityValue.Integer(SummonCount, Mathf.Clamp(Mathf.FloorToInt(context.scales.Capacity), 1, 5)),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 18f, 6f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 3f)),
        ];
        SpiritHostManifestation.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.SpiritHostManifestation.Description"),
            ability.GetNumber(StorageCapacity), ability.GetNumber(StorePerTrigger),
            ability.GetInteger(SummonCount), ability.GetNumber(EffectDuration));
        SpiritHostManifestation.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            event_minimum_state = ArtifactControlState.Ready,
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            Resource = SkillCastResources.Wakan,
            OnAttached = ConfigureSoulStorage,
        });
        SpiritHostManifestation.Handle<ArtifactKillEvent>(StoreSoulEssence);
        SpiritHostManifestation.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.ObjectOrPoint,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 6,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            CanUse = CanManifestSpiritHost,
            TryUse = ManifestSpiritHost,
        });
    }

    private static void ConfigureSoulStorage(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry _)
    {
        ref ArtifactStorageState storage = ref context.artifact.GetComponent<ArtifactStorageState>();
        ArtifactStorageOperations.Configure(ref storage, ArtifactStorageOperations.SoulEssence,
            ability.GetNumber(StorageCapacity));
    }

    private static void StoreSoulEssence(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactKillEvent evt)
    {
        if (evt.Victim == null) return;
        ref ArtifactStorageState storage = ref context.artifact.GetComponent<ArtifactStorageState>();
        float stored = ArtifactStorageOperations.Store(
            ref storage,
            ArtifactStorageOperations.SoulEssence,
            ability.GetNumber(StorePerTrigger) * context.effect_scale);
        if (stored > 0f)
        {
            ArtifactAbilityVisuals.Emit(
                context,
                ability,
                runtime,
                ArtifactVisualChannels.Drain,
                evt.Victim.current_position,
                target: evt.Victim,
                intensity: stored);
        }
    }

    private static bool CanManifestSpiritHost(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target)
    {
        if (!CanUseWorldTargetInRange(context, ability, runtime, target)) return false;
        ArtifactStorageState storage = context.artifact.GetComponent<ArtifactStorageState>();
        return ArtifactStorageOperations.GetAmount(storage, ArtifactStorageOperations.SoulEssence) >=
               ability.GetNumber(StoredResourceCost);
    }

    private static bool ManifestSpiritHost(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin _)
    {
        ref ArtifactStorageState storage = ref context.artifact.GetComponent<ArtifactStorageState>();
        float cost = ability.GetNumber(StoredResourceCost);
        ArtifactStorageOperations.Take(ref storage, ArtifactStorageOperations.SoulEssence, cost);
        int count = ArtifactSummonService.SummonSpirits(
            context,
            ability.instance_id,
            TargetPosition(target),
            target.Object,
            ability.GetInteger(SummonCount),
            ability.GetNumber(EffectDuration),
            ability.GetNumber(StorePerTrigger));
        if (count == 0)
        {
            ArtifactStorageOperations.Store(ref storage, ArtifactStorageOperations.SoulEssence, cost);
            return false;
        }

        ArtifactAbilityLifecycle.BeginTimedActivity(ref runtime);
        return true;
    }

    private static void ConfigureBannerAbilityVisuals()
    {
        ArtifactOrbitProjectionVisualCue host = new ArtifactOrbitProjectionVisualCue
        {
            anchor = ArtifactVisualAnchorKind.Controller,
            ResolveCount = context => context.ability.GetInteger(SummonCount),
            radius = 0.74f,
            vertical_ratio = 0.5f,
            angular_speed = -72f,
            alpha = 0.34f,
            pulse_amplitude = 0.08f,
        };
        ArtifactGlyphVisualCue gate = ActivityGlyph(
            ArtifactVisualAnchorKind.Controller,
            (ArtifactAbilityVisualContext _) => 0.72f,
            ArtifactVisualColorRole.Secondary,
            8,
            -30f,
            ArtifactVfxStyles.Soul
        );
        ArtifactBeamVisualCue harvest = Beam(
            ArtifactVisualAnchorRef.Appearance("cloth", "focus"),
            ArtifactVisualAnchorKind.Target,
            ArtifactVisualColorRole.Glow,
            ArtifactVfxStyles.Soul
        );
        harvest.width = 0.05f;
        SpiritHostManifestation.Visualize(
            Theme(SkillVfxElements.Entropy.AccentColor)
                .Loop("spirit_host", new ArtifactCompositeVisualCue(host, gate), IsActivityActive)
                .Signal(
                    ArtifactVisualChannels.Drain,
                    new ArtifactCompositeVisualCue(
                        harvest,
                        Burst(ArtifactVisualAnchorKind.Target, ArtifactVisualColorRole.Primary, 6)
                    ),
                    0.55f,
                    "artifact.spirit_host.harvest",
                    ArtifactVisualStackPolicy.MergeIntensity
                )
                .Signal(
                    ArtifactVisualChannels.Trigger,
                    new ArtifactCompositeVisualCue(
                        ExpandingArea(
                            ArtifactVisualAnchorKind.Point,
                            (ArtifactAbilityVisualContext _) => 1.2f,
                            ArtifactVisualColorRole.Glow,
                            ArtifactVfxStyles.Soul
                        ),
                        Burst(
                            ArtifactVisualAnchorKind.Point,
                            ArtifactVisualColorRole.Primary,
                            10,
                            0.5f
                        )
                    ),
                    0.58f
                )
        );
    }
}
