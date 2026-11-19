using ai.behaviours;
using Cultiway.Content.FamilyFarming;
using HarmonyLib;
using Cultiway.Content.KingdomPolicies;

namespace Cultiway.Content.Patch;

/// <summary>原版的直接农活调用也只使用家庭田块，避免自动文明等入口重新生成风车周围的零散田地。</summary>
internal static class PatchFamilyFarmActions
{
    /// <summary>绑定本家庭的单个目标，不临时改写全村列表。</summary>
    /// <param name="actor">寻找农活的人物。</param>
    /// <param name="action">需要寻找的动作。</param>
    /// <param name="result">原版行为返回值。</param>
    private static bool Select(Actor actor, FamilyFarmService.FarmAction action, ref BehResult result)
    {
        var tile = FamilyFarmService.FindFieldTarget(actor, action);
        if (tile != null) { actor.beh_tile_target = tile; actor.beh_building_target = tile.building; }
        result = tile == null ? BehResult.Stop : BehResult.Continue; return false;
    }

    /// <summary>开垦只找家庭已登记的待耕地。</summary>
    /// <param name="pActor">原版农民。</param>
    /// <param name="__result">目标选择结果。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(BehFindTileForFarm), nameof(BehFindTileForFarm.execute))]
    private static bool Till(Actor pActor, ref BehResult __result) => !FamilyFarmService.IsPrivate(pActor.city) || Select(pActor, FamilyFarmService.FarmAction.Till, ref __result);

    /// <summary>播种只找本家庭的有效田格。</summary>
    /// <param name="pActor">原版农民。</param>
    /// <param name="__result">目标选择结果。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(BehFindFarmField), nameof(BehFindFarmField.execute))]
    private static bool Plant(Actor pActor, ref BehResult __result) => !FamilyFarmService.IsPrivate(pActor.city) || Select(pActor, FamilyFarmService.FarmAction.Plant, ref __result);

    /// <summary>收割优先本家庭田块，允许帮收同村家庭和退耕的成熟作物。</summary>
    /// <param name="pActor">原版农民。</param>
    /// <param name="__result">目标选择结果。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(BehFindWheat), nameof(BehFindWheat.execute))]
    private static bool Harvest(Actor pActor, ref BehResult __result) => Select(pActor, FamilyFarmService.FarmAction.Harvest, ref __result);

    /// <summary>施肥不选其他家庭和内城中的作物。</summary>
    /// <param name="pActor">原版农民。</param>
    /// <param name="__result">目标选择结果。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(BehCityActorFindUngrownCrop), nameof(BehCityActorFindUngrownCrop.execute))]
    private static bool Fertilize(Actor pActor, ref BehResult __result) => Select(pActor, FamilyFarmService.FarmAction.Fertilize, ref __result);

    /// <summary>走到开垦地点后再次验证归属和退耕状态。</summary>
    /// <param name="pActor">准备翻土的人物。</param>
    /// <param name="__result">原版执行结果。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(BehMakeFarm), nameof(BehMakeFarm.execute))]
    private static bool MakeField(Actor pActor, ref BehResult __result)
    {
        if (VillageDuties.VillageDutyService.CanTakeCivilianJob(pActor) && FamilyFarmService.CanUseTarget(pActor, pActor.beh_tile_target)) return true;
        __result = BehResult.Stop; return false;
    }

    /// <summary>播种完成前再次检查地块。</summary>
    /// <param name="pActor">准备播种的人物。</param>
    /// <param name="__result">原版执行结果。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(BehPlantCrops), nameof(BehPlantCrops.execute))]
    private static bool PlantCrop(Actor pActor, ref BehResult __result)
    {
        if ((!KingdomPolicyService.Has(pActor.city, KingdomPolicyCard.SeasonalFarming) ||
             FarmSeasonRules.SowingSeason(Date.getCurrentMonth()) != 0) && FamilyFarmService.CanUseTarget(pActor, pActor.beh_tile_target)) return true;
        __result = BehResult.Stop; return false;
    }

    /// <summary>原版播种入口同样在成功后记录实际播种时刻。</summary>
    /// <param name="pActor">完成播种的农民。</param>
    /// <param name="__result">原版播种结果。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(BehPlantCrops), nameof(BehPlantCrops.execute))]
    private static void RecordSowing(Actor pActor, BehResult __result)
    { if (__result == BehResult.Continue) FamilyCropCalendar.Register(pActor.beh_tile_target?.building); }

    /// <summary>原版施肥同样消耗一份肥料增加单株收成，并继续核对作息、家庭与退耕。</summary>
    /// <param name="pActor">准备施肥的人物。</param>
    /// <param name="__result">原版执行结果。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(BehCityActorFertilizeCrop), nameof(BehCityActorFertilizeCrop.execute))]
    private static bool FertilizeCrop(Actor pActor, ref BehResult __result)
    {
        bool applied = VillageDuties.VillageDutyService.CanTakeCivilianJob(pActor) &&
            FamilyFarmService.CanUseTarget(pActor, pActor.beh_building_target?.current_tile) &&
            FamilyCropFertilizer.Apply(pActor, pActor.beh_building_target);
        __result = applied ? BehResult.Continue : BehResult.Stop;
        if (applied) pActor.addLoot(SimGlobals.m.coins_for_fertilize);
        return false;
    }

    /// <summary>在原版移除作物前发放该株的施肥增产，兼顾两条收割流程。</summary>
    /// <param name="__instance">正在收割的建筑。</param>
    /// <param name="pBy">实际采收人物。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Building), nameof(Building.extractResources))]
    private static void HarvestBonus(Building __instance, Actor pBy) => FamilyCropFertilizer.AddHarvestBonus(__instance, pBy);

    /// <summary>其他原版采集职业保持原样，麦类收割仍须符合本村登记与对应农时。</summary>
    /// <param name="pActor">实际采集的人物。</param>
    /// <param name="__result">原版执行结果。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(BehExtractResourcesFromBuilding), nameof(BehExtractResourcesFromBuilding.execute))]
    private static bool Extract(Actor pActor, ref BehResult __result)
    {
        if (pActor.beh_building_target?.asset.wheat != true) return true;
        bool valid = FamilyFarmService.CanUseTarget(pActor, pActor.beh_building_target.current_tile, true);
        if (valid) FamilyFarmService.HarvestGrain(pActor, pActor.beh_building_target);
        __result = valid ? BehResult.Continue : BehResult.Stop; return false;
    }

    /// <summary>原粮只交到风车或普通待加工仓位，不能借食物交付绕过加工。</summary>
    /// <param name="pActor">原版运粮的人物。</param>
    /// <param name="__result">原版选择结果。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(BehCityActorFindStorageWheat), nameof(BehCityActorFindStorageWheat.execute))]
    private static bool Store(Actor pActor, ref BehResult __result)
    {
        if (pActor.inventory.getResource(FamilyFarmService.RawGrainId) == 0) return true;
        var storage = FamilyFarmService.FindGrainStorage(pActor);
        if (storage != null) pActor.beh_building_target = storage;
        __result = storage == null ? BehResult.Stop : BehResult.Continue; return false;
    }
}
