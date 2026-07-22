using System;
using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.Semantics;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.Libraries;

public class Shape
{
    private const int   MaxColorRegions        = 3;
    private const int   HueBinCount            = 36;
    private const float AlphaThreshold         = 0.1f;
    private const float MinSampleSaturation    = 0.06f;
    private const float MinSampleValue         = 0.04f;
    private const float HueCoreDistance        = 0.12f;
    private const float HueOuterDistance       = 0.26f;
    private const float HueSuppressionDistance = 0.19f;
    private const float HueRefineDistance      = 0.095f;
    private const float MinRegionSeparation    = 0.16f;
    private const float HueVariationRetention        = 0.75f;
    private const float SaturationVariationRetention = 0.8f;
    private const float ValueVariationRetention      = 0.8f;
    private const float RegionReferenceQuantile      = 0.5f;
    private const float MinSaturationReference       = 0.15f;

    /// <summary>模板原始像素，用于保留轮廓、高光与不参与换色的装饰。</summary>
    public Color[,] Direct;

    /// <summary>-1 表示透明，0 表示保留原色，1~3 表示对应的可换色区域。</summary>
    public int[,] Source;

    /// <summary>当前像素应用目标颜色的强度，用于平滑换色区边缘。</summary>
    public float[,] ColorBlend;

    /// <summary>相对所属色相族保留的少量色相变化。</summary>
    public float[,] HueOffset;

    /// <summary>像素相对所属区域代表色的饱和度比例。</summary>
    public float[,] SaturationRatio;

    /// <summary>像素相对所属区域代表色的明度偏移。</summary>
    public float[,] ValueOffset;

    private readonly struct PixelSample
    {
        public readonly float Hue;
        public readonly float Saturation;
        public readonly float Value;
        public readonly float Alpha;
        public readonly float Weight;

        public PixelSample(float hue, float saturation, float value, float alpha, float weight)
        {
            Hue = hue;
            Saturation = saturation;
            Value = value;
            Alpha = alpha;
            Weight = weight;
        }
    }

    private readonly struct WeightedValue
    {
        public readonly float Value;
        public readonly float Weight;

        public WeightedValue(float value, float weight)
        {
            Value = value;
            Weight = weight;
        }
    }

    private struct ColorRegion
    {
        public float Hue;
        public float Score;
        public bool  IsNeutral;
    }

