using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

/// <summary>
///     魔法冥想行为：按月闭关修炼，参考仙道闭关范式。
///     每次冥想随机闭关 (等级+1)×(1~3) 个月，期间按 SpiritRegen（回神/月）持续积累精神力；
///     精神力满后突破境界。不从地图抽取灵气，原地冥想即可。
/// </summary>
public class BehMagicMeditate : BehCityActor
{
    private const float TickInterval = 1f;

    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        ref var magic = ref ae.GetCultisys<Magic>();
        var max_spirit = pObject.stats[BaseStatses.MaxSpirit.id];

        pObject.data.get(ContentActorDataKeys.MagicMeditateTime_float, out var time, -TimeScales.SecPerMonth);

        // 未开始本轮闭关：随机闭关时长 = (等级+1) × 随机(1~3) 月
        if (time <= -TimeScales.SecPerMonth)
        {
            time = (magic.CurrLevel + 1)
                   * Randy.randomFloat(MagicSetting.MeditateSessionMinMonths, MagicSetting.MeditateSessionMaxMonths)
                   * TimeScales.SecPerMonth;
        }

        if (time > 0)
        {
            time -= TickInterval;

            // 闭关期间按月持续积累精神力
            if (magic.spirit < max_spirit)
            {
                var gain = Mathf.Min(
                    Cultisyses.GetSpiritRegen(magic.CurrLevel) / TimeScales.SecPerMonth * TickInterval,
                    max_spirit - magic.spirit);
                magic.spirit += gain;
            }

            // 精神力满则突破
            if (Cultisyses.Magic.AllowUpgrade(ae))
            {
                pObject.changeHappiness(HappinessAssets.LevelUp.id);
                Cultisyses.Magic.TryPerformUpgrade(ae);
                magic.spirit = 0;
                pObject.data.set(ContentActorDataKeys.MagicMeditateTime_float, -TimeScales.SecPerMonth);
                return BehResult.Continue;
            }

            if (time > 0f)
            {
                pObject.data.set(ContentActorDataKeys.MagicMeditateTime_float, time);
                pObject.timer_action = TickInterval;
                return BehResult.RepeatStep;
            }
        }

        // 本轮闭关时长用尽，等待 job 循环开启下一轮随机闭关
        pObject.data.set(ContentActorDataKeys.MagicMeditateTime_float, -TimeScales.SecPerMonth);
        return BehResult.Continue;
    }
}
