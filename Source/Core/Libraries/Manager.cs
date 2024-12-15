namespace Cultiway.Core.Libraries;

public class Manager
{
    public CultisysLibrary      CultisysLibrary      { get; } = new();
    public CustomMapModeLibrary CustomMapModeLibrary { get; } = new();
    public ElementRootLibrary   ElementRootLibrary   { get; } = new();
    public MaterialShapeLibrary ItemShapeLibrary { get; } = new();

    public void Init()
    {
        AssetManager.instance.add(CultisysLibrary,      "cultisyses");
        AssetManager.instance.add(ElementRootLibrary,   "element_roots");
        AssetManager.instance.add(CustomMapModeLibrary, "custom_map_modes");
        AssetManager.instance.add(ItemShapeLibrary, "material_shapes");
    }

    public void PostInit()
    {
        CultisysLibrary.post_init();
        ElementRootLibrary.post_init();
        CustomMapModeLibrary.post_init();
        ItemShapeLibrary.post_init();
    }
}