using System;
using System.Collections.Generic;
using Cultiway.Core.SubWorlds.Generation;
using Cultiway.Core.SubWorlds.Model;
using Cultiway.Core.SubWorlds.Objects;
using UnityEngine;
using Random = System.Random;

namespace Cultiway.Content.SubWorlds.Natural.Generation;

/// <summary>使用原版地形和生态地块生成一张自然小世界地图。</summary>
public sealed class NaturalWorldGeneratorAsset : SubWorldGeneratorAsset
{
    internal const int MapWidth = 128;
    internal const int MapHeight = 128;

    private const int BiomeRegionCount = 4;
    private const string DeepOcean = "deep_ocean";
    private const string CloseOcean = "close_ocean";
    private const string ShallowWaters = "shallow_waters";
    private const string Sand = "sand";
    private const string SoilLow = "soil_low";
    private const string SoilHigh = "soil_high";
    private const string Hills = "hills";
    private const string Mountains = "mountains";
    private const string Summit = "summit";
    private const int NaturalBuildingMinimum = 8;
    private const int NaturalBuildingMaximum = 256;
    private const int NaturalBuildingTilesPerBuilding = 64;
    private const int NaturalBuildingAttemptMultiplier = 16;
    private const int EntryBuildingClearRadius = 4;

    /// <summary>按原版模板名称创建隔离生成器使用的默认参数。</summary>
    internal static SubWorldGenerationSettings CreateDefaultSettings(string profileId)
    {
        var settings = new SubWorldGenerationSettings
        {
            perlin_scale_stage_1 = 5,
            perlin_scale_stage_2 = 5,
            perlin_scale_stage_3 = 5,
            main_perlin_noise_stage = true,
            perlin_noise_stage_2 = true,
            perlin_noise_stage_3 = true,
            random_shapes_amount = 5,
            random_biomes = true,
            add_center_gradient_land = true,
            gradient_round_edges = true,
            add_vegetation = true
        };

        switch (profileId)
        {
            case "continent":
                settings.ring_effect = true;
                break;
            case "box_world":
                settings.gradient_round_edges = false;
                settings.add_mountain_edges = true;
                settings.ring_effect = true;
                break;
            case "islands":
                settings.add_center_gradient_land = false;
                settings.gradient_round_edges = true;
                settings.ring_effect = true;
                break;
            case "boring_plains":
                settings.add_mountain_edges = true;
                settings.remove_mountains = true;
                settings.gradient_round_edges = false;
                settings.random_shapes_amount = 0;
                settings.low_ground = false;
                settings.high_ground = false;
                break;
            case "donut":
                settings.main_perlin_noise_stage = false;
                settings.perlin_noise_stage_2 = true;
                settings.perlin_noise_stage_3 = false;
                settings.random_shapes_amount = 0;
                settings.add_center_lake = true;
                settings.ring_effect = false;
                break;
            case "toast":
                settings.gradient_round_edges = false;
                settings.square_edges = true;
                settings.remove_mountains = true;
                break;
            case "pancake":
                settings.remove_mountains = true;
                settings.random_shapes_amount = 0;
                break;
            case "dormant_volcano":
                settings.main_perlin_noise_stage = false;
                settings.perlin_noise_stage_2 = true;
                settings.perlin_noise_stage_3 = false;
                settings.random_biomes = false;
                settings.random_shapes_amount = 0;
                settings.gradient_round_edges = true;
                settings.add_mountain_edges = false;
                break;
            case "cheese":
                settings.main_perlin_noise_stage = false;
                settings.perlin_noise_stage_2 = true;
                settings.perlin_noise_stage_3 = false;
                settings.random_shapes_amount = 0;
                settings.gradient_round_edges = false;
                settings.square_edges = true;
                settings.remove_mountains = true;
                break;
            case "bad_apple":
                settings.perlin_noise_stage_2 = false;
                settings.perlin_noise_stage_3 = false;
                settings.random_shapes_amount = 0;
                settings.gradient_round_edges = true;
                settings.high_ground = true;
                break;
            case "chaos_pearl":
                settings.perlin_noise_stage_2 = false;
                settings.perlin_noise_stage_3 = false;
                settings.low_ground = true;
                settings.random_shapes_amount = 0;
                break;
            case "lasagna":
                settings.perlin_noise_stage_2 = false;
                settings.perlin_noise_stage_3 = false;
                settings.gradient_round_edges = false;
                settings.square_edges = true;
                settings.low_ground = true;
                break;
            case "anthill":
                settings.main_perlin_noise_stage = false;
                settings.perlin_noise_stage_2 = false;
                settings.perlin_noise_stage_3 = false;
                settings.random_biomes = true;
                settings.add_center_gradient_land = false;
                settings.gradient_round_edges = false;
                settings.random_shapes_amount = 0;
                break;
            case "checkerboard":
                settings.main_perlin_noise_stage = false;
                settings.perlin_noise_stage_2 = false;
                settings.perlin_noise_stage_3 = false;
                settings.random_biomes = true;
                settings.add_mountain_edges = true;
                settings.remove_mountains = true;
                settings.add_center_gradient_land = false;
                settings.gradient_round_edges = false;
                settings.random_shapes_amount = 0;
                break;
            case "cubicles":
                settings.main_perlin_noise_stage = false;
                settings.perlin_noise_stage_2 = false;
                settings.perlin_noise_stage_3 = false;
                settings.random_biomes = true;
                settings.add_mountain_edges = true;
                settings.remove_mountains = true;
                settings.add_center_gradient_land = false;
                settings.gradient_round_edges = false;
                settings.cubicle_size = 2;
                settings.random_shapes_amount = 0;
                break;
            case "empty":
                settings.main_perlin_noise_stage = false;
                settings.perlin_noise_stage_2 = false;
                settings.perlin_noise_stage_3 = false;
                settings.random_biomes = false;
                settings.add_center_gradient_land = false;
                settings.gradient_round_edges = false;
                settings.random_shapes_amount = 0;
                settings.add_vegetation = false;
                break;
        }

        return settings;
    }

