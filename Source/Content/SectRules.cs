using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Utils.Extension;

namespace Cultiway.Content;

public static class SectRules
{
    public static bool CanFoundSect(Actor actor)
    {
        if (actor == null || actor.isRekt()) return false;
        return CanFoundSect(actor.GetExtend());
    }

    public static bool CanFoundSect(ActorExtend ae)
    {
        if (ae == null || ae.Base == null || ae.Base.isRekt()) return false;
        if (ae.sect != null) return false;
        if (!ae.HasCultisys<Xian>()) return false;
        if (ae.GetCultisys<Xian>().CurrLevel < XianLevels.Yuanying) return false;
        if (!HasAvailableFounderApprentice(ae)) return false;

        return ae.GetMainCultibook() != null
               && SectResidencePlanner.HasFoundingSite(ae.Base);
    }

    private static bool HasAvailableFounderApprentice(ActorExtend founder)
    {
        foreach (ActorExtend apprentice in founder.GetApprentices())
        {
            if (apprentice.Base == null || apprentice.Base.isRekt()) continue;
            if (apprentice.sect != null) continue;

            return true;
        }

        return false;
    }
}
