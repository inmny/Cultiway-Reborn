using System;
using Cultiway.Core.SubWorlds.Generation;
using Cultiway.Core.SubWorlds.Model;
using Cultiway.Core.SubWorlds.Objects;

namespace Cultiway.Content.SubWorlds.Ruins.Generation;

/// <summary>生成 48x48 残破古修道场及其初始遗构。</summary>
public sealed class RuinedDaoGroundGeneratorAsset : SubWorldGeneratorAsset
{
    internal const int MapWidth = 48;
    internal const int MapHeight = 48;

    private const string SoilLow = "soil_low";
    private const string SoilHigh = "soil_high";
    private const string Hills = "hills";
    private const string Mountains = "mountains";
    private const string AncientWall = "wall_ancient";

    internal override SubWorldGeneratedScene Generate(
        SubWorldTemplateAsset template,
        int seed,
        SubWorldAnchor anchor,
        SubWorldCreationParameters parameters)
    {
        var random = new Random(seed);
        var tiles = new SubWorldTile[MapWidth * MapHeight];
        BuildTerrain(tiles, random);

        int entryTileIndex = Index(MapWidth / 2, 2);
        tiles[entryTileIndex] = new SubWorldTile(SoilLow);
        SubWorldBuildingPlacement[] buildings = BuildBuildings(tiles, random);

        return new SubWorldGeneratedScene(
            new SubWorldMapData
            {
                Width = MapWidth,
                Height = MapHeight,
                Tiles = tiles,
                EntryTileIndices = [entryTileIndex],
                ExitTileIndices = [entryTileIndex]
            },
            [
                new SubWorldSpawnPoint(SubWorldSpawnPointNames.Entry, entryTileIndex),
                new SubWorldSpawnPoint(SubWorldSpawnPointNames.Exit, entryTileIndex)
            ],
            buildingPlacements: buildings);
    }

    private static void BuildTerrain(SubWorldTile[] tiles, Random random)
    {
        for (int y = 0; y < MapHeight; y++)
        for (int x = 0; x < MapWidth; x++)
        {
            string main = y >= 37 ? Hills : y >= 25 ? SoilHigh : SoilLow;
            tiles[Index(x, y)] = new SubWorldTile(main);
        }

        for (int x = 0; x < MapWidth; x++)
        {
            tiles[Index(x, 0)] = new SubWorldTile(Mountains);
            tiles[Index(x, MapHeight - 1)] = new SubWorldTile(Mountains);
        }
        for (int y = 1; y < MapHeight - 1; y++)
        {
            tiles[Index(0, y)] = new SubWorldTile(Mountains);
            tiles[Index(MapWidth - 1, y)] = new SubWorldTile(Mountains);
        }

        int collapseMask = random.Next(3);
        int leftGap = 12 + collapseMask;
        int rightGap = 34 - collapseMask;
        for (int x = 4; x <= 43; x++)
        {
            if (Math.Abs(x - leftGap) <= 1 || Math.Abs(x - rightGap) <= 1) continue;
            SetWall(tiles, x, 17);
        }

        int upperGap = 23 + random.Next(-1, 2);
        for (int x = 8; x <= 40; x++)
        {
            if (Math.Abs(x - 10) <= 1 || Math.Abs(x - upperGap) <= 1 || Math.Abs(x - 38) <= 1) continue;
            SetWall(tiles, x, 33);
        }

        (int x0, int y0, int x1, int y1)[] fragments = collapseMask switch
        {
            0 => [(6, 10, 6, 15), (28, 21, 34, 21), (39, 27, 43, 27)],
            1 => [(5, 12, 10, 12), (18, 20, 18, 25), (35, 29, 42, 29)],
            _ => [(7, 8, 12, 8), (29, 19, 29, 24), (36, 31, 42, 31)]
        };
        for (int i = 0; i < fragments.Length; i++)
        {
            (int x0, int y0, int x1, int y1) line = fragments[i];
            PlaceWallLine(tiles, line.x0, line.y0, line.x1, line.y1);
        }

        for (int i = 0; i < 22; i++)
        {
            int x = 5 + random.Next(MapWidth - 10);
            int y = 5 + random.Next(MapHeight - 10);
            if (y == 17 || y == 33 || Math.Abs(x - MapWidth / 2) < 3) continue;
            tiles[Index(x, y)] = new SubWorldTile(y >= 25 ? SoilHigh : SoilLow);
        }
    }

    private static SubWorldBuildingPlacement[] BuildBuildings(SubWorldTile[] tiles, Random random)
    {
        (int x, int y) stele = Pick(random, (14, 12), (17, 13), (12, 14));
        (int x, int y) herb = Pick(random, (9, 22), (12, 23), (8, 25));
        (int x, int y) formationEye = Pick(random, (23, 26), (26, 27), (24, 25));
        (int x, int y) altar = Pick(random, (33, 40), (36, 39), (31, 41));
        (int x, int y) hall = Pick(random, (22, 35), (27, 36), (19, 38));

        return
        [
            PlaceBuilding(tiles, new LocalObjectId(1), "statue", stele, random.Next(12), SubWorldVisualState.Ruin),
            PlaceBuilding(tiles, new LocalObjectId(2), "fruit_bush", herb, random.Next(2)),
            PlaceBuilding(tiles, new LocalObjectId(3), "monolith", formationEye, 0, SubWorldVisualState.Ruin),
            PlaceBuilding(tiles, new LocalObjectId(4), "temple_human", altar, 0, SubWorldVisualState.Ruin),
            PlaceBuilding(tiles, new LocalObjectId(5), $"hall_human_{random.Next(3)}", hall, 0,
                SubWorldVisualState.Ruin)
        ];
    }

    private static SubWorldBuildingPlacement PlaceBuilding(
        SubWorldTile[] tiles,
        LocalObjectId localObjectId,
        string buildingAssetId,
        (int x, int y) anchor,
        int visualVariantIndex,
        SubWorldVisualState visualState = SubWorldVisualState.Default)
    {
        BuildingAsset asset = AssetManager.buildings.get(buildingAssetId);
        SubWorldBuildingBounds bounds = SubWorldBuildingGeometry.GetBounds(anchor.x, anchor.y, asset.fundament);
        for (int y = bounds.MinY; y <= bounds.MaxY; y++)
        for (int x = bounds.MinX; x <= bounds.MaxX; x++)
        {
            int tileIndex = Index(x, y);
            tiles[tileIndex] = new SubWorldTile(tiles[tileIndex].MainAssetId);
        }

        return new SubWorldBuildingPlacement(
            localObjectId,
            buildingAssetId,
            Index(anchor.x, anchor.y),
            visualVariantIndex,
            visualState);
    }

    private static void SetWall(SubWorldTile[] tiles, int x, int y)
    {
        tiles[Index(x, y)] = new SubWorldTile(y >= 25 ? SoilHigh : SoilLow, AncientWall);
    }

    private static void PlaceWallLine(SubWorldTile[] tiles, int x0, int y0, int x1, int y1)
    {
        int dx = Math.Sign(x1 - x0);
        int dy = Math.Sign(y1 - y0);
        int x = x0;
        int y = y0;
        while (true)
        {
            SetWall(tiles, x, y);
            if (x == x1 && y == y1) return;
            x += dx;
            y += dy;
        }
    }

    private static (int x, int y) Pick(
        Random random,
        (int x, int y) first,
        (int x, int y) second,
        (int x, int y) third)
    {
        return random.Next(3) switch
        {
            0 => first,
            1 => second,
            _ => third
        };
    }

    private static int Index(int x, int y) => y * MapWidth + x;
}
