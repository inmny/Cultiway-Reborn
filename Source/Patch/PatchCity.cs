using System.Collections.Generic;
using Cultiway.Content;
using Cultiway.Utils.Extension;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchCity
{
    private const float SkavenBlightSpawnChance = 0.13f;

    private sealed class DestroyedCityState
    {
        public WorldTile CenterTile;
        public List<TileZone> Zones;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(City), nameof(City.Dispose))]
    private static void Dispose_prefix(City __instance)
    {
        var ce = __instance.GetExtend();
        ce.Dispose();
    }

    [HarmonyPrefix, HarmonyPatch(typeof(CityManager), nameof(CityManager.removeObject))]
    private static void RemoveObject_prefix(City pObject, out DestroyedCityState __state)
    {
        __state = null;
        if (pObject == null || !Randy.randomChance(SkavenBlightSpawnChance)) return;

        __state = new DestroyedCityState
        {
            CenterTile = pObject.getTile(),
            Zones = new List<TileZone>(pObject.zones)
        };
    }

    [HarmonyPostfix, HarmonyPatch(typeof(CityManager), nameof(CityManager.removeObject))]
    private static void RemoveObject_postfix(DestroyedCityState __state)
    {
        if (__state == null) return;

        var site = FindSkavenBlightSite(__state);
        if (site != null)
        {
            World.world.buildings.addBuilding(Buildings.SkavenBlight, site);
        }
    }

    private static WorldTile FindSkavenBlightSite(DestroyedCityState state)
    {
        var building = Buildings.SkavenBlight;
        var site = state.CenterTile;
        if (CanBuildSkavenBlight(site, building)) return site;

        var bestDistance = int.MaxValue;
        site = null;
        foreach (var zone in state.Zones)
        {
            if (zone == null) continue;
            foreach (var tile in zone.tiles)
            {
                if (!CanBuildSkavenBlight(tile, building)) continue;

                var distance = state.CenterTile == null
                    ? 0
                    : Toolbox.SquaredDistTile(state.CenterTile, tile);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                site = tile;
            }
        }
        return site;
    }

    private static bool CanBuildSkavenBlight(WorldTile tile, BuildingAsset building)
    {
        return tile != null && building != null && World.world.buildings.canBuildFrom(tile, building, null);
    }
}
