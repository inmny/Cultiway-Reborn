using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Extensions;

public sealed class SectResidencePlan
{
    public WorldTile Tile;
    public readonly List<TileZone> Zones = new();
    public float TotalScore;
    public float WakanScore;
    public float TerrainScore;
    public float CityDistanceScore;
    public float BuildSpaceScore;
    public float SectDistanceScore;
    public int BuildSiteCount;
    public GeoRegion PrimaryRegion;
    public GeoRegion LandformRegion;
}

public static class SectResidencePlanner
{
    public static bool HasFoundingSite(Actor founder)
    {
        return TryFindFoundingSite(founder, out _);
    }

    public static bool TryFindFoundingSite(Actor founder, out SectResidencePlan plan)
    {
        return TryFindFoundingSite(null, founder, out plan);
    }

    public static bool TryFindFoundingSite(Sect sect, Actor founder, out SectResidencePlan plan)
    {
        plan = null;
        WorldTile origin = founder?.current_tile;
        TileZone originZone = origin?.zone;
        if (origin == null || originZone == null) return false;

        List<SectResidencePlan> plans = new();
        int radius = SectConst.ResidenceFoundingSearchZoneRadius;
        for (int x = originZone.x - radius; x <= originZone.x + radius; x++)
        {
            for (int y = originZone.y - radius; y <= originZone.y + radius; y++)
            {
                TileZone zone = World.world.zone_calculator.getZone(x, y);
                SectResidencePlan candidate = CreatePlan(sect, zone);
                if (candidate != null && candidate.TotalScore >= SectConst.ResidenceMinSiteScore)
                {
                    plans.Add(candidate);
                }
            }
        }

        if (plans.Count == 0) return false;

        plans.Sort((left, right) => right.TotalScore.CompareTo(left.TotalScore));
        int topCount = Mathf.Min(5, plans.Count);
        plan = plans[Randy.randomInt(0, topCount)];
        return true;
    }

    public static void ApplyResidencePlan(Sect sect, Actor founder, SectResidencePlan plan)
    {
        sect.data.ResidenceZones = new List<ZoneData>();
        sect.data.ResidenceTileID = plan.Tile.data.tile_id;
        sect.data.ResidenceFoundedTime = (float)World.world.getCurWorldTime();
        sect.data.ResidenceName = ResolveResidenceName(founder, plan);
        sect.data.ResidenceSiteScore = plan.TotalScore;
        sect.data.ResidenceWakanScore = plan.WakanScore;
        sect.data.ResidenceTerrainScore = plan.TerrainScore;
        sect.data.ResidenceCityDistanceScore = plan.CityDistanceScore;
        sect.data.ResidenceBuildSpaceScore = plan.BuildSpaceScore;
        sect.data.ResidenceGeoRegionID = plan.LandformRegion?.getID() ?? plan.PrimaryRegion?.getID() ?? -1;
        sect.data.ResidenceGeoRegionName = plan.LandformRegion?.name ?? plan.PrimaryRegion?.name;

        for (int i = 0; i < plan.Zones.Count; i++)
        {
            TileZone zone = plan.Zones[i];
            sect.data.ResidenceZones.Add(new ZoneData
            {
                x = zone.x,
                y = zone.y
            });
        }

        WorldboxGame.I?.Sects?.setDirtyResidenceZones();
        SectVerifyLog.Log(
            "ResidenceSetup",
            $"sect={SectVerifyLog.Sect(sect)} founder={SectVerifyLog.Actor(founder)} tile={plan.Tile.x},{plan.Tile.y} zone={plan.Tile.zone?.id ?? -1} zones={plan.Zones.Count} score={plan.TotalScore:F1} wakan={plan.WakanScore:F1} terrain={plan.TerrainScore:F1} city={plan.CityDistanceScore:F1} build={plan.BuildSpaceScore:F1} name={sect.data.ResidenceName}");
    }

