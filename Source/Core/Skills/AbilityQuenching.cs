using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>水火相消的明确数量规则，不按显示颜色猜测反应。</summary>
internal static class AbilityQuenching
{
    /// <summary>蒸散一立方地图格的水消耗的火作用量。</summary>
    internal const float ForcePerVolume = 4f;

    /// <summary>只接受已登记的火质和液态水。</summary>
    /// <param name="fire">候选火质。</param><param name="water">候选水质。</param>
    internal static bool Matches(AbilityMatter fire, AbilityMatter water) =>
        fire?.Id == "fire" && fire.State == "energy" && water?.Id == "water" && water.State == "liquid";

    /// <summary>本次有限火力能蒸散的最大水量。</summary>
    /// <param name="force">真正尚存的火作用量。</param>
    internal static double Capacity(float force) => Math.Max(0, force) / ForcePerVolume;
}

/// <summary>独立世界水体的实际接触形状，保留可再次确认的来源编号。</summary>
internal readonly struct QuenchingWater
{
    /// <summary>世界材料编号。</summary>
    internal readonly long Record;

    /// <summary>当前真实形状及水格编号。</summary>
    internal readonly AbilityCapsule Shape;

    /// <summary>保存本次接触候选。</summary>
    /// <param name="record">世界编号。</param><param name="shape">实际形状。</param>
    internal QuenchingWater(long record, AbilityCapsule shape)
    {
        Record = record;
        Shape = shape;
    }
}