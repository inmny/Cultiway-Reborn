using Cultiway.Utils;

namespace Cultiway.Content.AIGC;

public sealed class IngredientNamingContext
{
    public string SourceAssetId;
    public string SourceName;
    public string ShapeId;
    public string ElementRootId;
    public int PrimaryElementIndex = NamingRuleUtils.NoElement;
    public int SecondaryElementIndex = NamingRuleUtils.NoElement;
    public float PrimaryElementValue;
    public float SecondaryElementValue;
    public float ElementStrength;

    /// <summary>材料携带的金丹规范名称。</summary>
    public string JindanName;
    public float JindanStrength;
    public int XianLevel;
    public float PowerLevel;
    public int QualityStage;
    public int QualityLevel;
}
