using System;
using Cultiway.Core;

namespace Cultiway.Content.Skills;

/// <summary>施放和世界材料共用原资源写入，不复制精度、容量和整数规则。</summary>
internal static class MaterialRecoveryCredit
{
    /// <summary>只读查询当前原资源的可入账容量，日常需求不预先制造或领取资源。</summary>
    /// <param name="owner">实际原支付者。</param><param name="resource">实际原资源。</param>
    internal static double Room(ActorExtend owner, string resource)
    {
        var channel = ConstructionResources.Resolve(resource);
        return channel?.MaximumAmount != null && channel.IsAvailable(owner)
            ? Math.Max(0, (double)channel.MaximumAmount(owner) - channel.ReadAmount(owner))
            : 0d;
    }

    /// <summary>调用方已确认原支付者仍有效；只改动原来存在的资源体系。</summary>
    /// <param name="owner">有效原支付者。</param><param name="resource">原资源种类。</param>
    /// <param name="amount">本次可入账的份额。</param><param name="fraction">只保留因精度暂时不能表示的余额。</param>
    internal static double Grant(ActorExtend owner, string resource, double amount, out double fraction)
    {
        fraction = 0;
        var channel = ConstructionResources.Resolve(resource);
        if (channel?.MaximumAmount == null || !channel.IsAvailable(owner)) return 0d;
        double before = channel.ReadAmount(owner), after;
        if (channel.IntegerAmount)
        {
            double allowed = Math.Min(Math.Floor(amount + 1e-8), Math.Max(0d, channel.MaximumAmount(owner) - before));
            after = before + allowed;
        }
        else
        {
            float balance = (float)before;
            Add(ref balance, amount, (float)channel.MaximumAmount(owner), out fraction);
            after = balance;
        }

        if (after == before) return 0d;
        channel.WriteAmount(owner, after);
        return Math.Max(0d, channel.ReadAmount(owner) - before);
    }

    /// <summary>向下取可表示值，不用多次单精度加法抬高预算。</summary>
    /// <param name="balance">实际原资源。</param><param name="amount">本次份额。</param>
    /// <param name="maximum">当前容量。</param><param name="fraction">精度余额，容量不足的部分当次损失。</param>
    private static unsafe double Add(ref float balance, double amount, float maximum, out double fraction)
    {
        double before = balance, allowed = Math.Min(amount, Math.Max(0, (double)maximum - before));
        double target = before + allowed;
        float after = (float)target;
        if (after > target)
        {
            int bits = *(int*)&after - 1;
            after = *(float*)&bits;
        }

        balance = after;
        double granted = (double)after - before;
        fraction = Math.Max(0, allowed - granted);
        return granted;
    }
}