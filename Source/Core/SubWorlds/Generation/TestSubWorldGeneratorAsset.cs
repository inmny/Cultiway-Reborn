using System;
using Cultiway.Core.SubWorlds.Model;

namespace Cultiway.Core.SubWorlds.Generation;

/// <summary>
/// 生成第一阶段使用的确定性测试地图、入口、出口和测试 Pawn 出生点。
/// </summary>
public sealed class TestSubWorldGeneratorAsset : SubWorldGeneratorAsset
{
    private const string GroundAssetId = "soil_low";
    private const string WallAssetId = "wall_ancient";

    /// <inheritdoc />
    internal override SubWorldGeneratedScene Generate(
        SubWorldTemplateAsset template,
        int seed,
        SubWorldAnchor anchor,
        SubWorldCreationParameters parameters)
    {
        int width = template.width;
        int height = template.height;
        int middleY = height / 2;
        int entryX = 1;
        int exitX = width - 2;
        var tiles = new SubWorldTile[checked(width * height)];

        for (int index = 0; index < tiles.Length; index++)
        {
            tiles[index] = new SubWorldTile(GroundAssetId);
        }

        for (int x = 0; x < width; x++)
        {
            SetWall(tiles, width, x, 0);
            SetWall(tiles, width, x, height - 1);
        }
        for (int y = 1; y < height - 1; y++)
        {
            SetWall(tiles, width, 0, y);
            SetWall(tiles, width, width - 1, y);
        }

        PlaceSeededObstacles(tiles, width, height, middleY, seed);

        int entryIndex = middleY * width + entryX;
        int exitIndex = middleY * width + exitX;
        tiles[entryIndex] = new SubWorldTile(GroundAssetId);
        tiles[exitIndex] = new SubWorldTile(GroundAssetId);

        var mapData = new SubWorldMapData
        {
            Width = width,
            Height = height,
            Tiles = tiles,
            EntryTileIndices = [entryIndex],
            ExitTileIndices = [exitIndex]
        };

        return new SubWorldGeneratedScene(mapData, entryIndex);
    }

    private static void PlaceSeededObstacles(SubWorldTile[] tiles, int width, int height, int middleY, int seed)
    {
        var random = new Random(seed);
        int obstacleCount = 6;

        for (int i = 0; i < obstacleCount; i++)
        {
            bool upper = random.Next(2) == 0;
            int obstacleWidth = 2 + random.Next(5);
            int obstacleHeight = 2 + random.Next(4);
            int xRange = width - obstacleWidth - 3;
            int x = 2 + random.Next(xRange);
            int yMin = upper ? 2 : middleY + 2;
            int yMax = upper ? middleY - obstacleHeight - 1 : height - obstacleHeight - 2;
            int y = yMin + random.Next(yMax - yMin + 1);

            for (int obstacleY = y; obstacleY < y + obstacleHeight; obstacleY++)
            {
                for (int obstacleX = x; obstacleX < x + obstacleWidth; obstacleX++)
                {
                    SetWall(tiles, width, obstacleX, obstacleY);
                }
            }
        }
    }

    private static void SetWall(SubWorldTile[] tiles, int width, int x, int y)
    {
        tiles[y * width + x] = new SubWorldTile(GroundAssetId, WallAssetId);
    }
}
