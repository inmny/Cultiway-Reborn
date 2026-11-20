using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>接触位移沿用共用作用页，整招与独立覆盖使用相同字段。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>按距离击退和真实接触换位可独立开关，非法幅度交给正式编译入口判断。</summary>
    /// <param name="impact">当前草稿的实际作用。</param><param name="prefix">整招、身体或部位的行前缀。</param>
    private void ContactDisplacementRows(ConstructionImpact impact, string prefix)
    {
        ContactKnockbackRows(impact.Knockback, value => impact.Knockback = value, prefix);
        Toggle(prefix + "ContactExchange", impact.Exchange != null,
            value => impact.Exchange = value ? new ConstructionExchange() : null);
        if (impact.Exchange != null)
        {
            Number(prefix + "ExchangeChance", impact.Exchange.Chance, value => impact.Exchange.Chance = value);
            Number(prefix + "ExchangeWait", impact.Exchange.WaitSeconds, value => impact.Exchange.WaitSeconds = value);
        }
    }
    /// <summary>有限候选和普通接触使用同样的距离及高度字段。</summary>
    /// <param name="knockback">当前击退。</param><param name="set">替换声明。</param><param name="prefix">作用所属。</param>
    private void ContactKnockbackRows(ConstructionKnockback knockback, System.Action<ConstructionKnockback> set, string prefix)
    {
        Toggle(prefix + "ContactKnockback", knockback != null,
            value => set(value ? new ConstructionKnockback { Distance = 3f } : null));
        if (knockback != null)
        {
            Number(prefix + "KnockbackDistance", knockback.Distance, value => knockback.Distance = value);
            Number(prefix + "KnockbackHeight", knockback.Height, value => knockback.Height = value);
            Toggle(prefix + "KnockbackOnlyWhileRising", knockback.OnlyWhileRising, value => knockback.OnlyWhileRising = value);
        }
    }
}
