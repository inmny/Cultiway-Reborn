namespace Cultiway.Content.Libraries;

public class Manager
{
    public static JindanLibrary      JindanLibrary      { get; } = new();
    public static JindanGroupLibrary JindanGroupLibrary { get; } = new();
    public static ElixirLibrary ElixirLibrary { get; } = new();
    public static ElixirEffectAtomLibrary ElixirEffectAtomLibrary { get; } = new();
    public static ArtifactAtomLibrary ArtifactAtomLibrary { get; } = new();
    public static ArtifactAbilityLibrary ArtifactAbilityLibrary { get; } = new();
    public static ArtifactBlueprintExtensionLibrary ArtifactBlueprintExtensionLibrary { get; } = new();
    public static ArtifactPresentationLibrary ArtifactPresentationLibrary { get; } = new();
    public static CultibookLibrary CultibookLibrary { get; } = new();
    public static BloodlineLibrary BloodlineLibrary { get; } = new();
    public static CultibookRuleProfileLibrary CultibookRuleProfileLibrary { get; } = new();
    public static YuanyingLibrary YuanyingLibrary { get; } = new();
    public static CultivateMethodLibrary CultivateMethodLibrary { get; } = new();

    internal static void Init()
    {
        AssetManager._instance.add(JindanGroupLibrary, "jindan_groups");
        AssetManager._instance.add(JindanLibrary,      "jindan_types");
        AssetManager._instance.add(ElixirLibrary, "elixirs");
        AssetManager._instance.add(ElixirEffectAtomLibrary, "elixir_effect_atoms");
        AssetManager._instance.add(ArtifactAtomLibrary, "artifact_atoms");
        AssetManager._instance.add(ArtifactAbilityLibrary, "artifact_abilities");
        AssetManager._instance.add(ArtifactBlueprintExtensionLibrary, "artifact_blueprint_extensions");
        AssetManager._instance.add(ArtifactPresentationLibrary, "artifact_presentations");
        AssetManager._instance.add(CultibookLibrary, "cultibooks");
        AssetManager._instance.add(BloodlineLibrary, "bloodlines");
        AssetManager._instance.add(CultibookRuleProfileLibrary, "cultibook_rule_profiles");
        AssetManager._instance.add(YuanyingLibrary, "yuanying_types");
        AssetManager._instance.add(CultivateMethodLibrary, "cultivate_methods");
        
        PostInit();
    }

    private static void PostInit()
    {
        JindanLibrary.post_init();
        JindanGroupLibrary.post_init();
        ElixirLibrary.post_init();
        ElixirEffectAtomLibrary.post_init();
        ArtifactAtomLibrary.post_init();
        ArtifactAbilityLibrary.post_init();
        ArtifactBlueprintExtensionLibrary.post_init();
        ArtifactPresentationLibrary.post_init();
        CultibookLibrary.post_init();
        CultibookRuleProfileLibrary.post_init();
        YuanyingLibrary.post_init();
        CultivateMethodLibrary.post_init();
    }
}
