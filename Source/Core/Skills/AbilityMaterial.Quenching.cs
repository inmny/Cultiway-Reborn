using System;

namespace Cultiway.Core.Skills;

/// <summary>独立材料的局部蒸散，保存、回收和显示继续使用真实剩余值。</summary>
public sealed partial class AbilityMaterial
{
    /// <summary>只移除真正接触的材料，不创建新的支付份额。</summary>
    /// <param name="shape">原形编号或实际水格。</param><param name="maximum">允许蒸散的最大体积。</param>
    internal double EvaporateWater(int shape, double maximum)
    {
        double before = Volume;
        if (before <= 0) return 0;
        if (Flow == null)
        {
            double removed = Math.Min(before, maximum);
            Retain((float)Math.Max(0, 1 - removed / before));
            return before - Volume;
        }

        double loss = Flow.Evaporate(shape, maximum);
        Volume = Flow.Volume;
        if (Flow.Budget == null) Resource *= Math.Max(0, 1 - loss / before);
        else Resource = Flow.Resource;
        return loss;
    }
}