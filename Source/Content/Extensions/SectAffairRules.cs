using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Extensions;

/// <summary>
/// 宗门事务规则，统一判断成员可执行的事务资产并提供随机选择。
/// </summary>
public static class SectAffairRules
{
    private static readonly SectAffairAsset[] Affairs =
    [
        SectAffairs.Chore,
        SectAffairs.OrganizeScripture,
        SectAffairs.LectureCultibook
    ];

    /// <summary>
    /// 判断单位当前是否可以执行任意宗门事务。
    /// </summary>
    public static bool CanDoAnySectAffair(Actor actor)
    {
        for (int i = 0; i < Affairs.Length; i++)
        {
            if (CanDoSectAffair(actor, Affairs[i])) return true;
        }

        return false;
    }

    /// <summary>
    /// 判断单位当前是否可以执行指定宗门事务。
    /// </summary>
    public static bool CanDoSectAffair(Actor actor, SectAffairAsset affair)
    {
        if (actor == null || actor.isRekt()) return false;
        if (affair == null || string.IsNullOrEmpty(affair.taskId)) return false;

        Sect sect = actor.GetExtend().sect;
        if (sect == null || sect.isRekt()) return false;
        if (!HasAffairPermission(actor, sect, affair)) return false;
        if (!MeetsOfficeRequirement(actor, affair) && !CanBypassOfficeRequirement(actor, sect, affair)) return false;

        if (affair == SectAffairs.Chore) return SectChoreRules.CanDoSectChore(actor);
        if (affair == SectAffairs.OrganizeScripture) return CanOrganizeScripture(sect);
        if (affair == SectAffairs.LectureCultibook) return SectLectureRules.CanLectureCultibook(actor, sect);

        return true;
    }

    /// <summary>
    /// 按事务权重从当前单位可执行的宗门事务中随机选择一个。
    /// </summary>
    public static bool TryPickSectAffair(Actor actor, out SectAffairAsset affair)
    {
        affair = null;
        List<SectAffairAsset> candidates = new();
        float totalWeight = 0f;
        for (int i = 0; i < Affairs.Length; i++)
        {
            SectAffairAsset candidate = Affairs[i];
            if (!CanDoSectAffair(actor, candidate)) continue;

            candidates.Add(candidate);
            totalWeight += GetAffairWeight(actor.GetExtend().sect, candidate);
        }

        if (candidates.Count == 0) return false;

        float roll = Randy.randomFloat(0f, totalWeight);
        for (int i = 0; i < candidates.Count; i++)
        {
            SectAffairAsset candidate = candidates[i];
            roll -= GetAffairWeight(actor.GetExtend().sect, candidate);
            if (roll <= 0f)
            {
                affair = candidate;
                return true;
            }
        }

        affair = candidates[^1];
        return true;
    }

    /// <summary>
    /// 根据资产 id 获取宗门事务资产。
    /// </summary>
    public static SectAffairAsset GetAffair(string affairId)
    {
        return string.IsNullOrEmpty(affairId) || !ModClass.L.SectAffairLibrary.has(affairId)
            ? null
            : ModClass.L.SectAffairLibrary.get(affairId);
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

    private static bool HasAffairPermission(Actor actor, Sect sect, SectAffairAsset affair)
    {
        if (affair.requiredPermission == null) return true;
        if (actor.HasSectPermission(affair.requiredPermission)) return true;
        return affair == SectAffairs.OrganizeScripture && SectTraitRules.CanDiscipleOrganizeScripture(sect, actor);
    }

    private static bool CanBypassOfficeRequirement(Actor actor, Sect sect, SectAffairAsset affair)
    {
        return affair == SectAffairs.OrganizeScripture && SectTraitRules.CanDiscipleOrganizeScripture(sect, actor);
    }

    private static float GetAffairWeight(Sect sect, SectAffairAsset affair)
    {
        return Mathf.Max(0.01f, affair.weight * SectTraitRules.GetAffairWeightMultiplier(sect, affair));
    }

    private static bool CanOrganizeScripture(Sect sect)
    {
        return sect.GetScriptureBookIds().Count > 0;
    }
}
