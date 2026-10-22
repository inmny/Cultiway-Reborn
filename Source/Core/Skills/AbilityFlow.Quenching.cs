using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>真实局部蒸散与原有水流推进共用同一份水量。</summary>
internal sealed partial class AbilityFlow
{
    /// <summary>扣除指定水格，速度不因同比移除质量而凭空改变。</summary>
    /// <param name="index">实际水格。</param><param name="maximum">本次允许蒸散的最大体积。</param>
    internal double Evaporate(int index, double maximum)
    {
        Vector3 before = Water[index];
        double volume = before.x * (double)Cell * Cell;
        if (volume <= 0 || maximum <= 0) return 0;
        float retained = (float)Math.Max(0, 1 - Math.Min(volume, maximum) / volume);
        Water[index] *= retained;
        if (Budget != null) Budget[index] *= retained;
        double removed = (before.x - (double)Water[index].x) * Cell * Cell;
        Evaporated += removed;
        Recount();
        return removed;
    }
}