    internal override SubWorldGeneratedScene Generate(
        SubWorldTemplateAsset template,
        int seed,
        SubWorldAnchor anchor,
        SubWorldCreationParameters parameters)
    {
        int width = parameters.Width > 0 ? parameters.Width : template.width;
        int height = parameters.Height > 0 ? parameters.Height : template.height;
        string profileId = template.generation_profile_id ?? "continent";
        SubWorldGenerationSettings settings = parameters.Settings ?? template.generation_settings ??
            CreateDefaultSettings(profileId);
        settings = settings.Clone();
        settings.Clamp();

        var random = new Random(seed);
        NoiseOffsets offsets = CreateNoiseOffsets(random);
        ProfileData profile = CreateProfileData(profileId, random);
        RandomShape[] randomShapes = CreateRandomShapes(settings.random_shapes_amount, random);
        var tiles = new SubWorldTile[checked(width * height)];

        GenerateTerrain(tiles, width, height, profileId, settings, profile, randomShapes, offsets);
        ApplyBiomes(tiles, width, height, random, settings);

        int entryTileIndex = FindEntryTile(tiles, width, height);
        if (entryTileIndex < 0)
        {
            entryTileIndex = height / 2 * width + width / 2;
            tiles[entryTileIndex] = new SubWorldTile(SoilLow);
        }

        SubWorldBuildingPlacement[] buildingPlacements = settings.add_vegetation
            ? BuildNaturalBuildings(tiles, width, height, entryTileIndex, random)
            : Array.Empty<SubWorldBuildingPlacement>();

        return new SubWorldGeneratedScene(
            new SubWorldMapData
            {
                Width = width,
                Height = height,
                Tiles = tiles,
                EntryTileIndices = new[] { entryTileIndex },
                ExitTileIndices = new[] { entryTileIndex }
            },
            new[]
            {
                new SubWorldSpawnPoint(SubWorldSpawnPointNames.Entry, entryTileIndex),
                new SubWorldSpawnPoint(SubWorldSpawnPointNames.Exit, entryTileIndex)
            },
            buildingPlacements: buildingPlacements);
    }

