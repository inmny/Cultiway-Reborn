using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Utils.Extension;

public static class ActorTools
{
    private static readonly ActorExtendManager ActorExtendManager = ModClass.I.ActorExtendManager;
    public static ActorExtend GetExtend(this Actor actor)
    {
        return ActorExtendManager.Get(actor);
    }
    public static bool CheckExtend(this Actor actor)
    {
        return ActorExtendManager.Has(actor);
    }
    public static bool HasSect(this Actor actor)
    {
        return actor.GetExtend().sect != null;
    }

    /// <summary>
    /// 获取单位在指定宗门角色槽位上的角色；未设置时返回该槽位默认角色。
    /// </summary>
    public static SectRoleAsset GetSectRole(this Actor actor, SectRoleSlot slot)
    {
        if (slot == SectRoleSlot.Grade && GetStoredSectRole(actor, SectRoleSlot.Office)?.clearsGrade == true)
        {
            return ModClass.L.SectRoleLibrary.GetDefault(SectRoleSlot.Grade);
        }

        return GetStoredSectRole(actor, slot);
    }

    /// <summary>
    /// 设置单位在角色所属槽位上的宗门角色。
    /// </summary>
    public static void SetSectRole(this Actor actor, SectRoleAsset role)
    {
        if (role == null) return;
        actor.data.set(GetSectRoleDataKey(role.slot), role.id);
    }

    /// <summary>
    /// 清除单位在指定槽位上保存的宗门角色。
    /// </summary>
    public static void ClearSectRole(this Actor actor, SectRoleSlot slot)
    {
        actor.data.removeString(GetSectRoleDataKey(slot));
    }

    /// <summary>
    /// 清除单位保存的全部宗门角色。
    /// </summary>
    public static void ClearSectRoles(this Actor actor)
    {
        actor.ClearSectRole(SectRoleSlot.Grade);
        actor.ClearSectRole(SectRoleSlot.Office);
        actor.ClearSectRole(SectRoleSlot.Title);
    }

    /// <summary>
    /// 将单位的三个宗门角色槽位设置为默认角色。
    /// </summary>
    public static void SetDefaultSectRoles(this Actor actor)
    {
        actor.SetSectRole(ModClass.L.SectRoleLibrary.GetDefault(SectRoleSlot.Grade));
        actor.SetSectRole(ModClass.L.SectRoleLibrary.GetDefault(SectRoleSlot.Office));
        actor.SetSectRole(ModClass.L.SectRoleLibrary.GetDefault(SectRoleSlot.Title));
    }

    /// <summary>
    /// 判断单位是否拥有指定宗门角色。
    /// </summary>
    public static bool HasSectRole(this Actor actor, SectRoleAsset role)
    {
        if (actor == null || actor.isRekt() || role == null) return false;
        SectRoleAsset current = actor.GetSectRole(role.slot);
        return current != null && current.id == role.id;
    }

    /// <summary>
    /// 判断单位是否通过任一宗门角色获得指定权限。
    /// </summary>
    public static bool HasSectPermission(this Actor actor, SectPermissionAsset permission)
    {
        if (actor == null || actor.isRekt() || permission == null) return false;
        return actor.GetSectRole(SectRoleSlot.Grade)?.HasPermission(permission) == true
               || actor.GetSectRole(SectRoleSlot.Office)?.HasPermission(permission) == true
               || actor.GetSectRole(SectRoleSlot.Title)?.HasPermission(permission) == true;
    }

    /// <summary>
    /// 获取单位在人事 UI 中优先展示的宗门角色。
    /// </summary>
    public static SectRoleAsset GetSectDisplayRole(this Actor actor)
    {
        SectRoleAsset result = null;
        TryUseDisplayRole(actor.GetSectRole(SectRoleSlot.Grade), ref result);
        TryUseDisplayRole(actor.GetSectRole(SectRoleSlot.Office), ref result);
        TryUseDisplayRole(actor.GetSectRole(SectRoleSlot.Title), ref result);
        return result;
    }

    /// <summary>
    /// 获取单位宗门称谓的本地化摘要文本。
    /// </summary>
    public static string GetSectRoleSummary(this Actor actor)
    {
        SectRoleAsset display = actor.GetSectDisplayRole();
        return display == null ? "Cultiway.Sect.Role.None".Localize() : display.GetName();
    }

    private static void TryUseDisplayRole(SectRoleAsset role, ref SectRoleAsset result)
    {
        if (role == null || !role.showInPersonnel) return;
        if (result == null || role.order > result.order)
        {
            result = role;
        }
    }

