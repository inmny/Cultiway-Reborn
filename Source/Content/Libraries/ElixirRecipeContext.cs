namespace Cultiway.Content.Libraries;

public struct ElixirRecipeContext
{
    public int ingredient_count;
    public int primary_element_index;
    public int secondary_element_index;
    public float primary_element_value;
    public float secondary_element_value;
    public string main_shape_id;

    /// <summary>配方材料中出现次数最多的金丹规范名称。</summary>
    public string main_jindan_name;
    public string effect_hint;
    public float strength;
    public int quality_stage;
    public int quality_level;
}
