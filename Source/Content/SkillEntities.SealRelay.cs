using Cultiway.Core;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;

namespace Cultiway.Content;

public partial class SkillEntities
{
    /// <summary>以实际折纸接触留印，并从印面飞出后续纸符。</summary>
    public static SkillEntityAsset SealRelay { get; private set; }

    /// <summary>通过既有学习、制符和逐发支付入口登记。</summary>
    private static void ConfigureSealRelay()
    {
        Configure(SealRelay, ElementComposition.Static.Wood, Anim(SpiritBolt, 0),
                SkillTrajectories.TowardsPosition, SkillImpactProfileLibrary.NormalProjectile,
                SkillTrajectoryDomain.FlyingBody, SkillUseProfileLibrary.SingleEnemyObjectOrPoint,
                SkillEntityType.Attack, false, null, SkillSemantics.Element.Wood,
                SkillSemantics.Form.Single, SkillSemantics.Effect.Combo, SkillSemantics.Delivery.Projectile,
                SkillSemantics.Role.Offensive)
            .SetBaseCastDemand(4f);
    }
}
