using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

/// <summary>
///     魔法冥想行为：按月闭关修炼，参考仙道闭关范式。
///     每次冥想随机闭关 (等级+1)×(1~3) 个月，期间按精神力上限和先天神识资质持续积累精神力；
///     精神力满后结束冥想，实际进阶由统一进阶任务处理。不从地图抽取灵气，原地冥想即可。
/// </summary>
public class BehMagicMeditate : BehCityActor
{
    /// <summary>冥想收益和剩余闭关时间的更新间隔，单位为游戏秒。</summary>
    private const float TickInterval = 1f;

    /// <summary>推进一轮冥想，累积精神力，并在精神力满或本轮时长耗尽时退出任务。</summary>
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        ref var magic = ref ae.GetCultisys<Magic>();
        var max_spirit = pObject.stats[BaseStatses.MaxSpirit.id];
        var talent_multiplier = Mathf.Exp(ae.GetComponent<ValuableTalent>().DivineSense);

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

            // 主动冥想按精神力容量计算基础收益，先天神识只作为天赋乘子。
            if (magic.spirit < max_spirit)
            {
                var monthly_gain = max_spirit * MagicSetting.MeditateSpiritGainRatioPerMonth * talent_multiplier;
                var gain = Mathf.Min(
                    monthly_gain / TimeScales.SecPerMonth * TickInterval,
                    max_spirit - magic.spirit);
                magic.spirit += gain;
            }

            // 资源积累和进阶结算分离；满值后退出，让工作选择器调度统一进阶任务。
            if (magic.spirit >= max_spirit - 0.1f)
            {
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
