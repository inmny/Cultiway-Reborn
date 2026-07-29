using Cultiway.Core.Performance;
using HarmonyLib;

namespace Cultiway.Patch;

/// <summary>
/// 当一个王国在全图没有任何有效敌方对象时，直接生成原版的空分块缓存项。
/// 每个新分块键仍按原版规则消耗一次随机判定，确保后续逻辑的随机序列不变。
/// </summary>
internal static class PatchEnemyFinderCache
{
    private static readonly AccessTools.FieldRef<
        EnemyFinderContainer,
        Kingdom> KingdomField =
        AccessTools.FieldRefAccess<
            EnemyFinderContainer,
            Kingdom>("_kingdom");

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(EnemyFinderContainer),
        nameof(EnemyFinderContainer.getData))]
    private static bool getData(
        EnemyFinderContainer __instance,
        MapChunk pChunk,
        int pRange,
        ref EnemyFinderData __result)
    {
        int key =
            pChunk.id * 10000 +
            pRange;
        if (__instance.dict_data.TryGetValue(
                key,
                out EnemyFinderData cached))
        {
            EnemiesFinder.counter_reused++;
            __result = cached;
            return false;
        }

        if (EnemyPresenceCache.HasNegativeKey(
                __instance,
                key))
        {
            EnemiesFinder.counter_reused++;
            __result =
                EnemyPresenceCache
                    .SharedEmptyResult;
            return false;
        }

        Kingdom kingdom =
            KingdomField(__instance);
        if (!EnemyPresenceCache
                .IsPreparationActive ||
            kingdom == null ||
            EnemyPresenceCache
                .HasPopulatedEnemy(kingdom))
        {
            return true;
        }

        EnemyPresenceCache.AddNegativeKey(
            __instance,
            key);
        if (!kingdom.asset.force_look_all_chunks &&
            pRange != 0)
        {
            Randy.randomChance(0.8f);
        }

        EnemyPresenceCache
            .RecordSkippedChunkBuild();
        __result =
            EnemyPresenceCache
                .SharedEmptyResult;
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(
        typeof(EnemyFinderContainer),
        nameof(EnemyFinderContainer.clear))]
    private static void clearContainer(
        EnemyFinderContainer __instance)
    {
        EnemyPresenceCache
            .ClearNegativeKeys(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(
        typeof(EnemiesFinder),
        nameof(EnemiesFinder.clear))]
    private static void clear()
    {
        EnemyPresenceCache.Clear();
    }
}