    /// <summary>
    ///     将完整彩色模板解析为稳定的色相区域、软边权重和明暗信息，供运行时替换材质颜色。
    /// </summary>
    public Shape(Sprite sprite)
    {
        if (sprite == null)
        {
            ModClass.LogError("Sprite is null!");
            return;
        }

        Texture2D texture = sprite.texture;
        Rect rect = sprite.rect;
        int width = (int)rect.width;
        int height = (int)rect.height;
        Color[] pixels = texture.GetPixels((int)rect.x, (int)rect.y, width, height);

        Direct = new Color[width, height];
        Source = new int[width, height];
        ColorBlend = new float[width, height];
        HueOffset = new float[width, height];
        SaturationRatio = new float[width, height];
        ValueOffset = new float[width, height];

        List<PixelSample> samples = new();
        float[] hue_histogram = new float[HueBinCount];
        float neutral_score = 0f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = pixels[y * width + x];
                Direct[x, y] = pixel;
                Source[x, y] = pixel.a < AlphaThreshold ? -1 : 0;
                if (pixel.a < AlphaThreshold) continue;

                Color.RGBToHSV(pixel, out float hue, out float saturation, out float value);
                neutral_score += GetNeutralBlend(saturation, value) * pixel.a;
                if (saturation < MinSampleSaturation || value < MinSampleValue) continue;

                float weight = pixel.a * (0.2f + 0.8f * saturation) *
                               (0.35f + 0.65f * Mathf.Sqrt(value));
                samples.Add(new PixelSample(hue, saturation, value, pixel.a, weight));
                int hue_bin = Mathf.Clamp(Mathf.FloorToInt(hue * HueBinCount), 0, HueBinCount - 1);
                hue_histogram[hue_bin] += weight;
            }
        }

        if (samples.Count == 0 && neutral_score <= 0f)
        {
            ModClass.LogWarning("No recolorable pixels in item shape sprite.");
            return;
        }

        List<ColorRegion> regions = BuildColorRegions(samples, hue_histogram, neutral_score);
        if (regions.Count == 0) return;

        List<WeightedValue>[] region_saturations = new List<WeightedValue>[regions.Count];
        List<WeightedValue>[] region_values = new List<WeightedValue>[regions.Count];
        for (int i = 0; i < regions.Count; i++)
        {
            region_saturations[i] = new List<WeightedValue>();
            region_values[i] = new List<WeightedValue>();
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = Direct[x, y];
                if (pixel.a < AlphaThreshold) continue;

                Color.RGBToHSV(pixel, out float hue, out float saturation, out float value);
                int best_region = -1;
                float best_blend = 0f;
                for (int i = 0; i < regions.Count; i++)
                {
                    ColorRegion region = regions[i];
                    float blend = region.IsNeutral
                        ? GetNeutralBlend(saturation, value)
                        : GetHueBlend(hue, saturation, region.Hue);
                    if (blend <= best_blend) continue;
                    best_region = i;
                    best_blend = blend;
                }

                if (best_region < 0 || best_blend <= 0f) continue;

                ColorRegion selected_region = regions[best_region];
                Source[x, y] = best_region + 1;
                ColorBlend[x, y] = best_blend;
                HueOffset[x, y] = selected_region.IsNeutral
                    ? 0f
                    : SignedHueDelta(hue, selected_region.Hue) * HueVariationRetention;
                SaturationRatio[x, y] = saturation;
                ValueOffset[x, y] = value;
                if (best_blend > 0.05f)
                {
                    region_saturations[best_region].Add(new WeightedValue(saturation, best_blend * pixel.a));
                    region_values[best_region].Add(new WeightedValue(value, best_blend * pixel.a));
                }
            }
        }

        float[] base_saturations = new float[regions.Count];
        float[] base_values = new float[regions.Count];
        for (int i = 0; i < regions.Count; i++)
        {
            base_saturations[i] = Mathf.Max(MinSaturationReference,
                WeightedQuantile(region_saturations[i], RegionReferenceQuantile));
            base_values[i] = WeightedQuantile(region_values[i], RegionReferenceQuantile);
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int source = Source[x, y];
                if (source <= 0) continue;

                int region_index = source - 1;
                SaturationRatio[x, y] = regions[region_index].IsNeutral
                    ? 1f
                    : Mathf.Pow(SaturationRatio[x, y] / base_saturations[region_index],
                        SaturationVariationRetention);
                ValueOffset[x, y] = (ValueOffset[x, y] - base_values[region_index]) *
                                    ValueVariationRetention;
            }
        }
    }

    /// <summary>从色相直方图中确定最多三个稳定色相族，并让中性色主体参与面积排序。</summary>
    private static List<ColorRegion> BuildColorRegions(List<PixelSample> samples, float[] histogram,
        float neutral_score)
    {
        float[] smoothed = new float[HueBinCount];
        for (int i = 0; i < HueBinCount; i++)
        {
            smoothed[i] =
                histogram[PositiveMod(i - 2, HueBinCount)] +
                histogram[PositiveMod(i - 1, HueBinCount)] * 2f +
                histogram[i] * 3f +
                histogram[(i + 1) % HueBinCount] * 2f +
                histogram[(i + 2) % HueBinCount];
        }

        float[] remaining = (float[])smoothed.Clone();
        List<ColorRegion> candidates = new();
        int attempts = 0;
        while (candidates.Count < MaxColorRegions && attempts++ < HueBinCount)
        {
            int peak_index = 0;
            for (int i = 1; i < HueBinCount; i++)
                if (remaining[i] > remaining[peak_index])
                    peak_index = i;

            if (remaining[peak_index] <= 0f) break;

            float seed_hue = (peak_index + 0.5f) / HueBinCount;
            float x = 0f;
            float y = 0f;
            foreach (PixelSample sample in samples)
            {
                if (CircularHueDistance(sample.Hue, seed_hue) >= HueRefineDistance) continue;
                float angle = sample.Hue * Mathf.PI * 2f;
                x += Mathf.Cos(angle) * sample.Weight;
                y += Mathf.Sin(angle) * sample.Weight;
            }

            float hue = x == 0f && y == 0f
                ? seed_hue
                : Mathf.Repeat(Mathf.Atan2(y, x) / (Mathf.PI * 2f), 1f);
            SuppressHueRange(remaining, seed_hue);
            SuppressHueRange(remaining, hue);

            bool duplicate = false;
            foreach (ColorRegion candidate in candidates)
            {
                if (candidate.IsNeutral || CircularHueDistance(candidate.Hue, hue) >= MinRegionSeparation) continue;
                duplicate = true;
                break;
            }

            if (duplicate) continue;
            candidates.Add(new ColorRegion
            {
                Hue = hue,
                Score = CalculateRegionScore(samples, hue)
            });
        }

        if (neutral_score > 0f)
            candidates.Add(new ColorRegion
            {
                IsNeutral = true,
                Score = neutral_score
            });

        candidates.Sort((left, right) => right.Score.CompareTo(left.Score));
        if (candidates.Count > MaxColorRegions)
            candidates.RemoveRange(MaxColorRegions, candidates.Count - MaxColorRegions);
        return candidates;
    }

    /// <summary>按实际可见像素计算色相族面积，确保主换色区不受初始化顺序影响。</summary>
    private static float CalculateRegionScore(List<PixelSample> samples, float hue)
    {
        float score = 0f;
        foreach (PixelSample sample in samples)
            score += GetHueBlend(sample.Hue, sample.Saturation, hue) * sample.Alpha;
        return score;
    }

    /// <summary>抑制已选色相附近的直方图区间，避免把同一颜色的高光和阴影重复选为多个区域。</summary>
    private static void SuppressHueRange(float[] histogram, float hue)
    {
        for (int i = 0; i < HueBinCount; i++)
            if (CircularHueDistance((i + 0.5f) / HueBinCount, hue) < HueSuppressionDistance)
                histogram[i] = 0f;
    }

    /// <summary>计算像素对指定色相族的软归属权重。</summary>
    private static float GetHueBlend(float hue, float saturation, float region_hue)
    {
        float hue_blend = 1f - SmoothStep(HueCoreDistance, HueOuterDistance,
            CircularHueDistance(hue, region_hue));
        float saturation_blend = SmoothStep(0.025f, 0.14f, saturation);
        return hue_blend * saturation_blend;
    }

    /// <summary>计算灰白主体的软归属权重，同时排除黑色轮廓和纯白高光。</summary>
    private static float GetNeutralBlend(float saturation, float value)
    {
        float saturation_blend = 1f - SmoothStep(0.04f, 0.2f, saturation);
        float shadow_blend = SmoothStep(0.08f, 0.25f, value);
        float highlight_blend = 1f - SmoothStep(0.86f, 1f, value);
        return saturation_blend * shadow_blend * highlight_blend;
    }

    /// <summary>计算带平滑端点的 0~1 插值。</summary>
    private static float SmoothStep(float min, float max, float value)
    {
        float t = Mathf.Clamp01((value - min) / (max - min));
        return t * t * (3f - 2f * t);
    }

    /// <summary>返回环形色相空间中的最短无符号距离。</summary>
    private static float CircularHueDistance(float left, float right)
    {
        float distance = Mathf.Abs(left - right);
        return Mathf.Min(distance, 1f - distance);
    }

    /// <summary>返回从基准色相到像素色相的最短有符号偏移。</summary>
    private static float SignedHueDelta(float hue, float basis)
    {
        return Mathf.Repeat(hue - basis + 0.5f, 1f) - 0.5f;
    }

    /// <summary>按像素归属权重取得数值分位点，用于计算每个分色区的代表饱和度和明度。</summary>
    private static float WeightedQuantile(List<WeightedValue> values, float quantile)
    {
        if (values.Count == 0) return 1f;
        values.Sort((left, right) => left.Value.CompareTo(right.Value));
        float total_weight = 0f;
        foreach (WeightedValue value in values) total_weight += value.Weight;
        if (total_weight <= 0f) return 1f;

        float threshold = total_weight * quantile;
        float accumulated = 0f;
        foreach (WeightedValue value in values)
        {
            accumulated += value.Weight;
            if (accumulated >= threshold) return value.Value;
        }

        return values[^1].Value;
    }

    /// <summary>返回非负取模，供环形直方图访问使用。</summary>
    private static int PositiveMod(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}