    private static void GenerateTerrain(
        SubWorldTile[] tiles,
        int width,
        int height,
        string profileId,
        SubWorldGenerationSettings settings,
        ProfileData profile,
        RandomShape[] randomShapes,
        NoiseOffsets offsets)
    {
        float aspectX = width > height ? width / (float)height : 1f;
        float aspectY = height > width ? height / (float)width : 1f;
        float centerX = (width - 1) * 0.5f;
        float centerY = (height - 1) * 0.5f;
        float halfSize = Math.Min(width, height) * 0.5f;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float normalizedX = (x - centerX) / halfSize;
            float normalizedY = (y - centerY) / halfSize;
            float radialDistance = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
            float squareDistance = Mathf.Max(Mathf.Abs(normalizedX), Mathf.Abs(normalizedY));

            float primaryNoise = settings.main_perlin_noise_stage
                ? SampleNoise(x, y, width, height, offsets.MainX, offsets.MainY,
                    Mathf.Max(0.1f, settings.perlin_scale_stage_1), aspectX, aspectY)
                : 0.5f;
            float detailNoise = settings.perlin_noise_stage_2
                ? SampleNoise(x, y, width, height, offsets.DetailX, offsets.DetailY,
                    Mathf.Max(0.1f, settings.perlin_scale_stage_2 * 4f), aspectX, aspectY)
                : 0.5f;
            float fineNoise = settings.perlin_noise_stage_3
                ? SampleNoise(x, y, width, height, offsets.FineX, offsets.FineY,
                    Mathf.Max(0.1f, settings.perlin_scale_stage_3 * 10f), aspectX, aspectY)
                : 0.5f;

            float elevation = primaryNoise * 180f + detailNoise * 58f + fineNoise * 24f;
            elevation += GetProfileElevation(profileId, normalizedX, normalizedY,
                radialDistance, squareDistance, settings, profile);
            elevation += GetRandomShapeElevation(normalizedX, normalizedY, randomShapes);

            if (settings.add_center_gradient_land)
            {
                float centerStrength = Mathf.Clamp01((0.86f - radialDistance) / 0.42f);
                elevation += centerStrength * 42f;
            }

            if (settings.add_center_lake && radialDistance < 0.27f)
            {
                elevation -= (0.27f - radialDistance) / 0.27f * 155f;
            }

            if (settings.add_mountain_edges && radialDistance > 0.62f)
            {
                elevation += Mathf.Clamp01((radialDistance - 0.62f) / 0.35f) * 52f;
            }

            if (settings.ring_effect)
            {
                elevation += Mathf.Sin(radialDistance * Mathf.PI * 4f) * 18f;
            }

            if (settings.low_ground) elevation -= 26f;
            if (settings.high_ground) elevation += 26f;
            if (settings.remove_mountains) elevation = Mathf.Min(elevation, 194f);

            if (settings.square_edges)
            {
                if (squareDistance >= 0.96f) elevation = 0f;
                else if (squareDistance > 0.78f)
                    elevation *= (0.96f - squareDistance) / 0.18f;
            }
            else if (settings.gradient_round_edges)
            {
                if (radialDistance >= 0.99f) elevation = 0f;
                else if (radialDistance > 0.78f)
                    elevation *= (0.99f - radialDistance) / 0.21f;
            }

            int heightValue = Mathf.Clamp(Mathf.RoundToInt(elevation), 0, 255);
            tiles[y * width + x] = new SubWorldTile(GetTerrainAssetId(heightValue));
        }
    }

    private static float GetProfileElevation(
        string profileId,
        float x,
        float y,
        float radialDistance,
        float squareDistance,
        SubWorldGenerationSettings settings,
        ProfileData profile)
    {
        switch (profileId)
        {
            case "box_world":
                return (1f - Mathf.Clamp01(squareDistance / 0.92f)) * 72f - 24f;
            case "islands":
                return (GetIslandStrength(x, y, profile.Islands) - 0.42f) * 190f - 20f;
            case "boring_plains":
                return -66f;
            case "donut":
                return (1f - Mathf.Clamp01(Mathf.Abs(radialDistance - 0.58f) / 0.22f)) * 135f - 44f;
            case "toast":
                return (1f - Mathf.Clamp01(squareDistance / 0.82f)) * 150f - 45f;
            case "pancake":
                return (1f - Mathf.Clamp01(radialDistance / 0.78f)) * 155f - 50f;
            case "dormant_volcano":
                return (1f - Mathf.Clamp01(radialDistance / 0.88f)) * 105f +
                       (1f - Mathf.Clamp01(radialDistance / 0.34f)) * 100f - 35f;
            case "cheese":
                return (1f - Mathf.Clamp01(radialDistance / 0.9f)) * 135f -
                       GetHoleStrength(x, y, profile.Holes) * 190f - 28f;
            case "bad_apple":
                return (1f - Mathf.Clamp01(radialDistance / (0.78f + Mathf.Sin(Mathf.Atan2(y, x) * 3f) * 0.08f))) *
                       170f - 54f;
            case "chaos_pearl":
                return (1f - Mathf.Clamp01(radialDistance / 0.92f)) * 125f +
                       Mathf.Sin(x * 8f + y * 3f) * 20f - 35f;
            case "lasagna":
                return Mathf.Sin((y + 1f) * Mathf.PI * 4f) * 65f - 22f;
            case "anthill":
                return Mathf.Sin(radialDistance * Mathf.PI * 8f) * 34f +
                       (1f - Mathf.Clamp01(radialDistance / 0.88f)) * 110f - 36f;
            case "checkerboard":
            {
                int cellX = Mathf.FloorToInt((x + 1f) * 4f);
                int cellY = Mathf.FloorToInt((y + 1f) * 4f);
                return ((cellX + cellY) & 1) == 0 ? 82f : -128f;
            }
            case "cubicles":
            {
                int cellCount = Mathf.Clamp(Mathf.RoundToInt(16f / Mathf.Max(2, settings.cubicle_size)), 2, 8);
                int cellX = Mathf.FloorToInt((x + 1f) * cellCount);
                int cellY = Mathf.FloorToInt((y + 1f) * cellCount);
                return ((cellX + cellY) & 1) == 0 ? 72f : -118f;
            }
            case "empty":
                return -170f;
            default:
                return 0f;
        }
    }

    private static float GetIslandStrength(float x, float y, Island[] islands)
    {
        float strength = 0f;
        for (int i = 0; i < islands.Length; i++)
        {
            float dx = x - islands[i].X;
            float dy = y - islands[i].Y;
            float radius = islands[i].Radius;
            strength = Mathf.Max(strength, Mathf.Exp(-(dx * dx + dy * dy) / (radius * radius)));
        }

        return strength;
    }

    private static float GetHoleStrength(float x, float y, Hole[] holes)
    {
        float strength = 0f;
        for (int i = 0; i < holes.Length; i++)
        {
            float dx = x - holes[i].X;
            float dy = y - holes[i].Y;
            float radius = holes[i].Radius;
            strength = Mathf.Max(strength, Mathf.Exp(-(dx * dx + dy * dy) / (radius * radius)));
        }

        return strength;
    }

    private static float GetRandomShapeElevation(float x, float y, RandomShape[] shapes)
    {
        float elevation = 0f;
        for (int i = 0; i < shapes.Length; i++)
        {
            float dx = x - shapes[i].X;
            float dy = y - shapes[i].Y;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            if (distance >= shapes[i].Radius) continue;
            float falloff = 1f - distance / shapes[i].Radius;
            elevation += shapes[i].Strength * falloff;
        }

        return elevation;
    }

    private static NoiseOffsets CreateNoiseOffsets(Random random)
    {
        return new NoiseOffsets(
            random.Next(1_000_000), random.Next(1_000_000),
            random.Next(1_000_000), random.Next(1_000_000),
            random.Next(1_000_000), random.Next(1_000_000));
    }

    private static ProfileData CreateProfileData(string profileId, Random random)
    {
        if (profileId == "islands")
        {
            var islands = new Island[6];
            for (int i = 0; i < islands.Length; i++)
            {
                islands[i] = new Island(
                    (float)(random.NextDouble() * 1.25d - 0.625d),
                    (float)(random.NextDouble() * 1.25d - 0.625d),
                    0.18f + (float)random.NextDouble() * 0.15f);
            }
            return new ProfileData(islands, Array.Empty<Hole>());
        }

        if (profileId == "cheese")
        {
            var holes = new Hole[6];
            for (int i = 0; i < holes.Length; i++)
            {
                holes[i] = new Hole(
                    (float)(random.NextDouble() * 1.2d - 0.6d),
                    (float)(random.NextDouble() * 1.2d - 0.6d),
                    0.08f + (float)random.NextDouble() * 0.09f);
            }
            return new ProfileData(Array.Empty<Island>(), holes);
        }

        return new ProfileData(Array.Empty<Island>(), Array.Empty<Hole>());
    }

    private static RandomShape[] CreateRandomShapes(int count, Random random)
    {
        var shapes = new RandomShape[count];
        for (int i = 0; i < count; i++)
        {
            float strength = (i & 1) == 0 ? 24f : -20f;
            shapes[i] = new RandomShape(
                (float)(random.NextDouble() * 1.6d - 0.8d),
                (float)(random.NextDouble() * 1.6d - 0.8d),
                0.04f + (float)random.NextDouble() * 0.12f,
                strength + (float)random.NextDouble() * 20f);
        }

        return shapes;
    }

    private static float SampleNoise(
        int x,
        int y,
        int width,
        int height,
        float offsetX,
        float offsetY,
        float scale,
        float aspectX,
        float aspectY)
    {
        float sampleX = (offsetX + x) / width * scale * aspectX;
        float sampleY = (offsetY + y) / height * scale * aspectY;
        return Mathf.PerlinNoise(sampleX, sampleY);
    }

    private static string GetTerrainAssetId(int height)
    {
        if (height >= 230) return Summit;
        if (height >= 210) return Mountains;
        if (height >= 199) return Hills;
        if (height >= 128) return SoilHigh;
        if (height >= 108) return SoilLow;
        if (height >= 98) return Sand;
        if (height >= 70) return ShallowWaters;
        if (height >= 30) return CloseOcean;
        return DeepOcean;
    }

    private static void ApplyBiomes(
        SubWorldTile[] tiles,
        int width,
        int height,
        Random random,
        SubWorldGenerationSettings settings)
    {
        List<BiomeAsset> candidates = GetBiomeCandidates();
        if (candidates.Count == 0) return;

        BiomeRegion[] regions = settings.random_biomes
            ? CreateBiomeRegions(width, height, random, candidates)
            : new[] { new BiomeRegion(FindGrassBiome(candidates), (width - 1) * 0.5f, (height - 1) * 0.5f) };
        if (regions.Length == 0 || regions[0].Biome == null) return;

        float warpOffsetX = random.Next(1_000_000);
        float warpOffsetY = random.Next(1_000_000);
        float warpStrength = Math.Min(width, height) * 0.12f;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int index = y * width + x;
            string mainAssetId = tiles[index].MainAssetId;
            if (mainAssetId != SoilLow && mainAssetId != SoilHigh) continue;

            float warpedX = x + (Mathf.PerlinNoise((warpOffsetX + x) / width * 2.5f,
                (warpOffsetY + y) / height * 2.5f) - 0.5f) * warpStrength;
            float warpedY = y + (Mathf.PerlinNoise((warpOffsetY + x) / width * 2.5f,
                (warpOffsetX + y) / height * 2.5f) - 0.5f) * warpStrength;
            BiomeAsset biome = FindNearestBiome(regions, warpedX, warpedY);
            if (biome == null || string.IsNullOrEmpty(biome.tile_low) || string.IsNullOrEmpty(biome.tile_high)) continue;
            string topAssetId = mainAssetId == SoilLow ? biome.tile_low : biome.tile_high;
            tiles[index] = new SubWorldTile(mainAssetId, topAssetId);
        }
    }

    private static BiomeAsset FindGrassBiome(List<BiomeAsset> candidates)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].id == "biome_grass") return candidates[i];
        }

        return candidates[0];
    }

    private static BiomeRegion[] CreateBiomeRegions(
        int width,
        int height,
        Random random,
        List<BiomeAsset> candidates)
    {
        int count = Math.Min(BiomeRegionCount, candidates.Count);
        var regions = new BiomeRegion[count];
        float centerX = (width - 1) * 0.5f;
        float centerY = (height - 1) * 0.5f;
        float radiusBase = Math.Min(width, height) * 0.25f;

        for (int i = 0; i < count; i++)
        {
            BiomeAsset biome = TakeWeightedBiome(candidates, random);
            double angle = Math.PI * 2d * i / count + (random.NextDouble() - 0.5d) * 0.7d;
            float radius = radiusBase * (0.7f + (float)random.NextDouble() * 0.6f);
            regions[i] = new BiomeRegion(
                biome,
                centerX + Mathf.Cos((float)angle) * radius,
                centerY + Mathf.Sin((float)angle) * radius);
        }

        return regions;
    }

    private static List<BiomeAsset> GetBiomeCandidates()
    {
        var candidates = new List<BiomeAsset>();
        for (int i = 0; i < AssetManager.biome_library.list.Count; i++)
        {
            BiomeAsset biome = AssetManager.biome_library.list[i];
            if (biome.generator_pot_amount <= 0 ||
                biome.generator_max_size != 0 ||
                string.IsNullOrEmpty(biome.tile_low) ||
                string.IsNullOrEmpty(biome.tile_high) ||
                AssetManager.top_tiles.get(biome.tile_low) == null ||
                AssetManager.top_tiles.get(biome.tile_high) == null)
            {
                continue;
            }
            candidates.Add(biome);
        }

        candidates.Sort((left, right) => string.CompareOrdinal(left.id, right.id));
        return candidates;
    }

    private static BiomeAsset TakeWeightedBiome(List<BiomeAsset> candidates, Random random)
    {
        int totalWeight = 0;
        for (int i = 0; i < candidates.Count; i++) totalWeight += candidates[i].generator_pot_amount;

        int roll = random.Next(totalWeight);
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= candidates[i].generator_pot_amount;
            if (roll >= 0) continue;
            BiomeAsset selected = candidates[i];
            candidates.RemoveAt(i);
            return selected;
        }

        throw new InvalidOperationException("无法选择自然小世界生态地块");
    }

    private static BiomeAsset FindNearestBiome(BiomeRegion[] regions, float x, float y)
    {
        int nearestIndex = 0;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < regions.Length; i++)
        {
            float dx = x - regions[i].X;
            float dy = y - regions[i].Y;
            float distance = dx * dx + dy * dy;
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearestIndex = i;
        }

        return regions[nearestIndex].Biome;
    }

    private static SubWorldBuildingPlacement[] BuildNaturalBuildings(
        SubWorldTile[] tiles,
        int width,
        int height,
        int entryTileIndex,
        Random random)
    {
        int naturalGroundCount = CountNaturalBuildingGround(tiles);
        if (naturalGroundCount == 0) return Array.Empty<SubWorldBuildingPlacement>();

        int targetCount = Math.Min(
            NaturalBuildingMaximum,
            Math.Max(NaturalBuildingMinimum, naturalGroundCount / NaturalBuildingTilesPerBuilding));
        var occupied = new bool[tiles.Length];
        var placements = new List<SubWorldBuildingPlacement>(targetCount);
        int attemptCount = targetCount * NaturalBuildingAttemptMultiplier;

        for (int attempt = 0; attempt < attemptCount && placements.Count < targetCount; attempt++)
        {
            int anchorTileIndex = random.Next(tiles.Length);
            BiomeAsset biome = GetNaturalBiome(tiles[anchorTileIndex]);
            if (biome == null) continue;

            int anchorX = anchorTileIndex % width;
            int anchorY = anchorTileIndex / width;
            BuildingAsset asset = PickNaturalBuildingAsset(biome, random);
            if (asset == null || asset.fundament == null) continue;

            SubWorldBuildingBounds bounds = SubWorldBuildingGeometry.GetBounds(
                anchorX, anchorY, asset.fundament);
            if (!CanPlaceNaturalBuilding(
                    tiles, width, height, entryTileIndex, biome, asset, bounds, occupied))
            {
                continue;
            }

            MarkNaturalBuildingFootprint(occupied, width, bounds);
            placements.Add(new SubWorldBuildingPlacement(
                new LocalObjectId(placements.Count + 1),
                asset.id,
                anchorTileIndex));
        }

        return placements.ToArray();
    }

    private static int CountNaturalBuildingGround(SubWorldTile[] tiles)
    {
        int count = 0;
        for (int i = 0; i < tiles.Length; i++)
        {
            if (IsNaturalBuildingGround(tiles[i])) count++;
        }

        return count;
    }

    private static BiomeAsset GetNaturalBiome(SubWorldTile tile)
    {
        if (string.IsNullOrEmpty(tile.TopAssetId)) return null;

        TopTileType topTile = AssetManager.top_tiles.get(tile.TopAssetId);
        if (topTile?.biome_asset != null && topTile.biome_asset.grow_vegetation_auto)
            return topTile.biome_asset;

        for (int i = 0; i < AssetManager.biome_library.list.Count; i++)
        {
            BiomeAsset biome = AssetManager.biome_library.list[i];
            if (!biome.grow_vegetation_auto) continue;
            if (biome.tile_low == tile.TopAssetId || biome.tile_high == tile.TopAssetId)
                return biome;
        }

        return null;
    }

    private static BuildingAsset PickNaturalBuildingAsset(BiomeAsset biome, Random random)
    {
        int roll = random.Next(100);
        int preferredCategory = roll < 45 ? 0 : roll < 85 ? 1 : 2;
        for (int offset = 0; offset < 3; offset++)
        {
            int category = (preferredCategory + offset) % 3;
            List<string> pool = category switch
            {
                0 => biome.pot_trees_spawn,
                1 => biome.pot_plants_spawn,
                _ => biome.pot_bushes_spawn
            };
            if (pool == null || pool.Count == 0) continue;

            int start = random.Next(pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                string assetId = pool[(start + i) % pool.Count];
                if (string.IsNullOrWhiteSpace(assetId)) continue;
                BuildingAsset asset = AssetManager.buildings.get(assetId);
                if (asset == null || !asset.is_vegetation || asset.fundament == null) continue;
                return asset;
            }
        }

        return null;
    }

    private static bool CanPlaceNaturalBuilding(
        SubWorldTile[] tiles,
        int width,
        int height,
        int entryTileIndex,
        BiomeAsset biome,
        BuildingAsset asset,
        SubWorldBuildingBounds bounds,
        bool[] occupied)
    {
        if (bounds.MinX < 0 || bounds.MinY < 0 || bounds.MaxX >= width || bounds.MaxY >= height)
            return false;

        int entryX = entryTileIndex % width;
        int entryY = entryTileIndex / width;
        int clearDistanceSquared = EntryBuildingClearRadius * EntryBuildingClearRadius;
        for (int y = bounds.MinY; y <= bounds.MaxY; y++)
        for (int x = bounds.MinX; x <= bounds.MaxX; x++)
        {
            int dx = x - entryX;
            int dy = y - entryY;
            if (dx * dx + dy * dy <= clearDistanceSquared) return false;

            int tileIndex = y * width + x;
            SubWorldTile tile = tiles[tileIndex];
            if (occupied[tileIndex] || !CanNaturalBuildingOccupyTile(tile, asset)) return false;
            if (tile.TopAssetId != biome.tile_low && tile.TopAssetId != biome.tile_high)
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanNaturalBuildingOccupyTile(SubWorldTile tile, BuildingAsset asset)
    {
        if (!IsNaturalBuildingGround(tile)) return false;
        TopTileType topTile = AssetManager.top_tiles.get(tile.TopAssetId);
        if (topTile == null) return false;
        if (topTile.liquid && !asset.can_be_placed_on_liquid) return false;
        if (topTile.block && !asset.can_be_placed_on_blocks) return false;
        return true;
    }

    private static bool IsNaturalBuildingGround(SubWorldTile tile)
    {
        return (tile.MainAssetId == SoilLow || tile.MainAssetId == SoilHigh) &&
               !string.IsNullOrEmpty(tile.TopAssetId);
    }

    private static void MarkNaturalBuildingFootprint(
        bool[] occupied,
        int width,
        SubWorldBuildingBounds bounds)
    {
        for (int y = bounds.MinY; y <= bounds.MaxY; y++)
        for (int x = bounds.MinX; x <= bounds.MaxX; x++)
            occupied[y * width + x] = true;
    }

    private static int FindEntryTile(SubWorldTile[] tiles, int width, int height)
    {
        int centerX = width / 2;
        int centerY = height / 2;
        int bestIndex = -1;
        int bestDistance = int.MaxValue;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int index = y * width + x;
            string mainAssetId = tiles[index].MainAssetId;
            if (mainAssetId != SoilLow && mainAssetId != SoilHigh) continue;
            int dx = x - centerX;
            int dy = y - centerY;
            int distance = dx * dx + dy * dy;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            bestIndex = index;
        }

        return bestIndex;
    }

    private readonly struct NoiseOffsets
    {
        internal NoiseOffsets(float mainX, float mainY, float detailX, float detailY, float fineX, float fineY)
        {
            MainX = mainX;
            MainY = mainY;
            DetailX = detailX;
            DetailY = detailY;
            FineX = fineX;
            FineY = fineY;
        }

        internal float MainX { get; }
        internal float MainY { get; }
        internal float DetailX { get; }
        internal float DetailY { get; }
        internal float FineX { get; }
        internal float FineY { get; }
    }

    private sealed class ProfileData
    {
        internal ProfileData(Island[] islands, Hole[] holes)
        {
            Islands = islands;
            Holes = holes;
        }

        internal Island[] Islands { get; }
        internal Hole[] Holes { get; }
    }

    private readonly struct Island
    {
        internal Island(float x, float y, float radius)
        {
            X = x;
            Y = y;
            Radius = radius;
        }

        internal float X { get; }
        internal float Y { get; }
        internal float Radius { get; }
    }

    private readonly struct Hole
    {
        internal Hole(float x, float y, float radius)
        {
            X = x;
            Y = y;
            Radius = radius;
        }

        internal float X { get; }
        internal float Y { get; }
        internal float Radius { get; }
    }

    private readonly struct RandomShape
    {
        internal RandomShape(float x, float y, float radius, float strength)
        {
            X = x;
            Y = y;
            Radius = radius;
            Strength = strength;
        }

        internal float X { get; }
        internal float Y { get; }
        internal float Radius { get; }
        internal float Strength { get; }
    }

    private readonly struct BiomeRegion
    {
        internal BiomeRegion(BiomeAsset biome, float x, float y)
        {
            Biome = biome;
            X = x;
            Y = y;
        }

        internal BiomeAsset Biome { get; }
        internal float X { get; }
        internal float Y { get; }
    }
}
