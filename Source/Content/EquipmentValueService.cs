using System;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Utils.Extension;

namespace Cultiway.Content;

/// <summary>在物品固有价值之上计算指定角色实际获得的装备价值。</summary>
public static class EquipmentValueService
{
    /// <summary>计算已经生成的装备对指定角色的价值。</summary>
    public static int ResolveItemValue(Item item, Actor actor)
    {
        if (item == null) return 0;
        return item.getValue() + ResolvePreferenceBonus(item.getAsset(), actor);
    }

    /// <summary>计算尚未生成的装备资产对指定角色的打造价值。</summary>
    public static int ResolveAssetValue(EquipmentAsset asset, Actor actor)
    {
        if (asset == null) return 0;
        return asset.equipment_value + asset.mod_rank * 2 + ResolvePreferenceBonus(asset, actor);
    }

    /// <summary>计算角色体系与武器类型匹配产生的偏好分。</summary>
    public static int ResolvePreferenceBonus(EquipmentAsset asset, Actor actor)
    {
        if (asset == null || asset.equipment_type != EquipmentType.Weapon) return 0;

        ActorExtend actorExtend = actor.GetExtend();
        int bonus = 0;
        if (actorExtend.HasCultisys<Magic>() && MagicStaffTools.IsStaff(asset))
            bonus += MagicSetting.MagicStaffWeaponPreferenceBonus;
        if (actorExtend.HasCultisys<Knight>() && MatchesMasteredKnightStyle(actorExtend, asset))
            bonus += KnightSetting.KnightWeaponPreferenceBonus;
        return bonus;
    }

    private static bool MatchesMasteredKnightStyle(ActorExtend actor, EquipmentAsset asset)
    {
        if (!actor.TryGetComponent(out KnightStyleMastery mastery)) return false;

        int knightLevel = actor.GetCultisys<Knight>().CurrLevel;
        for (var i = 0; i < mastery.style_ids.Length; i++)
        {
            KnightStyleAsset style = Libraries.Manager.KnightStyleLibrary.get(mastery.style_ids[i]);
            if (knightLevel >= style.MinimumKnightLevel &&
                Array.IndexOf(style.WeaponGroups, asset.group_id) >= 0) return true;
        }
        return false;
    }
}
