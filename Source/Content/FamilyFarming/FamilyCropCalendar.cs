using System;
using Cultiway.Core;
using Cultiway.Content.KingdomPolicies;

namespace Cultiway.Content.FamilyFarming;

/// <summary>每株农民播种的小麦随原版建筑保存麦类和播种时刻。</summary>
public static class FamilyCropCalendar
{
    /// <summary>原版建筑内的麦类记录。</summary>
    private const string SeasonKey = "cw.farm.wheat.season";

    /// <summary>播种成功后才记录，翻土和认领不开始计算生长期。</summary>
    /// <param name="crop">刚由农民播种的作物。</param>
    public static void Register(Building crop)
    {
        int season = FarmSeasonRules.SowingSeason(Date.getCurrentMonth());
        if (crop?.asset.wheat != true || season == 0 || !KingdomPolicyService.Has(crop, KingdomPolicyCard.SeasonalFarming)) return;
        crop.data.set(SeasonKey, season);
    }
    /// <summary>读取麦类，玩家直接生成的作物保留原版生长。</summary>
    /// <param name="crop">当前作物。</param>
    public static int Season(Building crop)
    { crop.data.get(SeasonKey, out int season, 0); return season; }
    /// <summary>按统一日历和实际播种时刻计算月龄。</summary>
    /// <param name="crop">已登记的作物。</param>
    private static double Age(Building crop) => Math.Max(0, WorldCalendar.MonthsSince(crop.data.created_time));
    /// <summary>成熟并到了对应月份才能收割，内城最后收获由退耕规则另行判断。</summary>
    /// <param name="crop">准备收割的作物。</param>
    public static bool Ready(Building crop)
    {
        if (crop?.asset.wheat != true || !crop.isUsable()) return false;
        if (!KingdomPolicyService.Has(crop, KingdomPolicyCard.SeasonalFarming)) return crop.component_wheat.isMaxLevel();
        int season = Season(crop);
        return season == 0 ? crop.component_wheat.isMaxLevel() && FarmSeasonRules.HarvestMonth(Date.getCurrentMonth()) :
            FarmSeasonRules.CanHarvest(season, Age(crop), Date.getCurrentMonth());
    }
    /// <summary>按月龄更新原版外观，不再使用原版每级五十秒的生长速度。</summary>
    /// <param name="crop">原版麦类建筑。</param>
    public static bool Update(Building crop)
    {
        if (!KingdomPolicyService.Has(crop, KingdomPolicyCard.SeasonalFarming)) return true;
        int season = Season(crop);
        if (season == 0) return true;
        var wheat = crop.component_wheat;
        int level = Math.Min(wheat._max_level, (int)(Age(crop) / FarmSeasonRules.GrowingMonths(season) * wheat._max_level));
        if (level != wheat._current_level) { wheat._current_level = level; wheat.checkSprite(); }
        return false;
    }
}