    public static List<TileZone> GetResidenceZones(Sect sect)
    {
        List<TileZone> zones = new();
        if (sect.data.ResidenceZones != null && sect.data.ResidenceZones.Count > 0)
        {
            for (int i = 0; i < sect.data.ResidenceZones.Count; i++)
            {
                ZoneData zoneData = sect.data.ResidenceZones[i];
                AddZone(zones, World.world.zone_calculator.getZone(zoneData.x, zoneData.y));
            }
        }
        else
        {
            AddLegacyResidenceZones(zones, sect);
        }

        return zones;
    }

    public static WorldTile FindBuildTile(Sect sect, BuildingAsset building)
    {
        List<TileZone> zones = GetResidenceZones(sect);
        for (int i = 0; i < zones.Count; i++)
        {
            TileZone zone = zones[i];
            if (zone == null || zone.tiles.Length == 0) continue;

            if (CanBuildOnTile(zone.centerTile, building, zones))
            {
                return zone.centerTile;
            }

            for (int j = 0; j < zone.tiles.Length; j++)
            {
                WorldTile tile = zone.tiles[j];
                if (CanBuildOnTile(tile, building, zones))
                {
                    return tile;
                }
            }
        }

        return null;
    }

    private static SectResidencePlan CreatePlan(Sect sect, TileZone center)
    {
        if (!CanUseCenterZone(center, sect)) return null;

        SectResidencePlan plan = new()
        {
            Tile = FindResidenceCenterTile(center),
            PrimaryRegion = center.centerTile.GetExtend().GetGeoRegion(GeoRegionLayer.Primary),
            LandformRegion = center.centerTile.GetExtend().GetGeoRegion(GeoRegionLayer.Landform)
        };
        if (!TryCollectResidenceZones(plan.Zones, center, sect)) return null;

        plan.BuildSiteCount = CountBuildSites(plan.Zones, Buildings.SectHall);
        if (plan.BuildSiteCount < SectConst.ResidenceMinBuildSites) return null;

        plan.WakanScore = EvaluateWakan(plan.Zones);
        plan.TerrainScore = EvaluateTerrain(center, plan);
        plan.CityDistanceScore = EvaluateCityDistance(plan.Tile);
        plan.BuildSpaceScore = Mathf.Min(plan.BuildSiteCount, 6) * 10f;
        plan.SectDistanceScore = EvaluateSectDistance(plan.Tile);
        plan.TotalScore = plan.WakanScore
                          + plan.TerrainScore
                          + plan.CityDistanceScore
                          + plan.BuildSpaceScore
                          + plan.SectDistanceScore;
        return plan;
    }

    private static bool CanUseCenterZone(TileZone zone, Sect ignoredSect)
    {
        return zone != null
               && zone.centerTile != null
               && zone.tiles_with_ground > 0
               && !zone.hasCity()
               && !zone.hasLava()
               && !WorldboxGame.I.Sects.IsSectResidenceZone(zone, ignoredSect);
    }

    private static bool CanReserveZone(TileZone zone, Sect ignoredSect)
    {
        return zone != null
               && zone.centerTile != null
               && zone.tiles_with_ground > 0
               && !zone.hasCity()
               && !zone.hasLava()
               && !WorldboxGame.I.Sects.IsSectResidenceZone(zone, ignoredSect);
    }

