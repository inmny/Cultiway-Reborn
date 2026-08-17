using strings;

namespace Cultiway.Content;

/// <summary>
/// 提供法杖识别。
/// </summary>
public static class MagicStaffTools
{
    /// <summary>
    /// 判断单位当前武器槽是否装备了原版法杖分组中的武器。
    /// </summary>
    public static bool HasEquippedStaff(Actor actor)
    {
        return !actor.isRekt() && actor.hasWeapon() && IsStaff(actor.getWeaponAsset());
    }

    /// <summary>
    /// 判断装备资产是否属于原版法杖分组。
    /// </summary>
    public static bool IsStaff(EquipmentAsset asset)
    {
        return asset != null && asset.equipment_type == EquipmentType.Weapon &&
               asset.group_id == S_EquipmentGroup.staff;
    }

}
