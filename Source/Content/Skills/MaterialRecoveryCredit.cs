using System;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>施放和世界材料共用原资源写入，不复制精度、容量和整数规则。</summary>
internal static class MaterialRecoveryCredit
{
    /// <summary>调用方已确认原支付者仍有效；只改动原来存在的资源体系。</summary>
    /// <param name="owner">有效原支付者。</param><param name="resource">原资源种类。</param>
    /// <param name="amount">本次可入账的份额。</param><param name="fraction">只保留因精度暂时不能表示的余额。</param>
    internal static double Grant(ActorExtend owner, string resource, double amount, out double fraction)
    {
        fraction = 0;
        switch (resource)
        {
            case "wakan" when owner.HasCultisys<Xian>():
                return Add(ref owner.GetCultisys<Xian>().wakan, amount, owner.Base.stats[BaseStatses.MaxWakan.id],
                    out fraction);
            case "vigor" when owner.HasCultisys<Knight>():
                return Add(ref owner.GetCultisys<Knight>().vigor, amount, owner.Base.stats[BaseStatses.MaxVigor.id],
                    out fraction);
            case "mana":
                int mana = owner.Base.getMana();
                owner.Base.setMana(mana + Mathf.Min((int)amount, Mathf.Max(0, owner.Base.getMaxMana() - mana)), false);
                return owner.Base.getMana() - mana;
            case "stamina":
                int stamina = owner.Base.data.stamina;
                owner.Base.data.stamina += Mathf.Min((int)amount, Mathf.Max(0, owner.Base.getMaxStamina() - stamina));
                return owner.Base.data.stamina - stamina;
            default: return 0;
        }
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