    private static bool TryCollectResidenceZones(List<TileZone> zones, TileZone center, Sect ignoredSect)
    {
        int radius = SectConst.ResidenceInitialZoneRadius;
        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int y = center.y - radius; y <= center.y + radius; y++)
            {
                TileZone zone = World.world.zone_calculator.getZone(x, y);
                if (WorldboxGame.I.Sects.IsSectResidenceZone(zone, ignoredSect)) return false;
                AddZoneIfReservable(zones, zone, ignoredSect);
            }
        }

        if (!zones.Contains(center))
        {
            zones.Insert(0, center);
        }

        return zones.Count > 0;
    }

    private static WorldTile FindResidenceCenterTile(TileZone zone)
    {
        WorldTile best = zone.centerTile;
        float bestScore = float.MinValue;
        for (int i = 0; i < zone.tiles.Length; i++)
        {
            WorldTile tile = zone.tiles[i];
            if (tile == null || !tile.Type.ground) continue;

            float score = GetWakan(tile) - GetDirtyWakan(tile) * 0.25f;
            if (score > bestScore)
            {
                bestScore = score;
                best = tile;
            }
        }

        return best;
    }

    private static int CountBuildSites(List<TileZone> zones, BuildingAsset building)
    {
        if (building == null) return 0;

        int count = 0;
        for (int i = 0; i < zones.Count; i++)
        {
            TileZone zone = zones[i];
            if (zone == null) continue;

            if (CanBuildOnTile(zone.centerTile, building, zones))
            {
                count++;
            }

            for (int j = 0; j < zone.tiles.Length; j++)
            {
                WorldTile tile = zone.tiles[j];
                if (tile == zone.centerTile) continue;
                if (!CanBuildOnTile(tile, building, zones)) continue;

                count++;
                if (count >= 6) return count;
            }
        }

        return count;
    }

    private static bool CanBuildOnTile(WorldTile tile, BuildingAsset building, List<TileZone> residenceZones)
    {
        return tile != null
               && IsFootprintInsideZones(tile, building, residenceZones)
               && World.world.buildings.canBuildFrom(tile, building, null);
    }

    private static bool IsFootprintInsideZones(WorldTile tile, BuildingAsset building, List<TileZone> residenceZones)
    {
        BuildingFundament fundament = building.fundament;
        int startX = tile.x - fundament.left;
        int startY = tile.y - fundament.bottom;
        for (int x = 0; x < fundament.width; x++)
        {
            for (int y = 0; y < fundament.height; y++)
            {
                WorldTile part = World.world.GetTile(startX + x, startY + y);
                if (part == null || !residenceZones.Contains(part.zone)) return false;
            }
        }

        return true;
    }

    private static float EvaluateWakan(List<TileZone> zones)
    {
        float wakan = 0f;
        float dirty = 0f;
        float peak = 0f;
        int count = 0;
        for (int i = 0; i < zones.Count; i++)
        {
            TileZone zone = zones[i];
            for (int j = 0; j < zone.tiles.Length; j++)
            {
                WorldTile tile = zone.tiles[j];
                if (tile == null) continue;

                float tileWakan = GetWakan(tile);
                wakan += tileWakan;
                dirty += GetDirtyWakan(tile);
                peak = Mathf.Max(peak, tileWakan);
                count++;
            }
        }

        if (count == 0) return 0f;

        float avgWakan = wakan / count;
        float avgDirty = dirty / count;
        return (avgWakan - SectConst.ResidenceWakanScale) / 8f
               + (peak - SectConst.ResidenceWakanScale) / 20f
               - (avgDirty - SectConst.ResidenceDirtyWakanScale) / 12f;
    }

    private static float EvaluateTerrain(TileZone center, SectResidencePlan plan)
    {
        int ground = 0;
        int liquid = 0;
        int mountain = 0;
        TileZone[] zones = center.neighbours_all;
        for (int i = 0; i < center.tiles.Length; i++)
        {
            CountTerrain(center.tiles[i], ref ground, ref liquid, ref mountain);
        }

        for (int i = 0; i < zones.Length; i++)
        {
            TileZone zone = zones[i];
            if (zone == null) continue;

            for (int j = 0; j < zone.tiles.Length; j++)
            {
                CountTerrain(zone.tiles[j], ref ground, ref liquid, ref mountain);
            }
        }

        float total = Mathf.Max(1, ground + liquid);
        float mountainRatio = mountain / total;
        float liquidRatio = liquid / total;
        float score = mountainRatio * 55f - liquidRatio * 25f;

        GeoRegionLibrary library = ModClass.L.GeoRegionLibrary;
        if (MatchesCategory(plan.LandformRegion, library.LandformMountain)) score += 35f;
        if (MatchesCategory(plan.LandformRegion, library.LandformCanyon)) score += 12f;
        if (MatchesCategory(plan.LandformRegion, library.LandformBasin)) score += 10f;
        if (MatchesCategory(plan.PrimaryRegion, library.PrimaryHighlands)) score += 18f;
        if (MatchesCategory(plan.PrimaryRegion, library.PrimaryMountains)) score += 15f;

        return score;
    }

    private static void CountTerrain(WorldTile tile, ref int ground, ref int liquid, ref int mountain)
    {
        if (tile == null) return;

        TileTypeBase type = tile.Type;
        if (type.ground) ground++;
        if (type.liquid) liquid++;
        if (type.mountains || type.edge_mountains || type.summit || type.block) mountain++;
    }

    private static float EvaluateCityDistance(WorldTile tile)
    {
        float nearest = float.MaxValue;
        foreach (City city in World.world.cities)
        {
            if (city == null || city.isRekt()) continue;

            WorldTile cityTile = city.getTile();
            if (cityTile == null) continue;

            nearest = Mathf.Min(nearest, Toolbox.DistTile(tile, cityTile));
        }

        if (nearest == float.MaxValue) return 0f;
        if (nearest < 24f) return -120f + nearest * 3f;
        if (nearest <= 80f) return 40f;
        if (nearest <= 160f) return 25f;
        if (nearest <= 260f) return 10f;
        return -20f;
    }

    private static float EvaluateSectDistance(WorldTile tile)
    {
        float nearest = float.MaxValue;
        foreach (Sect sect in WorldboxGame.I.Sects)
        {
            if (sect == null || sect.isRekt()) continue;

            WorldTile residence = sect.GetResidenceTile();
            if (residence == null) continue;

            nearest = Mathf.Min(nearest, Toolbox.DistTile(tile, residence));
        }

        if (nearest == float.MaxValue) return 0f;
        if (nearest < 48f) return -90f;
        if (nearest < 96f) return -35f;
        if (nearest < 160f) return 8f;
        return 0f;
    }

    private static bool MatchesCategory(GeoRegion region, GeoRegionAsset asset)
    {
        return region?.data != null
               && asset != null
               && region.data.CategoryId == asset.id;
    }

    private static float GetWakan(WorldTile tile)
    {
        return WakanMap.I.map[tile.x, tile.y];
    }

    private static float GetDirtyWakan(WorldTile tile)
    {
        return DirtyWakanMap.I.map[tile.x, tile.y];
    }

    private static void AddLegacyResidenceZones(List<TileZone> zones, Sect sect)
    {
        if (sect.data.ResidenceTileID < 0) return;

        TileZone center = sect.GetResidenceZone();
        AddZone(zones, center);
        TileZone[] neighbours = center?.neighbours_all;
        if (neighbours == null) return;

        for (int i = 0; i < neighbours.Length; i++)
        {
            AddZone(zones, neighbours[i]);
        }
    }

    private static void AddZoneIfReservable(List<TileZone> zones, TileZone zone, Sect ignoredSect)
    {
        if (CanReserveZone(zone, ignoredSect))
        {
            AddZone(zones, zone);
        }
    }

    private static void AddZone(List<TileZone> zones, TileZone zone)
    {
        if (zone != null && !zones.Contains(zone))
        {
            zones.Add(zone);
        }
    }

    private static string ResolveResidenceName(Actor founder, SectResidencePlan plan)
    {
        if (!string.IsNullOrEmpty(plan.LandformRegion?.name)) return plan.LandformRegion.name;
        if (!string.IsNullOrEmpty(plan.PrimaryRegion?.name)) return plan.PrimaryRegion.name;
        if (founder.hasCity()) return founder.city.name + "外山门";
        return $"{plan.Tile.x}, {plan.Tile.y}";
    }
}
