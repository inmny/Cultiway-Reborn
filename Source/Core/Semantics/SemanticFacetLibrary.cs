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
        Element = Add("element");
        Form = Add("form");
        Delivery = Add("delivery");
        Motion = Add("motion");
        Effect = Add("effect");
        Role = Add("role");
        Theme = Add("theme");
        Craft = Add("craft");
        Resource = Add("resource");
        Material = Add("material");
        Realm = Add("realm");
        Path = Add("path");
        Organization = Add("organization");
        Trait = Add("trait");
    }

    private SemanticFacetAsset Add(string id)
    {
        return add(new SemanticFacetAsset
        {
            id = id,
            name_key = $"Cultiway.SemanticFacet.{id}"
        });
    }
}
