using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Core.Semantics;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 法器能力之间可复用的组合语义条件。组合概念不会伪装成新的字符串标签。
/// </summary>
public static class ArtifactSemanticRules
{
    public static readonly SemanticQueryExpression PurificationField =
        SemanticQueryExpression.All(ArtifactSemantics.Effect.Purification, ArtifactSemantics.Delivery.Field);
    public static readonly SemanticQueryExpression Purification =
        SemanticQueryExpression.Has(ArtifactSemantics.Effect.Purification);
    public static readonly SemanticQueryExpression CurseField =
        SemanticQueryExpression.All(SkillSemantics.Effect.Curse, ArtifactSemantics.Delivery.Field);
    public static readonly SemanticQueryExpression ColdField =
        SemanticQueryExpression.All(SkillSemantics.Element.Ice, ArtifactSemantics.Delivery.Field);
    public static readonly SemanticQueryExpression ImmovableField =
        SemanticQueryExpression.All(CultivationSemantics.Material.Immoveable, ArtifactSemantics.Delivery.Field);
    public static readonly SemanticQueryExpression WaterField =
        SemanticQueryExpression.All(SkillSemantics.Element.Water, ArtifactSemantics.Delivery.Field);
    public static readonly SemanticQueryExpression SilenceAura =
        SemanticQueryExpression.All(ArtifactSemantics.Effect.Silence, ArtifactSemantics.Delivery.Field);
    public static readonly SemanticQueryExpression DamageConversion =
        SemanticQueryExpression.Has(ArtifactSemantics.Effect.DamageConversion);
    public static readonly SemanticQueryExpression SoulPurification =
        SemanticQueryExpression.All(ArtifactSemantics.Theme.Soul, ArtifactSemantics.Effect.Purification);
    public static readonly SemanticQueryExpression Concealment =
        SemanticQueryExpression.Has(ArtifactSemantics.Effect.Concealment);
    public static readonly SemanticQueryExpression Absorption =
        SemanticQueryExpression.Has(ArtifactSemantics.Effect.Absorption);
    public static readonly SemanticQueryExpression BodyDeployment =
        SemanticQueryExpression.All(CultivationSemantics.Form.Body, ArtifactSemantics.Delivery.Deployment);
    public static readonly SemanticQueryExpression SingleHeavyStrike =
        SemanticQueryExpression.All(SkillSemantics.Form.Single, ArtifactSemantics.Effect.Impact);
    public static readonly SemanticQueryExpression Revealing =
        SemanticQueryExpression.Has(CultivationSemantics.Effect.Revealing);
    public static readonly SemanticQueryExpression RigidGuard =
        SemanticQueryExpression.Has(CultivationSemantics.Effect.RigidGuard);
    public static readonly SemanticQueryExpression Lightweight =
        SemanticQueryExpression.Has(CultivationSemantics.Material.Lightweight);
    public static readonly SemanticQueryExpression SkillAmplification =
        SemanticQueryExpression.All(ArtifactSemantics.Effect.Amplification, ArtifactSemantics.Form.Spell);
    public static readonly SemanticQueryExpression SingularProjection =
        SemanticQueryExpression.All(ArtifactSemantics.Delivery.Projection, SkillSemantics.Form.Single);
    public static readonly SemanticQueryExpression Nonretaliation =
        SemanticQueryExpression.Has(CultivationSemantics.Effect.Nonretaliation);
    public static readonly SemanticQueryExpression MobilityField =
        SemanticQueryExpression.All(ArtifactSemantics.Effect.Mobility, ArtifactSemantics.Delivery.Field);
    public static readonly SemanticQueryExpression ImmobileCore =
        SemanticQueryExpression.All(CultivationSemantics.Material.Immoveable, ArtifactSemantics.Material.Stability);
    public static readonly SemanticQueryExpression Brittle =
        SemanticQueryExpression.Has(CultivationSemantics.Material.Brittle);
}

