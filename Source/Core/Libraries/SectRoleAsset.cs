using System.Collections.Generic;
using Cultiway.Core;
using NeoModLoader.General;

namespace Cultiway.Core.Libraries;

/// <summary>
/// 宗门角色资产；描述一个门阶、职司或头衔的显示、门槛、名额和权限。
/// </summary>
public class SectRoleAsset : Asset
{
    /// <summary>
    /// 角色所属槽位。
    /// </summary>
    public SectRoleSlot slot;

    /// <summary>
    /// 角色在同槽位和人事 UI 中的排序值，数值越大越靠前。
    /// </summary>
    public int order;

    /// <summary>
    /// 角色的继任和排序权重。
    /// </summary>
    public int authority;

    /// <summary>
    /// 角色名称的本地化 key。
    /// </summary>
    public string nameKey;

    /// <summary>
    /// 角色说明的本地化 key。
    /// </summary>
    public string descriptionKey;

    /// <summary>
    /// 角色在 UI 中使用的图标路径。
    /// </summary>
    public string iconPath;

    /// <summary>
    /// 是否为所属槽位的默认角色。
    /// </summary>
    public bool defaultForSlot;

    /// <summary>
    /// 是否在人事列表中作为独立分组展示。
    /// </summary>
    public bool showInPersonnel = true;

    /// <summary>
    /// 是否允许普通人事评定自动授予该角色。
    /// </summary>
    public bool allowAutoAssign;

    /// <summary>
    /// 是否允许招募入宗时直接授予该角色。
    /// </summary>
    public bool allowInitialAssign;

    /// <summary>
    /// 是否在拥有该角色时忽略并清除门阶。
    /// </summary>
    public bool clearsGrade;

    /// <summary>
    /// 授予该角色需要的最低人事评分。
    /// </summary>
    public int minPersonnelScore;

    /// <summary>
    /// 授予该角色需要的最低修仙境界；小于 0 表示无要求。
    /// </summary>
    public int minCultivationLevel = -1;

    /// <summary>
    /// 该角色的基础名额；小于 0 表示不限名额。
    /// </summary>
    public int baseSlots = -1;

    /// <summary>
    /// 每多少名宗门成员增加一个额外名额；小于等于 0 表示不随人数增长。
    /// </summary>
    public int membersPerExtraSlot;

    /// <summary>
    /// 该角色拥有的权限资产 id 列表。
    /// </summary>
    public List<string> permissionIds = new();

    /// <summary>
    /// 授予该角色需要的最低师徒关系类型 id；为空表示无师徒要求。
    /// </summary>
    public string requiredMasterRelationTypeId;

    /// <summary>
    /// 满足该角色师徒要求时，是否允许自动安排符合条件的师父。
    /// </summary>
    public bool canAutoAssignMasterForRequirement;

    /// <summary>
    /// 自动安排师父时要求师父至少拥有的宗门职司角色 id。
    /// </summary>
    public string requiredMasterOfficeRoleId;

    /// <summary>
    /// 自动安排师父时要求师父拥有的宗门权限 id。
    /// </summary>
    public string requiredMasterPermissionId;

    /// <summary>
    /// 授予该角色时要求成员至少拥有的门阶角色 id；为空表示无门阶要求。
    /// </summary>
    public string requiredGradeRoleId;

    /// <summary>
    /// 授予该角色时要求成员当前拥有的前置职司角色 id；为空表示无前置职司要求。
    /// </summary>
    public string requiredPreviousOfficeRoleId;

    /// <summary>
    /// 获取角色的本地化名称。
    /// </summary>
    public string GetName()
    {
        return (string.IsNullOrEmpty(nameKey) ? id : nameKey).Localize();
    }

    /// <summary>
    /// 获取角色的本地化说明。
    /// </summary>
    public string GetDescription()
    {
        return (string.IsNullOrEmpty(descriptionKey) ? $"{id}.Info" : descriptionKey).Localize();
    }

    /// <summary>
    /// 判断该角色是否拥有指定权限。
    /// </summary>
    public bool HasPermission(SectPermissionAsset permission)
    {
        return permission != null && permissionIds.Contains(permission.id);
    }

    /// <summary>
    /// 根据当前宗门成员数计算该角色的可用名额。
    /// </summary>
    public int GetMaxCount(int memberCount)
    {
        if (baseSlots < 0) return int.MaxValue;
        if (membersPerExtraSlot <= 0) return baseSlots;
        return baseSlots + memberCount / membersPerExtraSlot;
    }
}
