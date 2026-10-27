#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;

namespace Cultiway.Content.Worldfall;

/// <summary>用基础成年东人的身高统一群系树木和花草的显示比例。</summary>
internal static partial class WorldfallBiomeSizes
{
    /// <summary>普通东人男装的有效高度9像素，基础缩放0.1，每游戏单位1像素。</summary>
    internal const float AdultHeight = 0.9f;
    /// <summary>未单独指定的花草保留原有脚踝至膝盖比例。</summary>
    internal const float PlantScale = 1.2f;
    /// <summary>烛火树下移累计0.12模型单位，约占树高6%，三档与残骸共用。</summary>
    internal const float CandleRootSink = 0.12f;
    /// <summary>血肉树下移0.06模型单位，约占树高3%。</summary>
    internal const float FleshBloodRootSink = 0.06f;
    /// <summary>泰坦树类累计下移0.18模型单位，约占自身高度9%，三档与残骸共用。</summary>
    internal const float TitansRootSink = 0.18f;
    /// <summary>小型烛火下移0.01模型单位，约占其高度3%。</summary>
    internal const float CandlePlantSink = 0.01f;
    /// <summary>目标最高外观除以原生最高外观；原生高度包含树木放大，保留各帧比例。</summary>
    private static readonly Dictionary<string, float> TreeScales = new(StringComparer.Ordinal)
    {
        ["Bamboo_tree"] = AdultHeight * 14.4f / 18.375f,
        ["Dark_tree"] = AdultHeight * 15.2f / 15.875f,
        ["Fern_tree"] = AdultHeight * 7.2f / 16.125f,
        ["Oak_tree"] = AdultHeight * 14.4f / 20.625f,
        ["Candle_tree"] = AdultHeight * 7.2f / 14.625f,
        ["Cemetery_tree"] = AdultHeight * 2.2f / 12.9375f,
        ["Coral_tree"] = AdultHeight * 2.4f / 11.25f,
        ["FleshBlood_tree"] = AdultHeight * 4.8f / 17.125f,
        ["Knowledge_tree"] = AdultHeight * 5.6f / 12.9375f,
        ["Rice_tree"] = AdultHeight * 3.6f / 14.0625f,
        ["Titans_tree"] = AdultHeight * 5.2f / 14.625f,
        ["Nurgle_tree"] = AdultHeight * 3.8f / (16.875f * 34f / 35f),
        ["Khorne_tree"] = AdultHeight * 3.6f / 15.125f,
        ["Tzeentch_tree"] = AdultHeight * 3.6f / 15.125f,
        ["Slaanesh_tree"] = AdultHeight * 3.6f / 15.125f
    };

    /// <summary>取得模组群系树木的等比调整量，原版物件保持原比例。</summary>
    /// <param name="assetId">树木的实际注册编号。</param>
    /// <returns>在原生生长比例之后应用的调整量。</returns>
    internal static float TreeScale(string assetId) => TreeScales.TryGetValue(assetId, out float scale) ? scale : 1f;

    /// <summary>按用户逐次确认的群系花草比例放置，知识与稻田累计放大四倍。</summary>
    /// <param name="assetId">模型记录的实际花草编号。</param>
    /// <returns>原生花草放置后的等比调整量。</returns>
    internal static float PlantScaleFor(string assetId) => assetId switch
    {
        "Knowledge" or "Rice" or "Bamboo" or "Candle" or "Oak" or "Dark" => PlantScale * 4f,
        "Fern" or "FleshBlood" or "Titans" => PlantScale * 2f,
        _ => PlantScale
    };
}
#endif
