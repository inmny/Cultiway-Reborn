using System.Collections.Generic;

namespace Cultiway.Core.Libraries;

/// <summary>
/// 宗门特质资产库。
/// </summary>
public class SectTraitLibrary : BaseTraitLibrary<SectTrait>
{
    public override string icon_path => "cultiway/icons/sect_traits/";

    public override List<string> getDefaultTraitsForMeta(ActorAsset pAsset)
    {
        return null;
    }

    public override void post_init()
    {
        base.post_init();
        LinkResidenceStrategyOpposites();
    }

    private void LinkResidenceStrategyOpposites()
    {
        List<SectTrait> strategies = GetResidenceStrategies();
        for (int i = 0; i < strategies.Count; i++)
        {
            SectTrait strategy = strategies[i];
            strategy.opposite_traits = new HashSet<SectTrait>();
            for (int j = 0; j < strategies.Count; j++)
            {
                if (i != j)
                {
                    strategy.opposite_traits.Add(strategies[j]);
                }
            }
        }
    }

    public List<SectTrait> GetResidenceStrategies()
    {
        var result = new List<SectTrait>();
        for (int i = 0; i < list.Count; i++)
        {
            SectTrait trait = list[i];
            if (trait.isResidenceStrategy)
            {
                result.Add(trait);
            }
        }

        return result;
    }
}
