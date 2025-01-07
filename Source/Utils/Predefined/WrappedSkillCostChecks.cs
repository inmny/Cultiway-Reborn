using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Libraries;

namespace Cultiway.Utils.Predefined;

public static class WrappedSkillCostChecks
{
    public static CostCheck DefaultWakanCost(float percent)
    {
        return (ActorExtend ae, out float strength) =>
        {
            strength = 0;
            if (!ae.TryGetComponent(out Xian xian))
            {
                return false;
            }

            strength = ae.Base.stats[BaseStatses.MaxWakan.id] * percent;
            if (xian.wakan < strength)
            {
                return false;
            }

            strength *= 10;
            
            return true;
        };
    }
}