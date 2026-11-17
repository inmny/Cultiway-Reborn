using System;

namespace Cultiway.Core.Skills;

/// <summary>原版永久结果只取得已经接触的有限水格。</summary>
internal sealed partial class AbilityFlow
{
    /// <summary>同比交出水深、动量和支付份额，单独记录实际转交量。</summary>
    /// <param name="index">已检查的真实接触格。</param><param name="volume">已检查的所需体积。</param>
    internal double Transfer(int index, double volume)
    {
        double before = Water[index].x * (double)Cell * Cell;
        float retained = (float)Math.Max(0, 1 - volume / before);
        Water[index] *= retained;
        if (Budget != null) Budget[index] *= retained;
        double removed = before - Water[index].x * (double)Cell * Cell;
        Transferred += removed;
        Recount();
        return removed;
    }
}
