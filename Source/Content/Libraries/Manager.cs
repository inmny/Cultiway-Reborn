using Cultiway.Content.CreatureCompositions.Libraries;

namespace Cultiway.Content.Libraries;

public class Manager
{
    public static CoreFormationAtomLibrary CoreFormationAtomLibrary { get; } = new();
    public static ElixirLibrary ElixirLibrary { get; } = new();
    public static ElixirEffectAtomLibrary ElixirEffectAtomLibrary { get; } = new();
    public static ArtifactAtomLibrary ArtifactAtomLibrary { get; } = new();
    public static ArtifactAbilityLibrary ArtifactAbilityLibrary { get; } = new();
    public static ArtifactBlueprintExtensionLibrary ArtifactBlueprintExtensionLibrary { get; } = new();
    public static ArtifactPresentationLibrary ArtifactPresentationLibrary { get; } = new();
    public static CultibookLibrary CultibookLibrary { get; } = new();
    public static BloodlineLibrary BloodlineLibrary { get; } = new();
    public static CultibookRuleProfileLibrary CultibookRuleProfileLibrary { get; } = new();
    public static CultivateMethodLibrary CultivateMethodLibrary { get; } = new();
    public static CultivationResourceLibrary CultivationResourceLibrary { get; } = new();
    public static SectNameAtomLibrary SectNameAtomLibrary { get; } = new();
    public static KnightStyleLibrary KnightStyleLibrary { get; } = new();
    public static KnightTechniqueLibrary KnightTechniqueLibrary { get; } = new();
    public static CreatureBodyPlanLibrary CreatureBodyPlanLibrary { get; } = new();
    public static CreatureBodySlotLibrary CreatureBodySlotLibrary { get; } = new();
    public static CreatureMorphLibrary CreatureMorphLibrary { get; } = new();
    public static CreatureOrganLibrary CreatureOrganLibrary { get; } = new();
    public static CreatureOrganRankLibrary CreatureOrganRankLibrary { get; } = new();
    public static CreatureVisualRigLibrary CreatureVisualRigLibrary { get; } = new();
    public static CreatureVisualLayerLibrary CreatureVisualLayerLibrary { get; } = new();

    /// <summary>将内容层资产库注册到 WorldBox 资产管理器，并完成统一后初始化。</summary>
    internal static void Init()
    {
        AssetManager._instance.add(CoreFormationAtomLibrary, "core_formation_atoms");
        AssetManager._instance.add(ElixirLibrary, "elixirs");
        AssetManager._instance.add(ElixirEffectAtomLibrary, "elixir_effect_atoms");
        AssetManager._instance.add(ArtifactAtomLibrary, "artifact_atoms");
        AssetManager._instance.add(ArtifactAbilityLibrary, "artifact_abilities");
        AssetManager._instance.add(ArtifactBlueprintExtensionLibrary, "artifact_blueprint_extensions");
        AssetManager._instance.add(ArtifactPresentationLibrary, "artifact_presentations");
        AssetManager._instance.add(CultibookLibrary, "cultibooks");
        AssetManager._instance.add(BloodlineLibrary, "bloodlines");
        AssetManager._instance.add(CultibookRuleProfileLibrary, "cultibook_rule_profiles");
        AssetManager._instance.add(CultivateMethodLibrary, "cultivate_methods");
        AssetManager._instance.add(CultivationResourceLibrary, "cultivation_resources");
        AssetManager._instance.add(SectNameAtomLibrary, "sect_name_atoms");
        AssetManager._instance.add(KnightStyleLibrary, "knight_styles");
        AssetManager._instance.add(KnightTechniqueLibrary, "knight_techniques");
        AssetManager._instance.add(CreatureBodyPlanLibrary, "creature_body_plans");
        AssetManager._instance.add(CreatureBodySlotLibrary, "creature_body_slots");
        AssetManager._instance.add(CreatureMorphLibrary, "creature_morphs");
        AssetManager._instance.add(CreatureOrganLibrary, "creature_organs");
        AssetManager._instance.add(CreatureOrganRankLibrary, "creature_organ_ranks");
        AssetManager._instance.add(CreatureVisualRigLibrary, "creature_visual_rigs");
        AssetManager._instance.add(CreatureVisualLayerLibrary, "creature_visual_layers");

        PostInit();
    }

    /// <summary>按依赖顺序调用各内容资产库的后初始化阶段。</summary>
    private static void PostInit()
    {
        CoreFormationAtomLibrary.post_init();
        ElixirLibrary.post_init();
        ElixirEffectAtomLibrary.post_init();
        ArtifactAtomLibrary.post_init();
        ArtifactAbilityLibrary.post_init();
        ArtifactBlueprintExtensionLibrary.post_init();
        ArtifactPresentationLibrary.post_init();
        CultibookLibrary.post_init();
        CultibookRuleProfileLibrary.post_init();
        CultivateMethodLibrary.post_init();
        CultivationResourceLibrary.post_init();
        SectNameAtomLibrary.post_init();
        KnightStyleLibrary.post_init();
        KnightTechniqueLibrary.post_init();
        CreatureBodyPlanLibrary.post_init();
        CreatureBodySlotLibrary.post_init();
        CreatureMorphLibrary.post_init();
        CreatureOrganLibrary.post_init();
        CreatureOrganRankLibrary.post_init();
        CreatureVisualRigLibrary.post_init();
        CreatureVisualLayerLibrary.post_init();
    }
}
