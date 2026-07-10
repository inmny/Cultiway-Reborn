using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Cultiway.Core.Libraries;

/// <summary>
/// 特殊物品分类资产库。
/// </summary>
public class SpecialItemCategoryLibrary : AssetLibrary<SpecialItemCategoryAsset>
{
    /// <summary>
    /// 按分类顺序解析指定特殊物品所属的首个分类。
    /// </summary>
    public SpecialItemCategoryAsset Resolve(Entity item)
    {
        SpecialItemCategoryAsset result = null;
        for (int i = 0; i < list.Count; i++)
        {
            SpecialItemCategoryAsset category = list[i];
            if (!category.Matches(item)) continue;
            if (result == null || category.order < result.order)
            {
                result = category;
            }
        }

        return result;
    }

    /// <summary>
    /// 获取按界面顺序排列的全部特殊物品分类。
    /// </summary>
    public List<SpecialItemCategoryAsset> GetOrdered()
    {
        List<SpecialItemCategoryAsset> result = new(list);
        result.Sort((left, right) => left.order.CompareTo(right.order));
        return result;
    }
}
