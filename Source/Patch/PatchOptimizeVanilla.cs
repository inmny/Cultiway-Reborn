using System.Collections.Generic;
using ai.behaviours;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchOptimizeVanilla
{

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
}
