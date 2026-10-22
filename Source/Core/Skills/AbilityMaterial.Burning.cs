using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>独立世界材料的燃烧与同一数值体积共用状态。</summary>
public sealed partial class AbilityMaterial
{
    /// <summary>真实接触只接收尚缺的点火量，来源随后扣除同一数量。</summary>
    /// <param name="available">来源尚可提供的作用量。</param><param name="source">实际点火来源。</param>
    /// <param name="point">实际世界接触位置。</param>
    internal double HeatWood(double available, long source, Vector3 point)
    {
        if (!AbilityBurning.CanBurn(Matter) || Volume <= 0) return 0;
        double amount = Math.Min(available, Burning.Needed(Volume));
        if (amount <= 0) return 0;
        Burning.AddHeat(Volume, amount, source);
        Vector3 local = Transform.inverse.MultiplyPoint3x4(point);
        Burning.X = local.x;
        Burning.Y = local.y;
        Burning.Z = local.z;
        return amount;
    }

    /// <summary>烧损改变实际几何与支付，不把损耗记为回能。</summary>
    /// <param name="seconds">共同世界时间。</param>
    private void AdvanceBurning(float seconds)
    {
        if (!AbilityBurning.CanBurn(Matter))
        {
            Burning.Extinguish();
            return;
        }

        double before = Volume, removed = Burning.Advance(seconds, Volume);
        if (removed <= 0) return;
        var burning = Burning;
        Retain((float)Math.Max(0, 1 - removed / before), recovery: false);
        Burning = burning;
        if (Volume <= Math.Max(1e-10, Burning.Fuel * 1e-6))
        {
            Retain(0, recovery: false);
            Burning = burning;
            Burning.Extinguish();
        }
    }
}