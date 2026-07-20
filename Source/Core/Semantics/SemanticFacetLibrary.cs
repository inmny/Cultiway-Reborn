namespace Cultiway.Core.Semantics;

/// <summary>
/// 全局语义维度库。这里只定义跨内容系统都可理解的维度，不承载具体玩法语义。
/// </summary>
public sealed class SemanticFacetLibrary : AssetLibrary<SemanticFacetAsset>
{
    public SemanticFacetAsset Element      { get; private set; }
    public SemanticFacetAsset Form         { get; private set; }
    public SemanticFacetAsset Delivery     { get; private set; }
    public SemanticFacetAsset Motion       { get; private set; }
    public SemanticFacetAsset Effect       { get; private set; }
    public SemanticFacetAsset Role         { get; private set; }
    public SemanticFacetAsset Theme        { get; private set; }
    public SemanticFacetAsset Craft        { get; private set; }
    public SemanticFacetAsset Resource     { get; private set; }
    public SemanticFacetAsset Material     { get; private set; }
    public SemanticFacetAsset Realm        { get; private set; }
    public SemanticFacetAsset Path         { get; private set; }
    public SemanticFacetAsset Organization { get; private set; }
    public SemanticFacetAsset Trait        { get; private set; }

    public override void init()
    {
        Element = Add("element", "ui/icons/iconMana");
        Form = Add("form", "ui/icons/iconBox");
        Delivery = Add("delivery", "ui/icons/iconArrowDestination");
        Motion = Add("motion", "ui/icons/iconSpeed");
        Effect = Add("effect", "ui/icons/iconStatusBudding");
        Role = Add("role", "ui/icons/citizen_jobs/iconCitizenJobBuilder");
        Theme = Add("theme", "ui/icons/iconAges");
        Craft = Add("craft", "ui/icons/iconCraftIron");
        Resource = Add("resource", "ui/icons/iconMana");
        Material = Add("material", "ui/icons/iconArtifact");
        Realm = Add("realm", "ui/icons/iconLevels");
        Path = Add("path", "ui/icons/religion_traits/religion_trait_path_of_unity");
        Organization = Add("organization", "ui/icons/iconAlliance");
        Trait = Add("trait", "ui/icons/iconEditTrait");
    }

    private SemanticFacetAsset Add(string id, string iconPath)
    {
        return add(new SemanticFacetAsset
        {
            id = id,
            name_key = $"Cultiway.SemanticFacet.{id}",
            icon_path = iconPath
        });
    }
}
