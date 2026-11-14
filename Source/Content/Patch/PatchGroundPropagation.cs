using Cultiway.Content.Skills;
using HarmonyLib;

namespace Cultiway.Content.Patch;

/// <summary>向主线程的只读地面采集报告真实地块类型变化。</summary>
internal static class PatchGroundPropagation
{
    /// <summary>所有已完成的原版地块类型修改共用这一入口。</summary>
    /// <param name="__instance">已经修改的地块。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(WorldTile), nameof(WorldTile.setTileType), typeof(TileType), typeof(bool))]
    private static void Changed(WorldTile __instance) => PropagationGroundMap.Changed(__instance);
}
