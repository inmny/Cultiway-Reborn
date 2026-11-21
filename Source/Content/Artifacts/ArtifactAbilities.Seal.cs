using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
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
using Friflo.Engine.ECS;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    /// <summary>坠印部署主动；法印落地后造成范围伤害、眩晕和冲击力。</summary>
    public static ArtifactAbilityAsset MountainSealFall { get; private set; }

    /// <summary>可部署禁法法域；周期性沉默范围内敌人并抽取其灵气。</summary>
    public static ArtifactAbilityAsset SpellBanningEdict { get; private set; }

    /// <summary>单体封脉主动；对目标施加减速与弱化，并抽取灵气。</summary>
    public static ArtifactAbilityAsset MeridianSealing { get; private set; }

    private static void ConfigureMountainSealFall()
    {
        MountainSealFall.name_key = "Cultiway.ArtifactAbility.MountainSealFall";
        MountainSealFall.SetSemantics(
            ArtifactSemantics.Delivery.Deployment,
            ArtifactSemantics.Effect.Impact,
            ArtifactSemantics.Effect.Control);
        MountainSealFall.exclusivity = ArtifactAbilityExclusivity.HeavyImpact;
        MountainSealFall.manifestation_cost = 1.45f;
        MountainSealFall.AddSynergies(ArtifactSemantics.Effect.Suppression, ArtifactSemantics.Delivery.Field);
        MountainSealFall.AddConflicts(ArtifactSemanticRules.Lightweight);
        MountainSealFall.minimum_score = 1f;
        MountainSealFall.use_profile = new ArtifactUseProfile { offensive = 0.9f, defensive = 0.15f };
        MountainSealFall.control_complexity = 0.46f;
        MountainSealFall.thread_cost = 1;
        MountainSealFall.parameter_schema =
        [
            NumberSpec(AttackRange),
            NumberSpec(EffectRadius),
            NumberSpec(DamageMultiplier),
            NumberSpec(ForceStrength),
            NumberSpec(StatusDuration),
            NumberSpec(ImpactDelay),
            NumberSpec(EffectDuration),
            NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
        ];
        MountainSealFall.state_schema =
        [
            new ArtifactAbilityValueSpec
            {
                key = ImpactDone,
                kind = ArtifactAbilityValueKind.Boolean,
                required = true,
            },
        ];
        MountainSealFall.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Impact) +
             context.GetTrait(ArtifactMaterialTraits.Hardness) * 0.75f +
             context.GetTrait(ArtifactMaterialTraits.Suppression) * 0.35f) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Earth) * 0.35f);
        MountainSealFall.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 8f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectRadius, 2.2f * context.scales.Range),
            ArtifactAbilityValue.Number(DamageMultiplier, 0.85f * context.scales.Potency),
            ArtifactAbilityValue.Number(ForceStrength, 0.8f * context.scales.Potency),
            ArtifactAbilityValue.Number(StatusDuration, 0.55f * context.scales.Duration),
            ArtifactAbilityValue.Number(ImpactDelay, Mathf.Clamp(0.5f / context.scales.Precision, 0.18f, 0.5f)),
            ArtifactAbilityValue.Number(EffectDuration, 1.1f),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 10f, 3.2f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 4.5f)),
        ];
        MountainSealFall.ComposeInitialState = _ => [ArtifactAbilityValue.Boolean(ImpactDone, false)];
        MountainSealFall.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.MountainSealFall.Description"),
            ability.GetNumber(AttackRange),
            ability.GetNumber(EffectRadius),
            ability.GetNumber(DamageMultiplier),
            ability.GetNumber(StatusDuration),
            ability.GetNumber(Cooldown));
        MountainSealFall.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            tick_minimum_state = ArtifactControlState.Operating,
            tick_interval = 0.1f,
            tick_requires_activity = true,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            Resource = SkillCastResources.Wakan,
            OnTick = ApplyMountainSealImpact,
        });
        MountainSealFall.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.Point,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 8,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            ResolveEffectRadius = (_, ability) => ability.GetNumber(EffectRadius),
            CanPrepare = CanPrepareFreeBody,
            CanUse = CanDeployInRange,
            TryUse = DeployMountainSeal,
        });
    }

    private static bool DeployMountainSeal(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin _)
    {
        runtime.SetBoolean(ImpactDone, false);
        return DeployAtTarget(
            context,
            ability,
            ref runtime,
            target,
            ArtifactBodyAnchorRef.Appearance("face", "focus", ArtifactBodyAnchorKind.ForwardTip),
            180f);
    }

    private static void ApplyMountainSealImpact(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        float _)
    {
        if (runtime.GetBoolean(ImpactDone) ||
            World.world.getCurWorldTime() - runtime.activity_started_at < ability.GetNumber(ImpactDelay)) return;

        ArtifactDeployment deployment = context.artifact.GetComponent<ArtifactDeployment>();
        Vector3 position = deployment.origin;
        float strength = ability.GetNumber(DamageMultiplier);
        var contact = new ArtifactAreaContact(ability.GetNumber(StatusDuration),
            ability.GetNumber(ForceStrength) * context.effect_scale, damageFirst: true);
        var aim = new ActiveAbilityTarget(null, position);
        if (!ArtifactAreaConstruction.Start(context, MountainSealFall, aim, ActiveAbilityUseOrigin.Reaction,
                position, ability.GetNumber(EffectRadius), .42f, contact, "seal_crush", ElementComposition.Static.Earth,
                SkillContext.DefaultStrength * strength, matter: "earth", strike: .025f)) return;

        runtime.SetBoolean(ImpactDone, true);
        ArtifactManifestationTools.AlignDeploymentAnchor(context.artifact, deployment);
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Impact,
            position,
            intensity: strength);
    }

    /// <summary>同一件实际部署物在落印期间拥有真实离地高度，部署原点始终指向触地位置。</summary>
    /// <param name="artifact">原法印。</param><param name="deployment">实际部署资料。</param>
    internal static Vector3 ResolveDeploymentAnchor(Entity artifact, in ArtifactDeployment deployment)
    {
        if (!TryGetMountainSeal(artifact, deployment.ability_instance_id, out int index)) return deployment.origin;
        ref var runtime = ref artifact.GetComponent<ArtifactAbilityRuntime>().abilities[index];
        if (runtime.GetBoolean(ImpactDone)) return deployment.origin;
        var ability = artifact.GetComponent<ArtifactAbilitySet>().abilities[index];
        float progress = Mathf.Clamp01((float)(World.world.getCurWorldTime() - deployment.started_at) /
                                       Mathf.Max(.001f, ability.GetNumber(ImpactDelay)));
        float height = Mathf.Lerp(3.6f, .035f, progress * progress);
        return deployment.origin + new Vector3(0f, height, height);
    }

    /// <summary>只有正在执行山岳印落的原器物使用立体印面；随身时仍用原法宝显示。</summary>
    internal static bool IsMountainSealDeployment(Entity artifact) =>
        artifact.TryGetComponent(out ArtifactDeployment deployment) &&
        TryGetMountainSeal(artifact, deployment.ability_instance_id, out _);

    /// <summary>用原能力实例匹配配置，正式读档后可以继续计算同一印身位置。</summary>
    private static bool TryGetMountainSeal(Entity artifact, string instance, out int index)
    {
        if (artifact.TryGetComponent(out ArtifactAbilitySet set))
        {
            for (int i = 0; i < set.abilities.Length; i++)
            {
                if (set.abilities[i].instance_id != instance || set.abilities[i].ability_id != MountainSealFall.id) continue;
                index = i;
                return true;
            }
        }
        index = -1;
        return false;
    }

    private static void ConfigureSpellBanningEdict()
    {
        SpellBanningEdict.name_key = "Cultiway.ArtifactAbility.SpellBanningEdict";
        SpellBanningEdict.SetSemantics(
            ArtifactSemantics.Delivery.Deployment,
            ArtifactSemantics.Delivery.Field,
            ArtifactSemantics.Effect.Silence);
        SpellBanningEdict.exclusivity = ArtifactAbilityExclusivity.SpellBanningField;
        SpellBanningEdict.manifestation_cost = 1.35f;
        SpellBanningEdict.AddSynergies(ArtifactSemantics.Effect.Suppression, ArtifactSemantics.Theme.Soul);
        SpellBanningEdict.AddConflicts(ArtifactSemanticRules.SkillAmplification);
        SpellBanningEdict.minimum_score = 1f;
        SpellBanningEdict.use_profile = new ArtifactUseProfile { offensive = 0.6f, support = 0.55f };
        SpellBanningEdict.control_complexity = 0.5f;
        SpellBanningEdict.thread_cost = 1;
        SpellBanningEdict.parameter_schema =
        [
            NumberSpec(AttackRange),
            NumberSpec(EffectRadius),
            NumberSpec(EffectDuration),
            NumberSpec(StatusDuration),
            NumberSpec(DrainAmount),
            NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
            NumberSpec(MaintenanceCost),
        ];
        SpellBanningEdict.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Sealing) +
             context.GetTrait(ArtifactMaterialTraits.Suppression) * 0.7f +
             context.GetTrait(ArtifactMaterialTraits.Binding) * 0.25f) *
            (0.5f + context.GetTrait(ArtifactMaterialTraits.FieldProjection) * 0.45f);
        SpellBanningEdict.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 8f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectRadius, 2.6f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectDuration, 5f * context.scales.Duration),
            ArtifactAbilityValue.Number(StatusDuration, 1.1f * context.scales.Duration),
            ArtifactAbilityValue.Number(DrainAmount, 1.8f * context.scales.Potency),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 14f, 4f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 4f)),
            ArtifactAbilityValue.Number(MaintenanceCost, ScaledCost(context, 0.45f)),
        ];
        SpellBanningEdict.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.SpellBanningEdict.Description"),
            ability.GetNumber(AttackRange),
            ability.GetNumber(EffectRadius),
            ability.GetNumber(EffectDuration),
            ability.GetNumber(StatusDuration),
            ability.GetNumber(DrainAmount));
        SpellBanningEdict.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
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
            OnTick = ApplySpellBanningField,
            OnActivityEnded = EndPaidArtifactArea,
        });
        SpellBanningEdict.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.Point,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 7,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            ResolveEffectRadius = (_, ability) => ability.GetNumber(EffectRadius),
            CanPrepare = CanPrepareFreeBody,
            CanUse = CanDeployInRange,
            TryUse = DeploySpellBanningEdict,
        });
    }

    private static bool DeploySpellBanningEdict(
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
            ArtifactBodyAnchorRef.Appearance("face", "focus"));
    }

    private static void ApplySpellBanningField(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry _,
        float interval)
    {
        float duration = ability.GetNumber(StatusDuration), drain = ability.GetNumber(DrainAmount) * context.effect_scale;
        var carrier = ArtifactAbilityLifecycle.ResolveSkillContext(context);
        var contact = new ArtifactAreaContact((source, target) =>
        {
            using var scope = SkillCasterContextService.Enter(in carrier);
            CombatStatusEffects.ApplyStatus(target, StatusEffects.Silence, duration, source);
            CombatResourceEffects.DrainWakan(target, drain);
        });
        StartPaidArtifactArea(context, SpellBanningEdict, ability, interval, "edict_seal", contact,
            ElementComposition.Static.empty);
    }

    private static void ConfigureMeridianSealing()
    {
        MeridianSealing.name_key = "Cultiway.ArtifactAbility.MeridianSealing";
        MeridianSealing.SetSemantics(ArtifactSemantics.Effect.Control, ArtifactSemantics.Effect.Sealing);
        MeridianSealing.exclusivity = ArtifactAbilityExclusivity.SingleTargetSealing;
        MeridianSealing.manifestation_cost = 0.95f;
        MeridianSealing.AddSynergies(ArtifactSemantics.Effect.Debuff, ArtifactSemantics.Theme.Soul);
        MeridianSealing.minimum_score = 1f;
        MeridianSealing.use_profile = new ArtifactUseProfile { offensive = 0.75f, support = 0.25f };
        MeridianSealing.control_complexity = 0.34f;
        MeridianSealing.parameter_schema =
        [
            NumberSpec(AttackRange),
            NumberSpec(StatusDuration),
            NumberSpec(StatusStrength),
            NumberSpec(DrainAmount),
            NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
        ];
        MeridianSealing.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Sealing) +
             context.GetTrait(ArtifactMaterialTraits.Binding) * 0.75f +
             context.GetTrait(ArtifactMaterialTraits.Suppression) * 0.3f) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Neg) * 0.28f);
        MeridianSealing.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 7f * context.scales.Range),
            ArtifactAbilityValue.Number(StatusDuration, 2.8f * context.scales.Duration),
            ArtifactAbilityValue.Number(StatusStrength, Mathf.Clamp(0.16f * context.scales.Potency, 0.16f, 0.65f)),
            ArtifactAbilityValue.Number(DrainAmount, 8f * context.scales.Potency),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 8f, 2.4f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 2.5f)),
        ];
        MeridianSealing.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.MeridianSealing.Description"),
            ability.GetNumber(AttackRange),
            ability.GetNumber(StatusDuration),
            ability.GetNumber(StatusStrength),
            ability.GetNumber(DrainAmount),
            ability.GetNumber(Cooldown));
        MeridianSealing.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            Resource = SkillCastResources.Wakan,
        });
        MeridianSealing.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.Object,
            activation_mode = ActiveAbilityActivationMode.Instant,
            ai_weight = 6,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            CanUse = CanTargetHostileActor,
            TryUse = ApplyMeridianSealing,
        });
    }

    private static bool ApplyMeridianSealing(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry _,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        Actor victim = target.Object.a;
        float strength = ability.GetNumber(StatusStrength);
        float duration = ability.GetNumber(StatusDuration);
        float drain = ability.GetNumber(DrainAmount) * context.effect_scale;
        var carrier = ArtifactAbilityLifecycle.ResolveSkillContext(context);
        var contact = new ArtifactAreaContact((source, recipient) =>
        {
            using var scope = SkillCasterContextService.Enter(in carrier);
            CombatStatusEffects.ApplyStatus(recipient, StatusEffects.Slow, duration, S.multiplier_speed, -strength, source);
            CombatStatusEffects.ApplyStatus(recipient, StatusEffects.Weaken, duration, S.multiplier_damage, -strength, source);
            CombatResourceEffects.DrainWakan(recipient, drain);
        });
        float radius = Mathf.Clamp(SpatialActorPose.Height(victim) * .68f, .38f, 3f);
        return ArtifactAreaConstruction.Start(context, MeridianSealing, target, origin,
            victim.current_position, radius, .53f, contact, "meridian_seal", ElementComposition.Static.empty,
            strike: .045f, selectedTargetOnly: true);
    }

    private static void ConfigureSealAbilityVisuals()
    {
        MountainSealFall.Visualize(
            Theme(SkillVfxElements.Earth.AccentColor)
                .Signal(
                    ArtifactVisualChannels.Impact,
                    new ArtifactAudioVisualCue
                    {
                        anchor = ArtifactVisualAnchorKind.Point,
                        sound = "event:/SFX/WEAPONS/WeaponRockLand",
                    },
                    0.55f
                )
        );
    }
}
