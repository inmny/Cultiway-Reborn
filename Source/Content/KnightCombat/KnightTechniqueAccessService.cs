using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.ActiveAbilities;

namespace Cultiway.Content.KnightCombat;

/// <summary>骑士战技的统一身份、等级、掌握和当前装备准入判断。</summary>
public static class KnightTechniqueAccessService
{
    /// <summary>判断角色是否已经满足战技的领域解锁条件。</summary>
    public static bool IsUnlocked(ActorExtend actor, KnightTechniqueAsset technique)
    {
        return GeneralSettings.EnableSkillSystems &&
               actor.HasCultisys<Knight>() &&
               KnightStyleMasteryService.IsMastered(actor, technique.Style) &&
               actor.GetCultisys<Knight>().CurrLevel >= technique.MinimumKnightLevel;
    }

    /// <summary>
    /// 解析当前真实武器，并判断其是否同时满足流派和战技的装备条件。
    /// 这是后续主动能力入口的统一当前武器解析方法。
    /// </summary>
    public static bool TryResolveCurrentWeapon(
        ActorExtend actor,
        KnightTechniqueAsset technique,
        out Item weapon,
        out EquipmentAsset weaponAsset)
    {
        weapon = null;
        weaponAsset = null;
        if (!IsUnlocked(actor, technique) || !TryResolveWeapon(actor, out weapon, out weaponAsset)) return false;
        return TryCreateContext(
            actor,
            technique,
            weapon,
            weaponAsset,
            null,
            default,
            out _);
    }

    /// <summary>使用已经解析的武器构造一次战技回调上下文。</summary>
    public static bool TryCreateContext(
        ActorExtend actor,
        KnightTechniqueAsset technique,
        Item weapon,
        EquipmentAsset weaponAsset,
        BaseSimObject target,
        ActiveAbilityTarget activeTarget,
        out KnightTechniqueContext context)
    {
        context = default;
        if (!IsUnlocked(actor, technique) || !technique.Style.MatchesEquipment(actor, weapon, weaponAsset))
            return false;

        context = new KnightTechniqueContext(
            actor,
            technique,
            weapon,
            weaponAsset,
            target,
            in activeTarget);
        return technique.MeetsEquipmentCondition(context);
    }

    /// <summary>解析当前可作为骑士战技载体的真实武器。</summary>
    public static bool TryResolveWeapon(
        ActorExtend actor,
        out Item weapon,
        out EquipmentAsset weaponAsset)
    {
        weapon = null;
        weaponAsset = null;
        if (actor == null || actor.Base.isRekt() || !actor.Base.hasWeapon()) return false;

        weapon = actor.Base.getWeapon();
        weaponAsset = actor.Base.getWeaponAsset();
        return weapon != null && weapon.isAlive() && !weapon.isBroken() &&
               weaponAsset != null && weaponAsset.equipment_type == EquipmentType.Weapon;
    }
}
