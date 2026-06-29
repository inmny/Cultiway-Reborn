using System.Collections.Generic;
using Cultiway.Core;

namespace Cultiway.Core.Libraries;

/// <summary>
/// 宗门角色资产库，提供按槽位和人事显示顺序查询角色的能力。
/// </summary>
public class SectRoleLibrary : AssetLibrary<SectRoleAsset>
{
    private readonly Dictionary<SectRoleSlot, SectRoleAsset> _defaults = new();

    /// <summary>
    /// 注册角色资产，并缓存所属槽位的默认角色。
    /// </summary>
    public override SectRoleAsset add(SectRoleAsset pAsset)
    {
        SectRoleAsset result = base.add(pAsset);
        if (result.defaultForSlot)
        {
            _defaults[result.slot] = result;
        }

        return result;
    }

    /// <summary>
    /// 获取指定槽位的默认角色。
    /// </summary>
    public SectRoleAsset GetDefault(SectRoleSlot slot)
    {
        if (_defaults.TryGetValue(slot, out SectRoleAsset result)) return result;

        for (int i = 0; i < list.Count; i++)
        {
            SectRoleAsset role = list[i];
            if (role.slot == slot && role.defaultForSlot)
            {
                _defaults[slot] = role;
                return role;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取指定槽位下的所有角色，按 order 从高到低排序。
    /// </summary>
    public List<SectRoleAsset> GetRoles(SectRoleSlot slot)
    {
        var result = new List<SectRoleAsset>();
        for (int i = 0; i < list.Count; i++)
        {
            SectRoleAsset role = list[i];
            if (role.slot == slot)
            {
                result.Add(role);
            }
        }

        result.Sort((left, right) => right.order.CompareTo(left.order));
        return result;
    }

    /// <summary>
    /// 获取需要在人事列表中展示的角色，按 order 从高到低排序。
    /// </summary>
    public List<SectRoleAsset> GetPersonnelDisplayOrder()
    {
        var result = new List<SectRoleAsset>();
        for (int i = 0; i < list.Count; i++)
        {
            SectRoleAsset role = list[i];
            if (role.showInPersonnel)
            {
                result.Add(role);
            }
        }

        result.Sort((left, right) => right.order.CompareTo(left.order));
        return result;
    }
}
