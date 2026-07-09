using System.Collections.Generic;
using Cultiway.Core.Libraries;

namespace Cultiway.Content.Libraries;

public struct ArtifactRecipeContext
{
    public string dominant_shape_id;
    public string main_material_shape_id;
    public int quality_stage;
    public int quality_level;
    public int ingredient_count;
    public Dictionary<string, int> shape_counts;

    public int CountShape(ItemShapeAsset shape)
    {
        if (shape == null || shape_counts == null) return 0;
        return shape_counts.TryGetValue(shape.id, out var count) ? count : 0;
    }

    public bool HasShape(ItemShapeAsset shape)
    {
        return CountShape(shape) > 0;
    }
}
