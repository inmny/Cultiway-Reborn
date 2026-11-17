using System;
using HarmonyLib;

namespace Cultiway.Content.Patch;

/// <summary>读取原物品时保留已保存耐久，避免原版计算价值顺带修复磨损。</summary>
internal static class PatchItemDurability
{
    /// <summary>原版读取会把耐久重设为资产上限，先保留实际保存值。</summary>
    /// <param name="pData">原物品正式资料。</param><param name="__state">此次读取前的耐久。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Item), nameof(Item.loadData))]
    private static void Capture(ItemData pData, out int __state) => __state = pData.durability;

    /// <summary>原版完成资产和价值初始化后归还同一物品的耐久。</summary>
    /// <param name="__instance">同一原物品。</param><param name="__state">此次实际保存值。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(Item), nameof(Item.loadData))]
    private static void Restore(Item __instance, int __state) => __instance.data.durability = Math.Max(0, __state);
}
