using System;
using Cultiway.Abstract;

namespace Cultiway.Content.KingdomPolicies;

/// <summary>政策直接接入原版忠诚度项目，城市数值、叛乱判断和提示明细使用同一结果。</summary>
internal sealed class KingdomPolicyLoyaltyContent : ICanInit
{
    /// <summary>每项有影响的政策只登记一次，关闭时由原版计算为零并从明细移除。</summary>
    public void Init()
    {
        foreach (KingdomPolicyCard card in Enum.GetValues(typeof(KingdomPolicyCard)))
        {
            int amount = KingdomPolicyData.LoyaltyImpact(card);
            if (amount == 0) continue;
            AssetManager.loyalty_library.add(new LoyaltyAsset {
                id = "Cultiway.Policy." + card,
                translation_key = "Cultiway.Policies." + card + ".Name",
                calc = city => city?.kingdom != null && KingdomPolicyService.Has(city, card) ? amount : 0
            });
        }
    }
}
