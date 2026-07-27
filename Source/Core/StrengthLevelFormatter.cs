using System.Collections.Generic;
using MathNet.Numerics.Distributions;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Core;

/// <summary>把连续强度转换为修炼体系定义的离散品阶名称。</summary>
internal static class StrengthLevelFormatter
{
    private const float QuantileWeight = 0.9f;
    private static readonly Dictionary<int, float[]> EdgeValueCache = new();

    /// <summary>按指定展示风格返回强度品阶；没有风格时使用仙道默认的四阶九品。</summary>
    public static string GetLevelName(float strength, ElementRootDisplayStyle style)
    {
        int count = style?.TotalLevelCount ?? 36;
        int index = GetStrengthIndex(strength, count);
        int levelsPerStage = style?.level_per_stage ?? 9;
        int stageIndex = index / levelsPerStage;
        int levelIndex = index % levelsPerStage;

        if (style == null)
            return LM.Get($"Cultiway.Stage.{stageIndex}") + "阶" + LM.Get($"Cultiway.Level.{levelIndex}");

        string stageName = LM.Get(style.stage_name_keys[stageIndex]);
        string levelName = LM.Get(style.level_name_keys[levelIndex]);
        return style.level_format
            .Replace("{stage}", stageName)
            .Replace("{level}", levelName);
    }

    /// <summary>返回连续强度落入的离散档位序号。</summary>
    private static int GetStrengthIndex(float strength, int count)
    {
        float[] edges = GetEdgeValues(count);
        for (var i = 0; i < edges.Length; i++)
            if (strength <= edges[i])
                return i;
        return edges.Length - 1;
    }

    /// <summary>按半正态分布与递减权重生成并缓存指定档位数的阈值。</summary>
    private static float[] GetEdgeValues(int count)
    {
        if (EdgeValueCache.TryGetValue(count, out float[] cached)) return cached;

        var values = new float[count];
        float probability = 1f / count;
        bool uniform = Mathf.Approximately(QuantileWeight, 1f);
        if (!uniform)
            probability = (1f - QuantileWeight) / (1f - Mathf.Pow(QuantileWeight, count));

        for (var i = 0; i < values.Length; i++)
        {
            float cumulative = uniform
                ? probability * i
                : probability * (1f - Mathf.Pow(QuantileWeight, i)) / (1f - QuantileWeight);
            values[i] = (float)Normal.InvCDF(0d, 1d, 0.5d + cumulative / 2d);
        }

        EdgeValueCache[count] = values;
        return values;
    }
}