public class ItemShapeAsset : Asset
{
    /// <summary>物品形态本身稳定表达的语义，不包含来源与实体组件带来的语义。</summary>
    public SemanticDescriptor semantics = new();
    public string major_texture_folder;
    public List<Sprite> major_textures = new();
    public List<Shape> major_shapes = new();
    public Func<Entity, Sprite> GetIcon;
    public Func<ActorAsset, bool> CheckDropFeature;
    public string[] ingredient_name_candidates = [];

    public bool CanDropFrom(ActorAsset actor_asset)
    {
        return CheckDropFeature?.Invoke(actor_asset) ?? false;
    }

    public string PickIngredientNameCandidate(int seed)
    {
        if (ingredient_name_candidates == null || ingredient_name_candidates.Length == 0) return string.Empty;
        return ingredient_name_candidates[Math.Abs(seed) % ingredient_name_candidates.Length];
    }

    public void LoadTextures()
    {
        major_textures.Clear();
        major_shapes.Clear();
        if (!string.IsNullOrEmpty(major_texture_folder))
            major_textures.AddRange(SpriteTextureLoader.getSpriteList(major_texture_folder));
        foreach (Sprite sprite in major_textures)
            major_shapes.Add(new Shape(sprite));
    }

    public Sprite GetSprite(int idx)
    {
        return major_textures[idx % major_textures.Count];
    }

    public int GetRandomTextureIdx()
    {
        return Randy.randomInt(0, major_textures.Count);
    }
}
