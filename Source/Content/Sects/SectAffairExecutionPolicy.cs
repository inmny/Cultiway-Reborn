using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Sects;

/// <summary>
/// 根据事务资产统一判断宗门成员能否执行事务。
/// </summary>
public static class SectAffairExecutionPolicy
{
    /// <summary>
    /// 判断单位当前是否可以执行任意已注册的宗门事务。
    /// </summary>
    public static bool CanExecuteAny(Actor actor)
    {
        foreach (SectAffairAsset affair in ModClass.L.SectAffairLibrary.list)
        {
            if (CanExecute(actor, affair)) return true;
        }

        return false;
    }

    /// <summary>
    /// 判断单位当前是否可以执行指定宗门事务。
    /// </summary>
    public static bool CanExecute(Actor actor, SectAffairAsset affair)
    {
        if (actor == null || actor.isRekt()) return false;
        if (affair == null || string.IsNullOrEmpty(affair.taskId)) return false;

        Sect sect = actor.GetExtend().sect;
        if (sect == null || sect.isRekt()) return false;

        bool bypassRequirements = affair.bypassRequirements?.Invoke(actor, sect) ?? false;
        if (!bypassRequirements && affair.requiredPermission != null &&
            !actor.HasSectPermission(affair.requiredPermission)) return false;
        if (!bypassRequirements && !MeetsOfficeRequirement(actor, affair)) return false;

        return affair.canExecute?.Invoke(actor, sect) ?? true;
    }

    private static bool MeetsOfficeRequirement(Actor actor, SectAffairAsset affair)
    {
        SectRoleAsset required = affair.requiredOfficeRole;
        if (required == null) return true;

        SectRoleAsset current = actor.GetSectRole(SectRoleSlot.Office);
        if (current == null) return false;

        return affair.requireOfficeAtLeast
            ? current.order >= required.order
            : current == required;
    }
}
