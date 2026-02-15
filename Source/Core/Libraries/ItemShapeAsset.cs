using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.Libraries;

public class Shape
{
    public Color[,] Direct;
    public float[,] Dark1;
    public float[,] Dark2;
    public float[,] Dark3;
    public int[,] Source;
    private const int K = 4;

    public Shape(Sprite sprite)
    {
        if (sprite == null)
        {
            ModClass.LogError("Sprite is null!");
            return;
        }

        // 1. 获取像素数据
        Texture2D texture = sprite.texture;
        Rect rect = sprite.rect;
        int width = (int)rect.width;
        int height = (int)rect.height;
        Color[] pixels = texture.GetPixels((int)rect.x, (int)rect.y, width, height);

        // 初始化数组
        Direct = new Color[width, height];
        Dark1 = new float[width, height];
        Dark2 = new float[width, height];
        Dark3 = new float[width, height];
        Source = new int[width, height];

        // 过滤透明像素
        List<Color> valid_pixels = pixels.Where(p => p.a > 0.1f).ToList();
        if (valid_pixels.Count == 0)
        {
            ModClass.LogWarning("No valid pixels (non-transparent) in sprite.");
            return;
        }

        // 2. K-Means聚类获取4个主要颜色（K=4）
        List<Color> centroids = KMeans(valid_pixels, K);

        // 3. 按聚类包含的像素数量排序（确定主色优先级）
        var sorted_centroids = GetSortedCentroidsByCount(valid_pixels, centroids).ToList();
        Color main1 = sorted_centroids.Count >= 1 ? sorted_centroids[0] : Color.white;
        Color main2 = sorted_centroids.Count >= 2 ? sorted_centroids[1] : Color.gray;
        Color main3 = sorted_centroids.Count >= 3 ? sorted_centroids[2] : Color.black;

        // 颜色距离阈值：超过此值视为不归属该主色（可根据需求调整，0.1~0.3较合适）
        const float colorDistanceThreshold = 0.5f;

        // 4. 填充数组（判断归属并设置对应值）
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                Color pixel = pixels[index];

                // 透明像素直接设为默认值
                if (pixel.a < 0.1f)
                {
                    Direct[x, y] = Color.clear;
                    Dark1[x, y] = 0;
                    Dark2[x, y] = 0;
                    Dark3[x, y] = 0;
                    Source[x, y] = -1;
                    continue;
                }

                // 计算像素与三个主色的距离（RGB空间）
                float dist1 = ColorDistance(pixel, main1);
                float dist2 = ColorDistance(pixel, main2);
                float dist3 = ColorDistance(pixel, main3);

                // 找到最近的主色（并判断是否在阈值内）
                float min_dist = Mathf.Min(dist1, dist2, dist3);
                bool is_assigned = min_dist <= colorDistanceThreshold * colorDistanceThreshold; // 用平方比较避免开方

                Direct[x, y] = pixel; 
                if (is_assigned)
                {
                    if (min_dist == dist1)
                    {
                        // 归属main1
                        Dark1[x, y] = CalculateDarkValue(pixel, main1, 1e-3f);
                        Dark2[x, y] = 0;
                        Dark3[x, y] = 0;
                        Source[x, y] = 1;
                    }
                    else if (min_dist == dist2)
                    {
                        // 归属main2
                        Dark1[x, y] = 0;
                        Dark2[x, y] = CalculateDarkValue(pixel, main2, 1e-3f);
                        Dark3[x, y] = 0;
                        Source[x, y] = 2;
                    }
                    else
                    {
                        // 归属main3
                        Dark1[x, y] = 0;
                        Dark2[x, y] = 0;
                        Dark3[x, y] = CalculateDarkValue(pixel, main3, 1e-3f);
                        Source[x, y] = 3;
                    }
                }
                else
                {
                    // 不归属任何主色：Direct设为原像素，所有dark设为0// 保留原像素颜色（包括alpha）
                    Dark1[x, y] = 0;
                    Dark2[x, y] = 0;
                    Dark3[x, y] = 0;
                    Source[x, y] = 0;
                }
            }
        }
    }

// 核心方法：计算调暗系数（基于RGB三个通道的平均值）
    private float CalculateDarkValue(Color pixel, Color mainColor, float minChannelValue)
    {
        // 分别计算每个通道的调暗系数
        float r_dark = CalculateSingleChannelDarkValue(pixel.r, mainColor.r, minChannelValue);
        float g_dark = CalculateSingleChannelDarkValue(pixel.g, mainColor.g, minChannelValue);
        float b_dark = CalculateSingleChannelDarkValue(pixel.b, mainColor.b, minChannelValue);

        // 取三个通道的平均值作为最终darkValue（确保各通道调暗一致）
        float dark_value = (r_dark + g_dark + b_dark) / 3f;

        // 限制范围：0~1（0=不调暗，1=完全变黑）
        return Mathf.Clamp01(dark_value);
    }

