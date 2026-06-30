using Cultiway.Core;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Extensions;

/// <summary>
/// 宗门杂务规则，限制只有没有职司的外门或内门弟子会通过杂务积累贡献。
/// </summary>
public static class SectChoreRules
{
    /// <summary>
    /// 判断单位当前是否可以执行宗门杂务。
    /// </summary>
    public static bool CanDoSectChore(Actor actor)
    {
        if (actor == null || actor.isRekt()) return false;

        Sect sect = actor.GetExtend().sect;
        if (sect == null || sect.isRekt()) return false;
        if (!actor.HasSectRole(SectRoles.NoOffice)) return false;

        return actor.CanDoSectChore(sect);
    }
}
