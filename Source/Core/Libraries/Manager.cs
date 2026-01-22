namespace Cultiway.Core.Libraries;

public class Manager
{
    public CultisysLibrary      CultisysLibrary      { get; } = new();
    public CustomMapModeLibrary CustomMapModeLibrary { get; } = new();
    public ElementRootLibrary   ElementRootLibrary   { get; } = new();
    public ItemShapeLibrary   ItemShapeLibrary   { get; } = new();
    public RandomEventLibrary RandomEventLibrary { get; } = new();
    public StatusEffectLibrary StatusEffectLibrary { get; } = new();
    public ForceTypeLibrary ForceTypeLibrary { get; } = new();
    public SectBannerLibrary SectBannerLibrary { get; } = new();
    public GeoRegionBannerLibrary GeoRegionBannerLibrary { get; } = new();
    public ImageTemplateLibrary ImageTemplateLibrary { get; } = new();
    public OperationLibrary OperationLibrary { get; } = new();
    public PortalLibrary PortalLibrary { get; } = new();

    public void Init()
    {
        AssetManager._instance.add(CultisysLibrary,      "cultisyses");
        AssetManager._instance.add(ElementRootLibrary,   "element_roots");
        AssetManager._instance.add(CustomMapModeLibrary, "custom_map_modes");
        AssetManager._instance.add(ItemShapeLibrary,   "item_shapes");
        AssetManager._instance.add(RandomEventLibrary, "random_events");
        AssetManager._instance.add(StatusEffectLibrary, "status_effects");
        AssetManager._instance.add(ForceTypeLibrary, "force_types");
        AssetManager._instance.add(SectBannerLibrary, "sect_banners");
        AssetManager._instance.add(GeoRegionBannerLibrary, "geo_region_banners");
        AssetManager._instance.add(ImageTemplateLibrary, "image_templates");
        AssetManager._instance.add(OperationLibrary, "operations");
        AssetManager._instance.add(PortalLibrary, "portals");
    }

    public void LinkAssets()
    {
        CultisysLibrary.linkAssets();
        ElementRootLibrary.linkAssets();
        CustomMapModeLibrary.linkAssets();
        ItemShapeLibrary.linkAssets();
        RandomEventLibrary.linkAssets();
        StatusEffectLibrary.post_init();
        ForceTypeLibrary.post_init();
        SectBannerLibrary.post_init();
        GeoRegionBannerLibrary.post_init();
        ImageTemplateLibrary.post_init();
        OperationLibrary.post_init();
        PortalLibrary.post_init();
    }
    public void PostInit()
    {
        CultisysLibrary.post_init();
        ElementRootLibrary.post_init();
        CustomMapModeLibrary.post_init();
        ItemShapeLibrary.post_init();
        RandomEventLibrary.post_init();
        StatusEffectLibrary.post_init();
        ForceTypeLibrary.post_init();
        SectBannerLibrary.post_init();
        GeoRegionBannerLibrary.post_init();
        ImageTemplateLibrary.post_init();
        OperationLibrary.post_init();
        PortalLibrary.post_init();
    }
}