    private static SectRoleAsset GetStoredSectRole(Actor actor, SectRoleSlot slot)
    {
        actor.data.get(GetSectRoleDataKey(slot), out string roleId);
        SectRoleAsset role = string.IsNullOrEmpty(roleId)
            ? null
            : ModClass.L.SectRoleLibrary.get(roleId);

        return role ?? ModClass.L.SectRoleLibrary.GetDefault(slot);
    }

    private static string GetSectRoleDataKey(SectRoleSlot slot)
    {
        return slot switch
        {
            SectRoleSlot.Grade => ActorDataKeys.SectGradeId_String,
            SectRoleSlot.Office => ActorDataKeys.SectOfficeId_String,
            SectRoleSlot.Title => ActorDataKeys.SectTitleId_String,
            _ => ActorDataKeys.SectGradeId_String
        };
    }

    public static float GetSectJoinTime(this Actor actor)
    {
        actor.data.get(ActorDataKeys.SectJoinTime_Float, out float joinTime, -1f);
        return joinTime;
    }

    public static void SetSectJoinTime(this Actor actor, float joinTime)
    {
        actor.data.set(ActorDataKeys.SectJoinTime_Float, joinTime);
    }

    public static void ClearSectJoinTime(this Actor actor)
    {
        actor.data.removeFloat(ActorDataKeys.SectJoinTime_Float);
    }

    public static int GetSectTenureYears(this Actor actor)
    {
        float joinTime = actor.GetSectJoinTime();
        if (joinTime < 0f) return 0;

        float elapsed = (float)World.world.getCurWorldTime() - joinTime;
        return Mathf.Max(0, Mathf.FloorToInt(elapsed / TimeScales.SecPerYear));
    }

    public static int GetSectContribution(this Actor actor)
    {
        actor.data.get(ActorDataKeys.SectContribution_Int, out int contribution, 0);
        return contribution;
    }

    /// <summary>
    /// 获取单位已消耗的宗门贡献。
    /// </summary>
    public static int GetSpentSectContribution(this Actor actor)
    {
        actor.data.get(ActorDataKeys.SectContributionSpent_Int, out int contribution, 0);
        return contribution;
    }

    /// <summary>
    /// 获取单位当前可用于消费的宗门贡献。
    /// </summary>
    public static int GetAvailableSectContribution(this Actor actor)
    {
        return Mathf.Max(0, actor.GetSectContribution() - actor.GetSpentSectContribution());
    }

    public static void AddSectContribution(this Actor actor, int contribution)
    {
        if (contribution <= 0) return;

        actor.data.set(ActorDataKeys.SectContribution_Int, actor.GetSectContribution() + contribution);
    }

    /// <summary>
    /// 消耗单位的可用宗门贡献，不影响累计贡献。
    /// </summary>
    public static bool TrySpendSectContribution(this Actor actor, int contribution)
    {
        if (actor == null || actor.isRekt()) return false;
        if (contribution <= 0) return true;
        if (actor.GetAvailableSectContribution() < contribution) return false;

        actor.data.set(ActorDataKeys.SectContributionSpent_Int, actor.GetSpentSectContribution() + contribution);
        return true;
    }

    public static void ClearSectContribution(this Actor actor)
    {
        actor.data.removeInt(ActorDataKeys.SectContribution_Int);
        actor.data.removeInt(ActorDataKeys.SectContributionSpent_Int);
    }

    public static SectJobAsset GetSectJob(this Actor actor)
    {
        return actor.GetExtend().TryGetComponent(out SectJobState state) ? state.Job : null;
    }

    public static long GetSectJobSectId(this Actor actor)
    {
        return actor.GetExtend().TryGetComponent(out SectJobState state) ? state.SectId : -1;
    }

    public static void SetSectJob(this Actor actor, Sect sect, SectJobAsset job)
    {
        ref SectJobState state = ref actor.GetExtend().E.GetComponent<SectJobState>();
        state.Job = job;
        state.SectId = sect.getID();
    }

    public static void ClearSectJob(this Actor actor)
    {
        ref SectJobState state = ref actor.GetExtend().E.GetComponent<SectJobState>();
        state.Job = null;
        state.SectId = -1;
    }

    public static string GetSourceSpawnerAssetId(this Actor actor)
    {
        actor.data.get(ActorDataKeys.SourceSpawnerId_String, out string result);
        return result;
    }

    public static long GetSourceSpawnerId(this Actor actor)
    {
        actor.data.get(ActorDataKeys.SourceSpawnerId_Long, out long result, -1);
        return result;
    }

    public static void SetSourceSpawnerId(this Actor actor, long id)
    {
        actor.data.set(ActorDataKeys.SourceSpawnerId_Long, id);
    }

    public static void SetSourceSpawnerAssetId(this Actor actor, string assetId)
    {
        actor.data.set(ActorDataKeys.SourceSpawnerId_String, assetId);
    }
}
