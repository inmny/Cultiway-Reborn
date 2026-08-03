using System;
using Cultiway.Core.Components;
using UnityEngine;

namespace Cultiway.Content.Libraries;

/// <summary>统一计算真气、仙基、金丹与元婴的连续品质分和四阶九品。</summary>
internal static class CoreFormationQualityEvaluator
{
    private const float ProfoundThreshold = 0.68f;
    private const float EarthThreshold = 0.82f;
    private const float HeavenThreshold = 0.93f;
    private const float AdvancedProfoundRawThreshold = 1.25f;
    private const float AdvancedEarthRawThreshold = 2.5f;
    private const float AdvancedHeavenRawThreshold = 4f;
    private const float AdvancedMaximumRawScore = 5.5f;

    /// <summary>一次评级同时产出的连续品质分与离散品阶。</summary>
    public readonly struct Evaluation
    {
        /// <summary>创建一份已经完成归一化与品阶映射的评级结果。</summary>
        public Evaluation(float score, ItemLevel level)
        {
            Score = score;
            Level = level;
        }

        /// <summary>供继承、签名和后续评级使用的 0..1 连续品质分。</summary>
        public float Score { get; }

        /// <summary>用于展示和规则判断的黄玄地天四阶九品。</summary>
        public ItemLevel Level { get; }
    }

    /// <summary>按前九层平均凝练品质和元素组成一致性计算真气品质。</summary>
    public static float ResolveQi(float qualitySum, float coherenceSum, int sampleCount)
    {
        if (sampleCount <= 0) throw new ArgumentOutOfRangeException(nameof(sampleCount));
        float quality = qualitySum / sampleCount;
        float coherence = coherenceSum / sampleCount;
        return Mathf.Clamp01(quality * 0.85f + coherence * 0.15f);
    }

    /// <summary>按真气根基、八步熬炼和最终结构契合计算仙基品质。</summary>
    public static float ResolveFoundation(
        float qiQuality,
        float refinementQualitySum,
        int sampleCount,
        float structureQuality)
    {
        if (sampleCount <= 0) throw new ArgumentOutOfRangeException(nameof(sampleCount));
        float refinementQuality = refinementQualitySum / sampleCount;
        return Mathf.Clamp01(
            Mathf.Clamp01(qiQuality) * 0.30f +
            Mathf.Clamp01(refinementQuality) * 0.50f +
            Mathf.Clamp01(structureQuality) * 0.20f);
    }

    /// <summary>按结丹强度、三花均衡和元素均衡计算金丹的连续品质分。</summary>
    public static Evaluation ResolveJindan(float strength, float threeHuaBalance, float elementBalance)
    {
        return ResolveAdvancedFormation(strength, threeHuaBalance, elementBalance);
    }

    /// <summary>按结婴强度、三花均衡和元素均衡计算元婴的连续品质分。</summary>
    public static Evaluation ResolveYuanying(float strength, float threeHuaBalance, float elementBalance)
    {
        return ResolveAdvancedFormation(strength, threeHuaBalance, elementBalance);
    }

    /// <summary>把 0..1 品质分按黄玄地天四段映射为每段九品。</summary>
    public static ItemLevel ResolveItemLevel(float score)
    {
        score = Mathf.Clamp01(score);
        int stage;
        float lower;
        float upper;
        if (score >= HeavenThreshold)
        {
            stage = 3;
            lower = HeavenThreshold;
            upper = 1f;
        }
        else if (score >= EarthThreshold)
        {
            stage = 2;
            lower = EarthThreshold;
            upper = HeavenThreshold;
        }
        else if (score >= ProfoundThreshold)
        {
            stage = 1;
            lower = ProfoundThreshold;
            upper = EarthThreshold;
        }
        else
        {
            stage = 0;
            lower = 0f;
            upper = ProfoundThreshold;
        }

        float progress = Mathf.InverseLerp(lower, upper, score);
        int level = Mathf.Clamp(Mathf.FloorToInt(progress * 9f), 0, 8);
        return ItemLevel.FromValue(stage * 9 + level);
    }

    /// <summary>复用原有高阶成果公式，并把原始分数映射到统一的 0..1 品质轴。</summary>
    private static Evaluation ResolveAdvancedFormation(
        float strength,
        float threeHuaBalance,
        float elementBalance)
    {
        float rawScore = Mathf.Log(Mathf.Max(0f, strength) + 1f, 2f) +
                         Mathf.Clamp01(threeHuaBalance) +
                         Mathf.Clamp01(elementBalance) * 0.5f;
        int stage;
        float rawLower;
        float rawUpper;
        float normalizedLower;
        float normalizedUpper;
        if (rawScore >= AdvancedHeavenRawThreshold)
        {
            stage = 3;
            rawLower = AdvancedHeavenRawThreshold;
            rawUpper = AdvancedMaximumRawScore;
            normalizedLower = HeavenThreshold;
            normalizedUpper = 1f;
        }
        else if (rawScore >= AdvancedEarthRawThreshold)
        {
            stage = 2;
            rawLower = AdvancedEarthRawThreshold;
            rawUpper = AdvancedHeavenRawThreshold;
            normalizedLower = EarthThreshold;
            normalizedUpper = HeavenThreshold;
        }
        else if (rawScore >= AdvancedProfoundRawThreshold)
        {
            stage = 1;
            rawLower = AdvancedProfoundRawThreshold;
            rawUpper = AdvancedEarthRawThreshold;
            normalizedLower = ProfoundThreshold;
            normalizedUpper = EarthThreshold;
        }
        else
        {
            stage = 0;
            rawLower = 0f;
            rawUpper = AdvancedProfoundRawThreshold;
            normalizedLower = 0f;
            normalizedUpper = ProfoundThreshold;
        }

        float progress = Mathf.InverseLerp(rawLower, rawUpper, rawScore);
        float normalizedScore = Mathf.Lerp(normalizedLower, normalizedUpper, progress);
        int level = Mathf.Clamp(Mathf.FloorToInt(progress * 9f), 0, 8);
        return new Evaluation(normalizedScore, ItemLevel.FromValue(stage * 9 + level));
    }
}
