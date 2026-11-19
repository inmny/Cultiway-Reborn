using System.Linq;
using Cultiway.Content.KingdomPolicies;

namespace Cultiway.Content.FamilyFarming;

/// <summary>每株小麦消耗一份肥料后收成翻倍，生长期和播收月份保持原样。</summary>
public static class FamilyCropFertilizer
{
    /// <summary>直接随原版作物建筑保存的单次施肥标记。</summary>
    private const string FertilizedKey = "cw.farm.wheat.fertilized";

    /// <summary>读取当前这一株作物是否已经施肥。</summary>
    /// <param name="crop">当前小麦。</param>
    public static bool IsFertilized(Building crop)
    { crop.data.get(FertilizedKey, out bool fertilized, false); return fertilized; }

    /// <summary>仅未成熟、尚未施肥且仍可用的小麦接受肥料。</summary>
    /// <param name="crop">待施肥的作物。</param>
    public static bool CanFertilize(Building crop) => crop?.asset.wheat == true && crop.isUsable() &&
        !crop.component_wheat.isMaxLevel() && (KingdomPolicyService.Has(crop, KingdomPolicyCard.FertilizerYield)
            ? !IsFertilized(crop) : !KingdomPolicyService.Has(crop, KingdomPolicyCard.SeasonalFarming) || FamilyCropCalendar.Season(crop) == 0);

    /// <summary>消耗农民携带的一份肥料，只登记增产，不修改生长阶段。</summary>
    /// <param name="actor">已经到达田间的农民。</param>
    /// <param name="crop">这次劳动选中的小麦。</param>
    public static bool Apply(Actor actor, Building crop)
    {
        if (!CanFertilize(crop) || actor.inventory.getResource("fertilizer") == 0) return false;
        actor.takeFromInventory("fertilizer", 1);
        if (KingdomPolicyService.Has(crop, KingdomPolicyCard.FertilizerYield)) crop.data.set(FertilizedKey, true);
        else crop.component_wheat.growFull();
        return true;
    }

    /// <summary>所有真实收割入口共用一次增产结算，基础收成仍由原有流程发放。</summary>
    /// <param name="crop">即将被收割移除的成熟小麦。</param>
    /// <param name="actor">实际获得收成的人物。</param>
    public static void AddHarvestBonus(Building crop, Actor actor)
    {
        if (actor == null || crop.asset.wheat != true || !crop.isUsable() ||
            !crop.component_wheat.isMaxLevel() || !IsFertilized(crop)) return;
        crop.data.set(FertilizedKey, false);
        int extra = crop.asset.resources_given.Where(r => r.id == FamilyFarmService.RawGrainId || r.id == "wheat").Sum(r => r.amount);
        actor.addToInventory(FamilyFarmService.HarvestResource(actor.city), extra);
    }
}
