using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;

namespace Cultiway.Content;

/// <summary>
/// 定义魔法体系的法杖施法要求，以及魔法师选择和打造武器时的法杖偏好。
/// </summary>
[Dependency(typeof(Cultisyses), typeof(SkillCastResources))]
public sealed class MagicStaffRules : ICanInit
{
    /// <summary>
    /// 将 mana 法术的法杖要求注册到 Core 施法前置条件链。
    /// </summary>
    public void Init()
    {
        SkillCastRequirements.Register(CheckStaffRequirement);
    }

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

    /// <summary>
    /// 返回角色语境下的装备比较价值；魔法师持有法杖时获得额外偏好分数。
    /// </summary>
    public static int ResolveEquipmentPreferenceValue(Item item, Actor actor)
    {
        if (item == null) return 0;
        var value = item.getValue();
        if (actor.GetExtend().HasCultisys<Magic>() && IsStaff(item.getAsset()))
            value += MagicSetting.MagicStaffWeaponPreferenceBonus;
        return value;
    }

    /// <summary>
    /// 尝试为魔法师打造价值更高的可负担法杖。
    /// 返回 true 表示已接管本次武器打造，result 表示是否实际打造成功。
    /// </summary>
    public static bool TryCraftPreferredStaff(Actor actor, string creatorName, int tries, City city,
        out bool result)
    {
        result = false;
        if (actor.isRekt() || city == null || !actor.GetExtend().HasCultisys<Magic>()) return false;

        var slot = actor.equipment.weapon;
        var current = slot.getItem();
        if (current != null && current.isCursed()) return true;

        var currentScore = ResolveEquipmentPreferenceValue(current, actor);
        EquipmentAsset selected = null;
        var selectedScore = currentScore;
        foreach (var candidate in AssetManager.items.pot_weapon_assets_all)
        {
            if (!IsStaff(candidate) || !CanAfford(actor, candidate, city)) continue;
            var score = candidate.equipment_value + candidate.mod_rank * 2 +
                        MagicSetting.MagicStaffWeaponPreferenceBonus;
            if (score <= selectedScore) continue;
            selected = candidate;
            selectedScore = score;
        }

        if (selected == null)
        {
            // 已经拿着法杖时阻止原版打造流程把它替换成普通武器；否则允许原版回退。
            return HasEquippedStaff(actor);
        }

        var crafted = World.world.items.generateItem(selected, actor.kingdom, creatorName, tries, actor);
        if (crafted == null) return true;

        if (current != null)
        {
            slot.takeAwayItem();
            city.tryToPutItem(current);
        }
        slot.setItem(crafted, actor);

        actor.spendMoney(selected.get_total_cost);
        if (selected.cost_resource_id_1 != "none")
            city.takeResource(selected.cost_resource_id_1, selected.cost_resource_1);
        if (selected.cost_resource_id_2 != "none")
            city.takeResource(selected.cost_resource_id_2, selected.cost_resource_2);

        result = true;
        return true;
    }

    /// <summary>
    /// 对魔法师自行支付 mana 的技能施法要求当前装备法杖。
    /// </summary>
    private static bool CheckStaffRequirement(ActorExtend caster, Entity skill,
        SkillCastFundingSource fundingSource)
    {
        // 卷轴、符箓等预付费载体自行承担施法媒介，不要求使用者手持法杖。
        if (fundingSource == SkillCastFundingSource.Prepaid) return true;
        if (!caster.HasCultisys<Magic>() || !MagicLearningRules.IsManaSkill(skill)) return true;
        return HasEquippedStaff(caster.Base);
    }
    /// <summary>
    /// 按原版打造规则检查个人金钱和城市资源是否足够。
    /// </summary>
    private static bool CanAfford(Actor actor, EquipmentAsset asset, City city)
    {
        if (!actor.hasEnoughMoney(asset.get_total_cost)) return false;
        if (asset.cost_resource_id_1 != "none" &&
            asset.cost_resource_1 > city.getResourcesAmount(asset.cost_resource_id_1)) return false;
        if (asset.cost_resource_id_2 != "none" &&
            asset.cost_resource_2 > city.getResourcesAmount(asset.cost_resource_id_2)) return false;
        return true;
    }
}
