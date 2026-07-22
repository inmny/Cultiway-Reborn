using Cultiway.Core.Components;
using Cultiway.Core.Semantics;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Semantics;

/// <summary>
/// 在材料实体完整组装后，根据其统一语义档案写入图标的三个可选换色槽。
/// </summary>
public static class IngredientColorService
{
    /// <summary>重新解析材料当前语义并覆盖图标颜色；不足三色的槽位保持为空。</summary>
    public static void Apply(Entity ingredient)
    {
        if (ingredient.IsNull || !ingredient.HasComponent<ItemIconData>()) return;

        var palette = SemanticColorResolver.Resolve(IngredientSemanticService.Build(ingredient));
        ref var iconData = ref ingredient.GetComponent<ItemIconData>();
        iconData.ColorHex1 = palette.GetHex(0);
        iconData.ColorHex2 = palette.GetHex(1);
        iconData.ColorHex3 = palette.GetHex(2);
    }
}
