using System;
using System.Collections.Generic;
using ai.behaviours;
using Cultiway.Core.Performance;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchOptimizeVanilla
{
    [ThreadStatic]
    private static MapChunk[] nearbyChunkBuffer;

    [HarmonyPrefix, HarmonyPatch(typeof(Toolbox), "getBuildingsTypeFromChunk")]
    private static bool GetBuildingsTypeFromChunkPrefix(
        MapChunk pChunk,
        string pType,
        bool pOnlyNonTargeted,
        bool pOnlyWithResources,
        ref IEnumerable<Building> __result)
    {
        __result = EnumerateBuildings(
            pChunk,
            pType,
            pOnlyNonTargeted,
            pOnlyWithResources);
        return false;
    }

    private static IEnumerable<Building> EnumerateBuildings(
        MapChunk chunk,
        string type,
        bool onlyNonTargeted,
        bool onlyWithResources)
    {
        foreach (Building building in Finder.getBuildingsFromChunk(
                     chunk.tiles[0],
                     0,
                     0,
                     pRandom: true))
        {
            if (building.asset.type != type)
            {
                continue;
            }

            if (onlyWithResources &&
                !building.hasResourcesToCollect())
            {
                continue;
            }

            if (!building.isUsable())
            {
                continue;
            }

            if (onlyNonTargeted &&
                building.current_tile.isTargeted())
            {
                continue;
            }

            yield return building;
        }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(BehFindMeatSource), "getClosestMeatActor")]
    private static bool GetClosestMeatActorPrefix(
        BehFindMeatSource __instance,
        Actor pActor,
        ref Actor __result)
    {
        bool stopEarly = Randy.randomBool();
        WorldTile origin = pActor.current_tile;
        float closestDistanceSquared = int.MaxValue;
        Actor closest = null;
        int chunkRadius = Randy.randomInt(1, 3);
        MeatTargetType targetType = __instance._meat_target_type;
        bool checkForFactions = __instance._check_for_factions;
        foreach (Actor target in Finder.getUnitsFromChunk(
                     origin,
                     chunkRadius,
                     0f,
                     stopEarly))
        {
            float distanceSquared = Toolbox.SquaredDistTile(
                target.current_tile,
                origin);
            if (distanceSquared >= closestDistanceSquared ||
                target == pActor ||
                !IsMatchingMeatSource(pActor, target, targetType) ||
                target.asset.actor_size > pActor.asset.actor_size ||
                !target.current_tile.isSameIsland(origin) ||
                !pActor.canAttackTarget(target, checkForFactions))
            {
                continue;
            }

            closestDistanceSquared = distanceSquared;
            closest = target;
            if (stopEarly &&
                Randy.randomBool())
            {
                break;
            }
        }

        __result = closest;
        return false;
    }

    private static bool IsMatchingMeatSource(
        Actor hunter,
        Actor target,
        MeatTargetType targetType)
    {
        switch (targetType)
        {
            case MeatTargetType.Meat:
                return target.asset.source_meat &&
                       !target.isSameSpecies(hunter.asset.id);
            case MeatTargetType.MeatSameSpecies:
                return target.isSameSpecies(hunter.asset.id);
            case MeatTargetType.Insect:
                return target.asset.source_meat_insect &&
                       !target.isSameSpecies(hunter.asset.id);
            default:
                return true;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(BehTryFindTargetWithStatusNearby),
        "getClosestActorWithStatus")]
    private static bool GetClosestActorWithStatusPrefix(
        Actor __0,
        string[] __1,
        ref Actor __result)
    {
        if (NearbyStatusTargetIndex.MayContainNearby(__0, __1))
        {
            return true;
        }

        ConsumeEmptyNearbyStatusSearchRandoms(__0.current_tile);
        __result = null;
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(
        typeof(StatusManager),
        nameof(StatusManager.newStatus))]
    private static void NewStatusPostfix(
        BaseSimObject pSimObject,
        StatusAsset pAsset)
    {
        NearbyStatusTargetIndex.NotifyStatusAdded(
            pSimObject,
            pAsset);
    }

    /// <summary>
    /// 原版在没有匹配目标时仍会为 chunk 和角色列表生成随机起点。
    /// 快速排除扫描后补回同样的随机调用，避免扰动后续玩法随机序列。
    /// </summary>
    private static void ConsumeEmptyNearbyStatusSearchRandoms(
        WorldTile origin)
    {
        bool randomizeUnits = Randy.randomBool();
        MapChunk[] chunkBuffer =
            nearbyChunkBuffer ??= new MapChunk[9];
        int chunkCount = 0;
        MapChunkManager manager = World.world.map_chunk_manager;
        for (int x = origin.chunk.x - 1;
             x <= origin.chunk.x + 1;
             x++)
        {
            for (int y = origin.chunk.y - 1;
                 y <= origin.chunk.y + 1;
                 y++)
            {
                MapChunk chunk = manager.get(x, y);
                if (chunk != null)
                {
                    chunkBuffer[chunkCount++] = chunk;
                }
            }
        }

        int chunkOffset = Randy.randomInt(0, chunkCount);
        if (!randomizeUnits || chunkCount == 0)
        {
            return;
        }

        for (int i = 0; i < chunkCount; i++)
        {
            MapChunk chunk =
                chunkBuffer[(i + chunkOffset) % chunkCount];
            Randy.randomInt(
                0,
                chunk.objects.units_all.Count);
        }
    }
}