/// <summary>
/// 法器能力的技术互斥组。它不是世界语义，不能参与角色画像或跨系统检索。
/// </summary>
public static class ArtifactAbilityExclusivity
{
    public static readonly ArtifactAbilityExclusivityKey SoulField = new("soul_field");
    public static readonly ArtifactAbilityExclusivityKey CommandField = new("command_field");
    public static readonly ArtifactAbilityExclusivityKey SpiritHost = new("spirit_host");
    public static readonly ArtifactAbilityExclusivityKey SoundBurst = new("sound_burst");
    public static readonly ArtifactAbilityExclusivityKey PurificationBurst = new("purification_burst");
    public static readonly ArtifactAbilityExclusivityKey ActiveBarrier = new("active_barrier");
    public static readonly ArtifactAbilityExclusivityKey SuppressionField = new("suppression_field");
    public static readonly ArtifactAbilityExclusivityKey SpatialAttack = new("spatial_attack");
    public static readonly ArtifactAbilityExclusivityKey AlchemyAssist = new("alchemy_assist");
    public static readonly ArtifactAbilityExclusivityKey RefiningFlameField = new("refining_flame_field");
    public static readonly ArtifactAbilityExclusivityKey DevouringField = new("devouring_field");
    public static readonly ArtifactAbilityExclusivityKey GeneralRefinementAssist = new("general_refinement_assist");
    public static readonly ArtifactAbilityExclusivityKey WindCone = new("wind_cone");
    public static readonly ArtifactAbilityExclusivityKey FireCone = new("fire_cone");
    public static readonly ArtifactAbilityExclusivityKey CleansingCone = new("cleansing_cone");
    public static readonly ArtifactAbilityExclusivityKey WakanBuffer = new("wakan_buffer");
    public static readonly ArtifactAbilityExclusivityKey ElementalCone = new("elemental_cone");
    public static readonly ArtifactAbilityExclusivityKey SpellReflection = new("spell_reflection");
    public static readonly ArtifactAbilityExclusivityKey TruthRevealingField = new("truth_revealing_field");
    public static readonly ArtifactAbilityExclusivityKey SoulStasis = new("soul_stasis");
    public static readonly ArtifactAbilityExclusivityKey PerceptionSupport = new("perception_support");
    public static readonly ArtifactAbilityExclusivityKey ContinuousRecovery = new("continuous_recovery");
    public static readonly ArtifactAbilityExclusivityKey SpiritReservoir = new("spirit_reservoir");
    public static readonly ArtifactAbilityExclusivityKey RechargingShield = new("recharging_shield");
    public static readonly ArtifactAbilityExclusivityKey ElementalBarrage = new("elemental_barrage");
    public static readonly ArtifactAbilityExclusivityKey SkillCastResonance = new("skill_cast_resonance");
    public static readonly ArtifactAbilityExclusivityKey GuardianReaction = new("guardian_reaction");
    public static readonly ArtifactAbilityExclusivityKey ConcealmentState = new("concealment_state");
    public static readonly ArtifactAbilityExclusivityKey DamageDiversion = new("damage_diversion");
    public static readonly ArtifactAbilityExclusivityKey HeavyImpact = new("heavy_impact");
    public static readonly ArtifactAbilityExclusivityKey SpellBanningField = new("spell_banning_field");
    public static readonly ArtifactAbilityExclusivityKey SingleTargetSealing = new("single_target_sealing");
    public static readonly ArtifactAbilityExclusivityKey SectGuardian = new("sect_guardian");
    public static readonly ArtifactAbilityExclusivityKey ArtifactSpirit = new("artifact_spirit");
    public static readonly ArtifactAbilityExclusivityKey ProjectionArray = new("projection_array");
    public static readonly ArtifactAbilityExclusivityKey BladeCounter = new("blade_counter");
    public static readonly ArtifactAbilityExclusivityKey ArmorBreakOnHit = new("armor_break_on_hit");
    public static readonly ArtifactAbilityExclusivityKey ImprisonmentField = new("imprisonment_field");
    public static readonly ArtifactAbilityExclusivityKey LayeredWardField = new("layered_ward_field");
    public static readonly ArtifactAbilityExclusivityKey RemoteProjection = new("remote_projection");
    public static readonly ArtifactAbilityExclusivityKey ArtifactVehicle = new("artifact_vehicle");
}
