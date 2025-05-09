namespace Cultiway.Core.Libraries;

public class Manager
{
    public CultisysLibrary      CultisysLibrary      { get; } = new();
    public CustomMapModeLibrary CustomMapModeLibrary { get; } = new();
    public ElementRootLibrary   ElementRootLibrary   { get; } = new();
    public ItemShapeLibrary   ItemShapeLibrary   { get; } = new();
    public RandomEventLibrary RandomEventLibrary { get; } = new();
    public StatusEffectLibrary StatusEffectLibrary { get; } = new();
    public WrappedSkillLibrary WrappedSkillLibrary { get; } = new();
    public ForceTypeLibrary ForceTypeLibrary { get; } = new();

    public void Init()
    {
        AssetManager._instance.add(CultisysLibrary,      "cultisyses");
        AssetManager._instance.add(ElementRootLibrary,   "element_roots");
        AssetManager._instance.add(CustomMapModeLibrary, "custom_map_modes");
        AssetManager._instance.add(ItemShapeLibrary,   "item_shapes");
        AssetManager._instance.add(RandomEventLibrary, "random_events");
        AssetManager._instance.add(StatusEffectLibrary, "status_effects");
        AssetManager._instance.add(WrappedSkillLibrary, "wrapped_skills");
        AssetManager._instance.add(ForceTypeLibrary, "force_types");
    }

    public void PostInit()
    {
        CultisysLibrary.post_init();
        ElementRootLibrary.post_init();
        CustomMapModeLibrary.post_init();
        ItemShapeLibrary.post_init();
        RandomEventLibrary.post_init();
        StatusEffectLibrary.post_init();
        WrappedSkillLibrary.post_init();
        ForceTypeLibrary.post_init();
    }
}