using Cultiway.Core.Pathfinding;
using HarmonyLib;

namespace Cultiway.Patch;

/// <summary>
/// 地块通行属性变化后立即刷新后台寻路使用的静态视图。
/// 火焰状态不在此缓存，寻路读取时仍直接观察当前火焰数组。
/// </summary>
[HarmonyPatch(
    typeof(WorldTile),
    nameof(WorldTile.updateStats))]
internal static class PatchTileTraversalInfo
{
    [HarmonyPostfix]
    private static void RefreshTraversalInfo(
        WorldTile __instance)
    {
        TileTraversalInfo.Refresh(__instance);
    }
}
