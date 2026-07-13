using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Extensions;

/// <summary>
/// 宗门通用权限规则，负责不属于具体子系统的人事、事务与建造权限。
/// </summary>
public static class SectPermissionRules
{
    /// <summary>
    /// 判断成员能否执行指定宗门的人事评定。
    /// </summary>
    public static bool CanEvaluateSectPersonnel(this Actor actor, Sect sect)
    {
        return IsMemberOfSect(actor, sect)
               && actor.HasSectPermission(SectPermissions.EvaluatePersonnel);
    }

    /// <summary>
    /// 判断成员能否为指定宗门招揽门人。
    /// </summary>
    public static bool CanRecruitSectMember(this Actor actor, Sect sect)
    {
        return IsMemberOfSect(actor, sect)
               && actor.HasSectPermission(SectPermissions.RecruitMember);
    }

    /// <summary>
    /// 判断成员能否将自己的徒弟带入指定宗门。
    /// </summary>
    public static bool CanBringApprenticeToSect(this Actor actor, Sect sect)
    {
        return IsMemberOfSect(actor, sect)
               && actor.HasSectPermission(SectPermissions.BringApprenticeToSect);
    }

    /// <summary>
    /// 判断成员能否为指定宗门执行杂务。
    /// </summary>
    public static bool CanDoSectChore(this Actor actor, Sect sect)
    {
        return IsMemberOfSect(actor, sect)
               && actor.HasSectPermission(SectPermissions.DoSectChore);
    }

    /// <summary>
    /// 判断成员能否参与指定宗门的建筑修建。
    /// </summary>
    public static bool CanBuildSectBuilding(this Actor actor, Sect sect)
    {
        return IsMemberOfSect(actor, sect)
               && actor.HasSectPermission(SectPermissions.BuildBuilding);
    }

    /// <summary>
    /// 判断成员能否任免指定宗门成员的角色。
    /// </summary>
    public static bool CanPromoteSectMember(this Actor actor, Sect sect, Actor target, SectRoleAsset role)
    {
        if (!IsMemberOfSect(actor, sect)) return false;
        if (target == null || target.isRekt()) return false;
        if (target.GetExtend().sect != sect) return false;
        if (role == null) return false;

        return role == SectRoles.Leader
            ? actor.HasSectPermission(SectPermissions.ManageSect)
            : actor.HasSectPermission(SectPermissions.PromoteMember);
    }

    /// <summary>
    /// 判断成员能否管理指定宗门。
    /// </summary>
    public static bool CanManageSect(this Actor actor, Sect sect)
    {
        return IsMemberOfSect(actor, sect)
               && actor.HasSectPermission(SectPermissions.ManageSect);
    }

    private static bool IsMemberOfSect(Actor actor, Sect sect)
    {
        return actor != null
               && !actor.isRekt()
               && sect != null
               && !sect.isRekt()
               && actor.GetExtend().sect == sect;
    }

}
