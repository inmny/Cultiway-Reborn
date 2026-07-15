using Cultiway.Content.Components;
using Cultiway.Core.Libraries;

namespace Cultiway.Content.Libraries;

/// <summary>
/// 能力和 atom 组合阶段读取的确定性材料上下文。
/// </summary>
public sealed class ArtifactRecipeContext
{
    public string dominant_shape_id;
    public string main_material_shape_id;
    public int quality_stage;
    public int quality_level;
    public ArtifactMaterialData material_data;

    public int ingredient_count => material_data.ingredient_count;

    public int CountShape(ItemShapeAsset shape)
    {
        return shape == null ? 0 : material_data.CountShape(shape.id);
    }

    public bool HasShape(ItemShapeAsset shape)
    {
        return CountShape(shape) > 0;
    }

    public float GetTrait(string key)
    {
        return material_data.GetTrait(key);
    }
}
