using System.Linq;
using Cultiway.Content.KingdomPolicies;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using ai.behaviours;
using Cultiway.Content.FamilyFarming;
using Cultiway.Content.VillageDuties;

namespace Cultiway.Content.Patch;

/// <summary>国家政策只改变所属居民的生产资格和城市建造，不放宽征兵年龄。</summary>
internal static class PatchKingdomPolicies
{
    /// <summary>正常易主前记录旧国家制度，创建村庄和读档时不执行切换。</summary>
    /// <param name="__instance">即将改变归属的村庄。</param>
    /// <param name="pKingdom">即将采用制度的国家。</param>
    /// <param name="pFromLoad">是否正在恢复存档。</param>
    /// <param name="__state">此次真实易主前的制度。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(City), nameof(City.setKingdom))]
    private static void BeforeKingdomChange(City __instance, Kingdom pKingdom, bool pFromLoad, out KingdomPolicyData __state)
    {
        __state = !pFromLoad && Config.game_loaded && !SmoothLoader.isLoading() && __instance.kingdom != null &&
            pKingdom != null && __instance.kingdom != pKingdom ? KingdomPolicyService.Get(__instance.kingdom) : null;
    }

    /// <summary>易主即采用新国家制度，保留在田作物并重新安排职业。</summary>
    /// <param name="__instance">已完成归属变更的村庄。</param>
    /// <param name="__state">此次真实易主前的制度。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(City), nameof(City.setKingdom))]
    private static void AfterKingdomChange(City __instance, KingdomPolicyData __state)
    {
        if (__state == null) return;
        FamilyFarmService.PolicyChanged(__instance, __state, KingdomPolicyService.Get(__instance.kingdom));
        VillageOccupationService.PolicyChanged(__instance);
        CityBehCheckFarms.check(__instance);
        __instance.getLoyalty(pForceRecalc: true);
    }

    /// <summary>风车只有公有农田需要原版耕地选址条件。</summary>
    /// <param name="asset">正在检查的建筑。</param>
    /// <param name="city">建设所属村庄。</param>
    private static bool NeedsFarm(BuildingAsset asset, City city) => asset.needs_farms_ground &&
        (asset.type != "type_windmill" || !KingdomPolicyService.Has(city, KingdomPolicyCard.PrivateFarms));

    /// <summary>检查单格放置时按该格所属国家判断风车要求。</summary>
    /// <param name="asset">正在检查的建筑。</param>
    /// <param name="tile">建筑所在格。</param>
    private static bool NeedsFarmTile(BuildingAsset asset, WorldTile tile) => NeedsFarm(asset, tile.zone?.city);

    /// <summary>按村庄政策替换风车的固定全局选址开关。</summary>
    /// <param name="instructions">原版城市建造逻辑。</param>
    [HarmonyTranspiler, HarmonyPatch(typeof(CityBehBuild), nameof(CityBehBuild.tryToBuild))]
    private static IEnumerable<CodeInstruction> MillPlacement(IEnumerable<CodeInstruction> instructions) => ReplaceFarmCheck(instructions, nameof(NeedsFarm));

    /// <summary>按地格所属国家检查风车，其他建筑条件保留。</summary>
    /// <param name="instructions">原版单格建造逻辑。</param>
    [HarmonyTranspiler, HarmonyPatch(typeof(WorldTile), nameof(WorldTile.canBuildOn))]
    private static IEnumerable<CodeInstruction> MillTile(IEnumerable<CodeInstruction> instructions) => ReplaceFarmCheck(instructions, nameof(NeedsFarmTile));

    /// <summary>原版检查的第二个参数均为当前函数首个参数。</summary>
    /// <param name="instructions">原版字段读取。</param>
    /// <param name="method">按城市或地格查询的替代函数。</param>
    private static IEnumerable<CodeInstruction> ReplaceFarmCheck(IEnumerable<CodeInstruction> instructions, string method)
    {
        foreach (var instruction in instructions)
        {
            if (!instruction.LoadsField(AccessTools.Field(typeof(BuildingAsset), nameof(BuildingAsset.needs_farms_ground))))
            { yield return instruction; continue; }
            var argument = new CodeInstruction(OpCodes.Ldarg_0); instruction.MoveLabelsTo(argument);
            yield return argument;
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PatchKingdomPolicies), method));
        }
    }

    /// <summary>有国家归属的智慧居民按政策决定未成年人是否可以生产。</summary>
    /// <param name="__instance">原版查询工作资格的人物。</param>
    /// <param name="__result">最终工作资格。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.canWork))]
    private static void WorkAge(Actor __instance, ref bool __result)
    {
        if (__instance.city?.kingdom != null && __instance.isSapient() && !__instance.asset.is_boat)
            __result = KingdomPolicyService.CanProduce(__instance);
    }

    /// <summary>城建优先关闭时恢复原版农田地表限制，其他完整占地规则继续使用原版结果。</summary>
    /// <param name="__instance">已经检查的占地列表。</param>
    /// <param name="pCity">准备建设的城市。</param>
    /// <param name="pNewBuildingAsset">实际建筑。</param>
    /// <param name="pType">新建或读档用途。</param>
    /// <param name="__result">完整选址结果。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(BuildingManager), nameof(BuildingManager.canBuildFrom))]
    private static void FarmConstruction(BuildingManager __instance, City pCity, BuildingAsset pNewBuildingAsset,
        BuildPlacingType pType, ref bool __result)
    {
        if (__result && pCity != null && pType == BuildPlacingType.New && pNewBuildingAsset.city_building && pNewBuildingAsset.only_build_tiles &&
            !pNewBuildingAsset.flora && !KingdomPolicyService.Has(pCity, KingdomPolicyCard.ConstructionPriority))
            __result = !__instance._temp_list_tiles.Any(t => t.Type.farm_field);
    }
}
