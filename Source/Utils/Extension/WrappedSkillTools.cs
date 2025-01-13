using Cultiway.Core.Libraries;
using Cultiway.Utils.Predefined;

namespace Cultiway.Utils.Extension;

public static class WrappedSkillTools
{
    public static WrappedSkillAsset.Wrapper SetDefaultWakanCost(this WrappedSkillAsset.Wrapper wrapper, float percent)
    {
        return wrapper.SetCostCheck(WrappedSkillCostChecks.DefaultWakanCost(percent));
    }
}