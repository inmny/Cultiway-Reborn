using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

/// <summary>
///     魔法冥想行为：每秒固定收益积累精神力，精神力满后突破境界。
///     不从地图抽取（与仙道闭关抽灵气不同），原地冥想即可。
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

        // 固定收益积累精神力（不从地图抽取）
        if (magic.spirit < max_spirit)
        {
            var gain = Mathf.Min(MagicSetting.MeditateBaseGain * TickInterval, max_spirit - magic.spirit);
            magic.spirit += gain;
        }

        // 精神力满则突破
        if (Cultisyses.Magic.AllowUpgrade(ae))
        {
            pObject.changeHappiness(HappinessAssets.LevelUp.id);
            Cultisyses.Magic.TryPerformUpgrade(ae);
            magic.spirit = 0;
            return BehResult.Continue;
        }

        if (!Cultisyses.Magic.PreCheckUpgrade(ae)) return BehResult.Continue;
        pObject.timer_action = TickInterval;
        return BehResult.RepeatStep;
    }
}
