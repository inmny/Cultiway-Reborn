using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.KnightCombat;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 结算一个月的训练假人操练：增加斗气，并按当前真实武器解锁匹配流派。
/// </summary>
public class BehKnightTrain : BehCityActor
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        ref var knight = ref ae.GetCultisys<Knight>();
        var maxVigor = pObject.stats[BaseStatses.MaxVigor.id];
        if (maxVigor <= 0f)
        {
            ModClass.LogInfo(
                $"[BehKnightTrain] actor={pObject.getName()}[{pObject.data.id}] stop=max_vigor_non_positive maxVigor={maxVigor:0.##}");
            return BehResult.Stop;
        }

        var vigorBefore = knight.vigor;
        var monthly_gain = maxVigor * KnightSetting.PracticeVigorGainRatioPerMonth;
        knight.vigor = Mathf.Min(knight.vigor + monthly_gain, maxVigor);

        var skillSystemsEnabled = GeneralSettings.EnableSkillSystems;
        var weaponResolved = KnightTechniqueAccessService.TryResolveWeapon(
            ae, out Item weapon, out EquipmentAsset weaponAsset);

        if (skillSystemsEnabled && weaponResolved)
        {
            var guardianMatched = KnightStyles.Guardian.MatchesEquipment(ae, weapon, weaponAsset);
            var lancerMatched = KnightStyles.Lancer.MatchesEquipment(ae, weapon, weaponAsset);
            var duelistMatched = KnightStyles.Duelist.MatchesEquipment(ae, weapon, weaponAsset);

            if (guardianMatched)
            {
                KnightStyleMasteryService.Master(ae, KnightStyles.Guardian);
                KnightTechniqueSkills.LearnStyle(ae, KnightStyles.Guardian);
            }
            if (lancerMatched)
            {
                KnightStyleMasteryService.Master(ae, KnightStyles.Lancer);
                KnightTechniqueSkills.LearnStyle(ae, KnightStyles.Lancer);
            }
            if (duelistMatched)
            {
                KnightStyleMasteryService.Master(ae, KnightStyles.Duelist);
                KnightTechniqueSkills.LearnStyle(ae, KnightStyles.Duelist);
            }
        }

        var result = knight.vigor < maxVigor - 0.1f ? BehResult.RestartTask : BehResult.Continue;
        return result;
    }
}
