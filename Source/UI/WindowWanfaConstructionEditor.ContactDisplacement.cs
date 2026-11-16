using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>接触位移沿用共用作用页，整招与独立覆盖使用相同字段。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>按距离击退和真实接触换位可独立开关，非法幅度交给正式编译入口判断。</summary>
    /// <param name="impact">当前草稿的实际作用。</param><param name="prefix">整招、身体或部位的行前缀。</param>
    private void ContactDisplacementRows(ConstructionImpact impact, string prefix)
    {
        Toggle(prefix + "ContactKnockback", impact.Knockback != null,
            value => impact.Knockback = value ? new ConstructionKnockback { Distance = 3f } : null);
        if (impact.Knockback != null)
        {
            Number(prefix + "KnockbackDistance", impact.Knockback.Distance, value => impact.Knockback.Distance = value);
            Number(prefix + "KnockbackHeight", impact.Knockback.Height, value => impact.Knockback.Height = value);
        }
        Toggle(prefix + "ContactExchange", impact.Exchange != null,
            value => impact.Exchange = value ? new ConstructionExchange() : null);
        if (impact.Exchange != null)
        {
            Number(prefix + "ExchangeChance", impact.Exchange.Chance, value => impact.Exchange.Chance = value);
            Number(prefix + "ExchangeWait", impact.Exchange.WaitSeconds, value => impact.Exchange.WaitSeconds = value);
        }
    }
}
