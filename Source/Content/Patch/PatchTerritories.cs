using System;
using Cultiway.Content.Territories;
using HarmonyLib;

namespace Cultiway.Content.Patch;

/// <summary>在原版建村候选和领地写入处执行定制边界，加载存档时保持原版恢复流程。</summary>
internal static class PatchTerritories
{
    /// <summary>地图和村庄加载完成后恢复范围及颜色，暂停世界也会执行。</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(MapBox), nameof(MapBox.finishingUpLoading))]
    private static void Load() => TerritoryService.Load();

    /// <summary>原版找建村地点时排除已被定制村庄占用的整块区域。</summary>
    /// <param name="__instance">候选格子。</param>
    /// <param name="__result">原版地形条件的结果。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(TileZone), nameof(TileZone.canStartCityHere))]
    private static void CanStart(TileZone __instance, ref bool __result)
    {
        if (__result && Config.game_loaded && !Config.worldLoading) __result = TerritoryService.CanFound(__instance);
    }

    /// <summary>同时覆盖已缓存的建村候选和单位的就地建村检查。</summary>
    /// <param name="__instance">候选格子。</param>
    /// <param name="__result">原版建村检查结果。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(TileZone), nameof(TileZone.isGoodForNewCity), new Type[] { })]
    private static void GoodForCity(TileZone __instance, ref bool __result)
    {
        if (__result && Config.game_loaded && !Config.worldLoading) __result = TerritoryService.CanFound(__instance);
    }

    /// <summary>原版选择扩张地点时提前排除不能领取的格子。</summary>
    /// <param name="__instance">候选格子。</param>
    /// <param name="pCity">正在扩张的村庄。</param>
    /// <param name="__result">原版扩张检查结果。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(TileZone), nameof(TileZone.canBeClaimedByCity))]
    private static void CanClaim(TileZone __instance, City pCity, ref bool __result)
    {
        if (__result && Config.game_loaded && !Config.worldLoading) __result = TerritoryService.CanClaim(pCity, __instance);
    }

    /// <summary>在修改原版领地列表之前守住边界，首次领取时绑定唯一村庄。</summary>
    /// <param name="__instance">领取领地的村庄。</param>
    /// <param name="pZone">即将领取的格子。</param>
    /// <returns>原版是否继续执行。</returns>
    [HarmonyPrefix, HarmonyPatch(typeof(City), nameof(City.addZone))]
    private static bool AddZone(City __instance, TileZone pZone)
        => !Config.game_loaded || Config.worldLoading || TerritoryService.PrepareClaim(__instance, pZone);

    /// <summary>定制区域允许独立建村，不被原版相邻村庄自动吞并；保留种族和建村资格检查。</summary>
    /// <param name="pActor">尝试建立文明的单位。</param>
    /// <param name="__result">是否可以在当前位置建村。</param>
    /// <returns>未划定区域继续执行原版规则。</returns>
    [HarmonyPrefix, HarmonyPatch(typeof(CityManager), nameof(CityManager.canStartNewCityCivilizationHere))]
    private static bool FirstCivilization(Actor pActor, ref bool __result)
    {
        if (!Config.game_loaded || Config.worldLoading || TerritoryService.Get(pActor.current_tile.zone) == null) return true;
        __result = TerritoryService.CanFound(pActor.current_tile.zone)
                   && !pActor.kingdom.asset.is_forced_by_trait && pActor.canBuildNewCity()
                   && AssetManager.kingdoms.get(pActor.asset.kingdom_id_civilization)?.civ == true;
        return false;
    }

    /// <summary>原版抓取世界快照前保存地区，普通保存和自动保存共用。</summary>
    [HarmonyPrefix, HarmonyPatch(typeof(SavedMap), nameof(SavedMap.create))]
    private static void Save()
    {
        if (Config.game_loaded && !Config.worldLoading) TerritoryService.Save();
    }
}