// 辅助方法：计算单个通道的调暗系数
    private float CalculateSingleChannelDarkValue(float pixelChannel, float mainChannel, float minChannelValue)
    {
        // 主通道值过小时，直接返回0（避免除以0或异常大的系数）
        if (mainChannel < minChannelValue)
            return 0f;

        // 公式：darkValue = 1 - (像素通道值 / 主色通道值)
        float ratio = pixelChannel / mainChannel;
        return 1f - ratio;
    }

    // 简化版K-Means聚类（颜色聚类）
    private List<Color> KMeans(List<Color> pixels, int k)
    {
        if (pixels.Count <= k)
            return pixels.Distinct().Take(k).ToList(); // 像素太少时直接取独特颜色

        // 1. 随机初始化聚类中心（避免重复）
        HashSet<int> init_indices = new HashSet<int>();
        List<Color> centroids = new List<Color>();
        while (init_indices.Count < k)
        {
            int idx = Randy.randomInt(0, pixels.Count);
            if (init_indices.Add(idx))
                centroids.Add(pixels[idx]);
        }

        // 2. 迭代聚类（最多20次，避免无限循环）
        for (int iter = 0; iter < 20; iter++)
        {
            // 为每个像素分配到最近的中心
            List<List<Color>> clusters = Enumerable.Range(0, k).Select(_ => new List<Color>()).ToList();
            foreach (var pixel in pixels)
            {
                int closest_idx = GetClosestCentroidIndex(pixel, centroids);
                clusters[closest_idx].Add(pixel);
            }

            // 3. 更新聚类中心（取平均值）
            List<Color> new_centroids = new List<Color>();
            foreach (var cluster in clusters)
            {
                if (cluster.Count == 0)
                {
                    // 空聚类时随机重置一个中心
                    new_centroids.Add(pixels[Randy.randomInt(0, pixels.Count)]);
                }
                else
                {
                    // 计算聚类的平均颜色（RGB分别平均）
                    float r = 0, g = 0, b = 0;
                    foreach (var c in cluster)
                    {
                        r += c.r;
                        g += c.g;
                        b += c.b;
                    }

                    new_centroids.Add(new Color(r / cluster.Count, g / cluster.Count, b / cluster.Count));
                }
            }

            // 4. 检查收敛（中心变化小于阈值）
            if (CentroidsConverged(centroids, new_centroids, 0.01f))
                break;

            centroids = new_centroids;
        }

        return centroids;
    }

    // 找到最近的聚类中心索引（基于RGB距离）
    private int GetClosestCentroidIndex(Color pixel, List<Color> centroids)
    {
        int closest_idx = 0;
        float min_dist = float.MaxValue;
        for (int i = 0; i < centroids.Count; i++)
        {
            float dist = ColorDistance(pixel, centroids[i]);
            if (dist < min_dist)
            {
                min_dist = dist;
                closest_idx = i;
            }
        }

        return closest_idx;
    }

    // 计算两个颜色的距离（RGB空间欧氏距离）
    private float ColorDistance(Color a, Color b)
    {
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        return dr * dr + dg * dg + db * db; // 省略开方，不影响比较
    }

    // 检查聚类中心是否收敛（变化小于阈值）
    private bool CentroidsConverged(List<Color> old, List<Color> @new, float threshold)
    {
        for (int i = 0; i < old.Count; i++)
        {
            if (ColorDistance(old[i], @new[i]) > threshold * threshold)
                return false;
        }

        return true;
    }

    // 按聚类包含的像素数量排序中心
    private IEnumerable<Color> GetSortedCentroidsByCount(List<Color> pixels, List<Color> centroids)
    {
        // 用索引代替颜色作为键（避免颜色重复问题）
        int[] cluster_counts = new int[centroids.Count];

        foreach (var pixel in pixels)
        {
            int closest_idx = GetClosestCentroidIndex(pixel, centroids);
            cluster_counts[closest_idx]++; // 统计每个聚类的像素数量
        }

        // 按数量降序排序，返回对应的中心颜色
        return centroids
            .Select((color, index) => new { Color = color, Count = cluster_counts[index] })
            .OrderByDescending(item => item.Count)
            .Select(item => item.Color);
    }
}

public class ItemShapeAsset : Asset
{
    public string major_texture_folder;
    public List<Sprite> major_textures = new();
    public List<Shape> major_shapes = new();
    public Func<Entity, Sprite> GetIcon;
    public void LoadTextures()
    {
        major_textures.Clear();
        if (!string.IsNullOrEmpty(major_texture_folder))
            major_textures.AddRange(SpriteTextureLoader.getSpriteList(major_texture_folder));
        foreach (var sprite in major_textures)
        {
            major_shapes.Add(new Shape(sprite));
        }
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