using System;
using Friflo.Engine.ECS;

namespace Cultiway.Core.Libraries;

/// <summary>
/// 特殊物品分类资产，统一描述分类识别、库藏占用和领取规则。
/// </summary>
public class SpecialItemCategoryAsset : Asset
{
    public string nameKey;
    public string descriptionKey;
    public string iconPath;
    public int order;
    public int baseContributionCost;
    public int storageWeight = 1;
    public float permissionCostMultiplier;
    public SpecialItemWithdrawalMode withdrawalMode;
    public Func<Entity, bool> matches;

    /// <summary>
    /// 判断指定特殊物品是否属于该分类。
    /// </summary>
    public bool Matches(Entity item)
    {
        return matches?.Invoke(item) == true;
    }

    /// <summary>
    /// 获取分类的本地化名称。
    /// </summary>
    public string GetName()
    {
        return (string.IsNullOrEmpty(nameKey) ? id : nameKey).Localize();
    }
}

/// <summary>
/// 特殊物品离开宗门库藏时采用的所有权处理方式。
/// </summary>
public enum SpecialItemWithdrawalMode
{
    Transfer,
    Loan
}
