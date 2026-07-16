using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 骑士操练行为：原地按月度收益折算到每 tick 积累斗气，比战斗慢；斗气满后退出任务，
/// 实际突破由 KnightBreakthroughSystem 月度结算。仅在和平期被分配（战时不练）。
/// </summary>
public class BehKnightTrain : BehCityActor
{
    private const float TickInterval = 1f;

    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        ref var knight = ref ae.GetCultisys<Knight>();
        var maxVigor = pObject.stats[BaseStatses.MaxVigor.id];
        if (maxVigor <= 0f) return BehResult.Stop;

        // 斗气已蓄满：退出操练，等月度突破系统结算
        if (knight.vigor >= maxVigor - 0.1f) return BehResult.Continue;

        var monthly_gain = maxVigor * KnightSetting.PracticeVigorGainRatioPerMonth;
        var gain = Mathf.Min(monthly_gain / TimeScales.SecPerMonth * TickInterval, maxVigor - knight.vigor);
        knight.vigor += gain;

        pObject.timer_action = TickInterval;
        return BehResult.RepeatStep;
    }
}
