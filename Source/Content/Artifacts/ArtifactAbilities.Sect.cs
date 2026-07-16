using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using NeoModLoader.General;
using strings;
using UnityEngine;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    private const string SectHealthRatio = "sect_health_ratio";
    private const string SectArmorBonus = "sect_armor_bonus";

    /// <summary>宗门供奉被动；法器被宗门供奉时，按优先级为成员增加生命比例和护甲。</summary>
    public static ArtifactAbilityAsset SectGuardianTreasure { get; private set; }

    private static void ConfigureSectAbilities()
    {
        ConfigureSectGuardianTreasure();
    }

    private static void ConfigureSectGuardianTreasure()
    {
        SectGuardianTreasure.name_key = "Cultiway.ArtifactAbility.SectGuardianTreasure";
        SectGuardianTreasure.tags = ["passive", "support", "sect", "field"];
        SectGuardianTreasure.exclusive_group = "sect_guardian";
        SectGuardianTreasure.manifestation_cost = 1.15f;
        SectGuardianTreasure.synergy_tags = ["ward", "field", "sustain"];
        SectGuardianTreasure.minimum_score = 1f;
        SectGuardianTreasure.use_profile = new ArtifactUseProfile { defensive = 0.35f, support = 0.8f };
        SectGuardianTreasure.control_complexity = 0.28f;
        SectGuardianTreasure.parameter_schema = [NumberSpec(SectHealthRatio), NumberSpec(SectArmorBonus)];
        SectGuardianTreasure.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.SectGuardian) *
            (0.62f + context.GetTrait(ArtifactMaterialTraits.Ward) * 0.36f +
             context.GetTrait(ArtifactMaterialTraits.FieldProjection) * 0.24f +
             context.GetTrait(ArtifactMaterialTraits.Sustain) * 0.16f);
        SectGuardianTreasure.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(
                SectHealthRatio,
                0.012f + context.scales.Potency * 0.006f +
                Mathf.Min(8f, context.GetTrait(ArtifactMaterialTraits.Ward)) * 0.003f),
            ArtifactAbilityValue.Number(
                SectArmorBonus,
                0.8f + context.scales.Potency * 0.65f +
                Mathf.Min(8f, context.GetTrait(ArtifactMaterialTraits.Hardness)) * 0.32f),
        ];
        SectGuardianTreasure.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.SectGuardianTreasure.Description"),
            ability.GetNumber(SectHealthRatio),
            ability.GetNumber(SectArmorBonus));
        SectGuardianTreasure.SupportSect(new ArtifactSectAbilityProfile
        {
            stacking_group = "sect.guardian",
            max_active = 3,
            ResolvePriority = ability => ability.GetNumber(SectHealthRatio) * 100f +
                                         ability.GetNumber(SectArmorBonus),
            ContributeMemberStats = (context, ability, stats) =>
            {
                stats[S.health] += context.baseline_stats[S.health] * ability.GetNumber(SectHealthRatio);
                stats[S.armor] += ability.GetNumber(SectArmorBonus);
            },
        });
    }
}
