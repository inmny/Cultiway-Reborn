using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Sects;

public static class SectTreasureMetrics
{
    public static int GetCapacity(Sect sect)
    {
        return Mathf.Max(0, Mathf.RoundToInt(sect.base_stats[WorldboxGame.BaseStats.TreasureCapacity.id]));
    }

    public static int GetUsedCapacity(Sect sect)
    {
        int used = 0;
        foreach (Entity item in sect.Treasures.GetOwnedItems())
        {
            SpecialItemCategoryAsset category = ModClass.L.SpecialItemCategoryLibrary.Resolve(item);
            if (category != null)
            {
                used += Mathf.Max(1, category.storageWeight);
            }
        }

        return used;
    }

    public static int GetStage(Entity item)
    {
        return item.TryGetComponent(out ItemLevel level) ? level.Stage : 0;
    }

    public static int GetValue(Entity item)
    {
        SpecialItemCategoryAsset category = ModClass.L.SpecialItemCategoryLibrary.Resolve(item);
        if (category == null) return 0;

        int rank = item.TryGetComponent(out ItemLevel level) ? Mathf.Max(0, level) : 0;
        return Mathf.Max(1, category.baseContributionCost * (rank + 1));
    }

    public static int GetContributionReward(Entity item)
    {
        return Mathf.Max(1, Mathf.CeilToInt(GetValue(item) * SectConst.TreasureContributionRewardRatio));
    }

}
