using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using strings;
using UnityEngine;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    private const string AccuracyBonus = "accuracy_bonus";
    private const string CriticalBonus = "critical_bonus";
    private const string DamageRangeBonus = "damage_range_bonus";
    private const string HealPerTick = "heal_per_tick";
    private const string MaintenanceCost = "maintenance_cost";
    private const string WakanCapacity = "wakan_capacity";
    private const string WakanRegen = "wakan_regen";

    /// <summary>常驻洞察被动；提高持有者的命中、暴击概率和伤害波动上限。</summary>
    public static ArtifactAbilityAsset MirrorInsight { get; private set; }
    /// <summary>持续恢复被动；法器运转时每秒消耗灵气，为受伤持有者恢复生命。</summary>
    public static ArtifactAbilityAsset VitalityRenewal { get; private set; }
    /// <summary>灵力储备被动；法器运转时提高持有者的灵气上限和灵气回复。</summary>
    public static ArtifactAbilityAsset SpiritReservoir { get; private set; }

    private static void ConfigureMirrorInsight()
    {
        MirrorInsight.name_key = "Cultiway.ArtifactAbility.MirrorInsight";
        MirrorInsight.tags = ["passive", "support", "perception"];
        MirrorInsight.exclusive_group = "perception_support";
        MirrorInsight.minimum_score = 1f;
        MirrorInsight.use_profile = new ArtifactUseProfile { offensive = 0.2f, support = 0.8f };
        MirrorInsight.control_complexity = 0.12f;
        MirrorInsight.parameter_schema =
        [
            NumberSpec(AccuracyBonus),
            NumberSpec(CriticalBonus),
            NumberSpec(DamageRangeBonus),
        ];
        MirrorInsight.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.Insight) *
            (1f + context.GetTrait(ArtifactMaterialTraits.Perception) * 0.16f +
             context.GetTrait(ArtifactMaterialTraits.Reflection) * 0.05f);
        MirrorInsight.ComposeParameters = context =>
        {
            int quality = Quality(context);
            float perception = Mathf.Min(7f, context.GetTrait(ArtifactMaterialTraits.Perception));
            return
            [
                ArtifactAbilityValue.Number(AccuracyBonus, 2f + quality * 0.18f + perception * 1.25f),
                ArtifactAbilityValue.Number(CriticalBonus, 0.01f + quality * 0.0012f + perception * 0.006f),
                ArtifactAbilityValue.Number(DamageRangeBonus, 0.015f + quality * 0.001f + perception * 0.008f),
            ];
        };
        MirrorInsight.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.MirrorInsight.Description"),
            ability.GetNumber(AccuracyBonus),
            ability.GetNumber(CriticalBonus),
            ability.GetNumber(DamageRangeBonus));
        MirrorInsight.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            stats_minimum_state = ArtifactControlState.Operating,
            ContributeStats = (_, ability, _, stats) =>
            {
                stats[S.accuracy] += ability.GetNumber(AccuracyBonus);
                stats[S.critical_chance] += ability.GetNumber(CriticalBonus);
                stats[S.damage_range] += ability.GetNumber(DamageRangeBonus);
            },
        });
    }

    private static void ConfigureVitalityRenewal()
    {
        VitalityRenewal.name_key = "Cultiway.ArtifactAbility.VitalityRenewal";
        VitalityRenewal.tags = ["passive", "support", "recovery"];
        VitalityRenewal.exclusive_group = "continuous_recovery";
        VitalityRenewal.minimum_score = 1f;
        VitalityRenewal.use_profile = new ArtifactUseProfile { defensive = 0.35f, support = 0.75f };
        VitalityRenewal.control_complexity = 0.16f;
        VitalityRenewal.parameter_schema = [NumberSpec(HealPerTick), NumberSpec(MaintenanceCost)];
        VitalityRenewal.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.Renewal) *
            (1f + context.GetTrait(ArtifactMaterialTraits.Vitality) * 0.12f +
             context.GetTrait(ArtifactMaterialTraits.Wood) * 0.04f);
        VitalityRenewal.ComposeParameters = context =>
        {
            int quality = Quality(context);
            float vitality = Mathf.Min(8f, context.GetTrait(ArtifactMaterialTraits.Vitality));
            float renewal = Mathf.Min(8f, context.GetTrait(ArtifactMaterialTraits.Renewal));
            return
            [
                ArtifactAbilityValue.Number(HealPerTick, 1f + quality * 0.18f + vitality * 0.7f + renewal * 0.35f),
                ArtifactAbilityValue.Number(MaintenanceCost, 0.15f + quality * 0.015f + renewal * 0.04f),
            ];
        };
        VitalityRenewal.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.VitalityRenewal.Description"),
            ability.GetNumber(HealPerTick),
            ability.GetNumber(MaintenanceCost));
        VitalityRenewal.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            tick_minimum_state = ArtifactControlState.Operating,
            tick_interval = 1f,
            ResolveMaintenanceCost = (_, ability) => ability.GetNumber(MaintenanceCost),
            Resource = UseWakan,
            CanTick = CanRestoreControllerHealth,
            OnTick = RestoreControllerHealth,
        });
    }

    private static void ConfigureSpiritReservoir()
    {
        SpiritReservoir.name_key = "Cultiway.ArtifactAbility.SpiritReservoir";
        SpiritReservoir.tags = ["passive", "support", "resource"];
        SpiritReservoir.exclusive_group = "spirit_reservoir";
        SpiritReservoir.minimum_score = 1f;
        SpiritReservoir.use_profile = new ArtifactUseProfile { support = 0.35f, cultivate = 0.9f };
        SpiritReservoir.control_complexity = 0.1f;
        SpiritReservoir.parameter_schema = [NumberSpec(WakanCapacity), NumberSpec(WakanRegen)];
        SpiritReservoir.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.SpiritReservoir) *
            (1f + context.GetTrait(ArtifactMaterialTraits.Capacity) * 0.12f +
             context.GetTrait(ArtifactMaterialTraits.Spirituality) * 0.08f);
        SpiritReservoir.ComposeParameters = context =>
        {
            int quality = Quality(context);
            float capacity = Mathf.Min(8f, context.GetTrait(ArtifactMaterialTraits.Capacity));
            float spirituality = Mathf.Min(8f, context.GetTrait(ArtifactMaterialTraits.Spirituality));
            return
            [
                ArtifactAbilityValue.Number(WakanCapacity, 8f + quality * 1.5f + capacity * 6f + spirituality * 2f),
                ArtifactAbilityValue.Number(WakanRegen, 0.08f + quality * 0.018f + spirituality * 0.12f),
            ];
        };
        SpiritReservoir.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.SpiritReservoir.Description"),
            ability.GetNumber(WakanCapacity),
            ability.GetNumber(WakanRegen));
        SpiritReservoir.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            stats_minimum_state = ArtifactControlState.Operating,
            ContributeStats = (_, ability, _, stats) =>
            {
                stats[BaseStatses.MaxWakan.id] += ability.GetNumber(WakanCapacity);
                stats[BaseStatses.WakanRegen.id] += ability.GetNumber(WakanRegen);
            },
        });
    }

    private static void RestoreControllerHealth(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry _,
        float __)
    {
        Actor actor = context.controller.GetComponent<ActorBinder>().Actor;
        actor.restoreHealth(Mathf.Max(1, Mathf.RoundToInt(ability.GetNumber(HealPerTick))));
    }

    private static bool CanRestoreControllerHealth(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance _,
        ArtifactAbilityRuntimeEntry __)
    {
        Actor actor = context.controller.GetComponent<ActorBinder>().Actor;
        return actor.data.health < actor.stats[S.health];
    }

    private static bool UseWakan(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance _,
        float amount,
        bool consume)
    {
        ActorExtend controller = context.controller.GetComponent<ActorBinder>().AE;
        if (!controller.HasCultisys<Xian>()) return false;
        ref Xian xian = ref controller.GetCultisys<Xian>();
        if (xian.wakan < amount) return false;
        if (consume) xian.wakan -= amount;
        return true;
    }
}
