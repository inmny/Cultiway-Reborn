using Cultiway.Core.AIGCLib;
using Cultiway.Core.Semantics;

namespace Cultiway.Core.Libraries;

public class Manager
{
    public CultisysLibrary      CultisysLibrary      { get; } = new();
    public CustomMapModeLibrary CustomMapModeLibrary { get; } = new();
    public ElementRootLibrary   ElementRootLibrary   { get; } = new();
    public ItemShapeLibrary   ItemShapeLibrary   { get; } = new();
    public SpecialItemCategoryLibrary SpecialItemCategoryLibrary { get; } = new();
    public RandomEventLibrary RandomEventLibrary { get; } = new();
    public StatusEffectLibrary StatusEffectLibrary { get; } = new();
    public ForceTypeLibrary ForceTypeLibrary { get; } = new();
    public SectBannerLibrary SectBannerLibrary { get; } = new();
    public GeoRegionBannerLibrary GeoRegionBannerLibrary { get; } = new();
    public ImageTemplateLibrary ImageTemplateLibrary { get; } = new();
    public OperationLibrary OperationLibrary { get; } = new();
    public PortalLibrary PortalLibrary { get; } = new();
    public GeoRegionLibrary GeoRegionLibrary { get; } = new();
    public MasterApprenticeTypeLibrary MasterApprenticeTypeLibrary { get; } = new();
    public SectTraitGroupLibrary SectTraitGroupLibrary { get; } = new();
    public SectTraitLibrary SectTraitLibrary { get; } = new();
    public SectPermissionLibrary SectPermissionLibrary { get; } = new();
    public SectRoleLibrary SectRoleLibrary { get; } = new();
    public SectAffairLibrary SectAffairLibrary { get; } = new();
    public SectBuildOrderLibrary SectBuildOrderLibrary { get; } = new();
    public SectJobLibrary SectJobLibrary { get; } = new();
    public SkillNameAtomLibrary SkillNameAtomLibrary { get; } = new();
    public SemanticFacetLibrary SemanticFacetLibrary { get; } = new();
    public SemanticLibrary SemanticLibrary { get; } = new();

    public void Init()
    {
        AssetManager._instance.add(CultisysLibrary,      "cultisyses");
        AssetManager._instance.add(ElementRootLibrary,   "element_roots");
        AssetManager._instance.add(CustomMapModeLibrary, "custom_map_modes");
        AssetManager._instance.add(ItemShapeLibrary,   "item_shapes");
        AssetManager._instance.add(SpecialItemCategoryLibrary, "special_item_categories");
        AssetManager._instance.add(RandomEventLibrary, "random_events");
        AssetManager._instance.add(StatusEffectLibrary, "status_effects");
        AssetManager._instance.add(ForceTypeLibrary, "force_types");
        AssetManager._instance.add(SectBannerLibrary, "sect_banners");
        AssetManager._instance.add(GeoRegionBannerLibrary, "geo_region_banners");
        AssetManager._instance.add(ImageTemplateLibrary, "image_templates");
        AssetManager._instance.add(OperationLibrary, "operations");
        AssetManager._instance.add(PortalLibrary, "portals");
        AssetManager._instance.add(GeoRegionLibrary, "geo_regions");
        AssetManager._instance.add(MasterApprenticeTypeLibrary, "master_apprentice_types");
        AssetManager._instance.add(SectTraitGroupLibrary, "sect_trait_groups");
        AssetManager._instance.add(SectTraitLibrary, "sect_traits");
        AssetManager._instance.add(SectPermissionLibrary, "sect_permissions");
        AssetManager._instance.add(SectRoleLibrary, "sect_roles");
        AssetManager._instance.add(SectAffairLibrary, "sect_affairs");
        AssetManager._instance.add(SectBuildOrderLibrary, "sect_build_orders");
        AssetManager._instance.add(SectJobLibrary, "sect_jobs");
        AssetManager._instance.add(SkillNameAtomLibrary, "skill_name_atoms");
        AssetManager._instance.add(SemanticFacetLibrary, "semantic_facets");
        AssetManager._instance.add(SemanticLibrary, "semantics");
    }

    public void LinkAssets()
    {
        CultisysLibrary.linkAssets();
        ElementRootLibrary.linkAssets();
        CustomMapModeLibrary.linkAssets();
        ItemShapeLibrary.linkAssets();
        SpecialItemCategoryLibrary.linkAssets();
        RandomEventLibrary.linkAssets();
        StatusEffectLibrary.linkAssets();
        ForceTypeLibrary.linkAssets();
        SectBannerLibrary.linkAssets();
        GeoRegionBannerLibrary.linkAssets();
        ImageTemplateLibrary.linkAssets();
        OperationLibrary.linkAssets();
        PortalLibrary.linkAssets();
        GeoRegionLibrary.linkAssets();
        MasterApprenticeTypeLibrary.linkAssets();
        SectTraitGroupLibrary.linkAssets();
        SectTraitLibrary.linkAssets();
        SectPermissionLibrary.linkAssets();
        SectRoleLibrary.linkAssets();
        SectAffairLibrary.linkAssets();
        SectBuildOrderLibrary.linkAssets();
        SectJobLibrary.linkAssets();
        SkillNameAtomLibrary.linkAssets();
        SemanticFacetLibrary.linkAssets();
        SemanticLibrary.linkAssets();
    }
    public void PostInit()
    {
        CultisysLibrary.post_init();
        ElementRootLibrary.post_init();
        CustomMapModeLibrary.post_init();
        ItemShapeLibrary.post_init();
        SpecialItemCategoryLibrary.post_init();
        RandomEventLibrary.post_init();
        StatusEffectLibrary.post_init();
        ForceTypeLibrary.post_init();
        SectBannerLibrary.post_init();
        GeoRegionBannerLibrary.post_init();
        ImageTemplateLibrary.post_init();
        OperationLibrary.post_init();
        PortalLibrary.post_init();
        GeoRegionLibrary.post_init();
        MasterApprenticeTypeLibrary.post_init();
        SectTraitGroupLibrary.post_init();
        SectTraitLibrary.post_init();
        SectPermissionLibrary.post_init();
        SectRoleLibrary.post_init();
        SectAffairLibrary.post_init();
        SectBuildOrderLibrary.post_init();
        SectJobLibrary.post_init();
        SkillNameAtomLibrary.post_init();
        SemanticFacetLibrary.post_init();
        SemanticLibrary.post_init();
    }
}
