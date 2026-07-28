using System;
using System.Collections.Generic;
using ai.behaviours;
using Cultiway.Core.Performance;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchOptimizeVanilla
{
    [ThreadStatic]
    private static List<Actor> socializeBestTargets;

    [ThreadStatic]
    private static List<Actor> socializeNormalTargets;

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
    [HarmonyPatch(typeof(Finder), nameof(Finder.findTileInChunk))]
    private static bool FindTileInChunkPrefix(
        WorldTile pTile,
        TileFinderType pTileType,
        ref WorldTile __result)
    {
        if (pTileType != TileFinderType.FreeTile ||
            !FreeTileSearchIndex.TryFind(
                pTile,
                out WorldTile tile))
        {
            return true;
        }

        __result = tile;
        return false;
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
        if (!NearbyStatusTargetIndex.TryFindClosest(
                __0,
                __1,
                out Actor target))
        {
            return true;
        }

        __result = target;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BehFindLover), nameof(BehFindLover.execute))]
    private static bool FindLoverExecutePrefix(
        Actor pActor,
        ref BehResult __result)
    {
        if (pActor.hasLover())
        {
            __result = BehResult.Stop;
            return false;
        }

        Actor lover = FindNearbyLover(pActor);
        if (lover == null && pActor.hasCity())
        {
            lover = FindCityLover(pActor);
        }

        if (lover != null)
        {
            pActor.becomeLoversWith(lover);
        }

        __result = BehResult.Continue;
        return false;
    }

    /// <summary>
    /// 保留原版 chunk 随机起点和成员顺序，直接使用索引访问，
    /// 避免 Finder 迭代器、接口枚举器与共享静态缓冲区的开销。
    /// </summary>
    private static Actor FindNearbyLover(Actor actor)
    {
        WorldTile origin = actor.current_tile;
        MapChunk[] chunks =
            ChunkWindowIndex.Get(origin.chunk, 1);
        int chunkCount = chunks.Length;

        int chunkOffset = Randy.randomInt(0, chunkCount);
        for (int i = 0; i < chunkCount; i++)
        {
            List<Actor> units =
                chunks[(i + chunkOffset) % chunkCount]
                    .objects.units_all;
            int count = units.Count;
            for (int j = 0; j < count; j++)
            {
                Actor target = units[j];
                if (!target.isAlive() ||
                    !IsPossibleLover(actor, target))
                {
                    continue;
                }

                return target;
            }
        }

        return null;
    }

    private static Actor FindCityLover(Actor actor)
    {
        List<Actor> units = actor.city.units;
        int count = units.Count;
        int offset = Randy.randomInt(0, count);
        for (int i = 0; i < count; i++)
        {
            Actor target = units[(i + offset) % count];
            if (IsPossibleLover(actor, target) &&
                target.inOwnCityBorders())
            {
                return target;
            }
        }

        return null;
    }

    private static bool IsPossibleLover(
        Actor actor,
        Actor target)
    {
        return target != actor &&
               target.hasSubspecies() &&
               target.isAlive() &&
               target.canFallInLoveWith(actor);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(BehTryToSocialize),
        nameof(BehTryToSocialize.execute))]
    private static bool TryToSocializeExecutePrefix(
        BehTryToSocialize __instance,
        Actor pActor,
        ref BehResult __result)
    {
        pActor.resetSocialize();
        Actor target = FindSocializeTarget(pActor);
        if (target == null)
        {
            __result = BehResult.Stop;
            return false;
        }

        pActor.beh_actor_target = target;
        if (pActor.canFallInLoveWith(target))
        {
            pActor.becomeLoversWith(target);
        }

        pActor.resetSocialize();
        target.resetSocialize();
        __result =
            pActor.hasTelepathicLink() &&
            target.hasTelepathicLink()
                ? __instance.forceTask(
                    pActor,
                    "socialize_do_talk",
                    pClean: false)
                : __instance.forceTask(
                    pActor,
                    "socialize_go_to_target",
                    pClean: false);
        return false;
    }

    /// <summary>
    /// 按照原版相同的随机起点和候选顺序直接扫描角色，
    /// 避免每次社交搜索租用两个 ListPool 并创建 Finder 迭代器。
    /// </summary>
    private static Actor FindSocializeTarget(Actor actor)
    {
        List<Actor> bestTargets =
            socializeBestTargets ??= new List<Actor>(4);
        List<Actor> normalTargets =
            socializeNormalTargets ??= new List<Actor>(8);
        bestTargets.Clear();
        normalTargets.Clear();

        bool needsOppositeSex =
            actor.subspecies
                .needOppositeSexTypeForReproduction();
        bool animalWhisperer =
            actor.hasCulture() &&
            actor.culture.hasTrait("animal_whisperers");
        bool telepathic = actor.hasTelepathicLink();
        if (telepathic)
        {
            AddTelepathicSocializeTargets(
                actor,
                bestTargets,
                normalTargets);
        }

        int radius = telepathic ? 2 : 1;
        MapChunk[] chunks = ChunkWindowIndex.Get(
            actor.current_tile.chunk,
            radius);
        int chunkCount = chunks.Length;
        int chunkOffset = Randy.randomInt(0, chunkCount);
        bool actorIsKingdomCiv = actor.isKingdomCiv();
        bool stopSearch = false;
        for (int i = 0;
             i < chunkCount && !stopSearch;
             i++)
        {
            List<Actor> units =
                chunks[(i + chunkOffset) % chunkCount]
                    .objects.units_all;
            int count = units.Count;
            int unitOffset = Randy.randomInt(0, count);
            for (int j = 0; j < count; j++)
            {
                Actor target =
                    units[(j + unitOffset) % count];
                if (!target.isAlive() ||
                    !actor.canTalkWith(target))
                {
                    continue;
                }

                if (actorIsKingdomCiv)
                {
                    if (target.isKingdomMob() &&
                        !animalWhisperer)
                    {
                        continue;
                    }
                }
                else if (!actor.isSameSpecies(target))
                {
                    continue;
                }

                if (needsOppositeSex &&
                    actor.canFallInLoveWith(target))
                {
                    bestTargets.Add(target);
                    stopSearch = true;
                    break;
                }

                normalTargets.Add(target);
                if (normalTargets.Count > 3)
                {
                    stopSearch = true;
                    break;
                }
            }
        }

        Actor result = null;
        if (bestTargets.Count > 0)
        {
            result = bestTargets[
                Randy.rnd.Next(0, bestTargets.Count)];
        }
        else if (normalTargets.Count > 0)
        {
            result = normalTargets[
                Randy.rnd.Next(0, normalTargets.Count)];
        }

        bestTargets.Clear();
        normalTargets.Clear();
        return result;
    }

    private static void AddTelepathicSocializeTargets(
        Actor actor,
        List<Actor> bestTargets,
        List<Actor> normalTargets)
    {
        if (actor.hasFamily())
        {
            List<Actor> units = actor.family.units;
            int count = units.Count;
            for (int i = 0; i < count; i++)
            {
                Actor target = units[i];
                if (actor.canTalkWith(target))
                {
                    normalTargets.Add(target);
                }
            }
        }

        AddTelepathicParent(
            actor,
            actor.data.parent_id_1,
            bestTargets);
        AddTelepathicParent(
            actor,
            actor.data.parent_id_2,
            bestTargets);
    }

    private static void AddTelepathicParent(
        Actor actor,
        long parentId,
        List<Actor> bestTargets)
    {
        Actor parent = World.world.units.get(parentId);
        if (parent != null &&
            parent.isAlive() &&
            actor.canTalkWith(parent))
        {
            bestTargets.Add(parent);
        }
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

}
