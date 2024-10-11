using Cultiway.Core.SkillLib;

namespace Cultiway.Core.Libraries;

public class Manager
{
    public CultisysLibrary      CultisysLibrary      = new();
    public CustomMapModeLibrary CustomMapModeLibrary = new();
    public ElementRootLibrary   ElementRootLibrary   = new();
    public SkillEntityLibrary   SkillEntityLibrary   = new();
    public TrajectoryLibrary    TrajectoryLibrary    = new();

    public void Init()
    {
        AssetManager.instance.add(CultisysLibrary,      "cultisyses");
        AssetManager.instance.add(ElementRootLibrary,   "element_roots");
        AssetManager.instance.add(CustomMapModeLibrary, "custom_map_modes");
        AssetManager.instance.add(SkillEntityLibrary,   "skill_entities");
        AssetManager.instance.add(TrajectoryLibrary,    "trajectories");
    }

    public void PostInit()
    {
        CultisysLibrary.post_init();
        ElementRootLibrary.post_init();
        CustomMapModeLibrary.post_init();
        SkillEntityLibrary.post_init();
        TrajectoryLibrary.post_init();
    }
}