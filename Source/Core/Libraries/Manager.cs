namespace Cultiway.Core.Libraries;

public class Manager
{
    public CultisysLibrary      CultisysLibrary      = new();
    public CustomMapModeLibrary CustomMapModeLibrary = new();
    public ElementRootLibrary   ElementRootLibrary   = new();

    public void Init()
    {
        AssetManager.instance.add(CultisysLibrary,      "cultisyses");
        AssetManager.instance.add(ElementRootLibrary,   "element_roots");
        AssetManager.instance.add(CustomMapModeLibrary, "custom_map_modes");
    }

    public void PostInit()
    {
        CultisysLibrary.post_init();
        ElementRootLibrary.post_init();
        CustomMapModeLibrary.post_init();
    }
}