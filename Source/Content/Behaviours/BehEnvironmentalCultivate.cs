using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>在选定地块持续分派主修功法的环境修炼结算。</summary>
public sealed class BehEnvironmentalCultivate : BehaviourActionActor
{
    private const float TickInterval = 1f;

    [Hotfixable]
    public override BehResult execute(Actor pActor)
    {
        var actor = pActor.GetExtend();
        var method = actor.GetMainCultibook()?.GetCultivateMethod();
        if (method?.EnvironmentRule == null) return BehResult.Continue;

        pActor.data.get(ContentActorDataKeys.CultivateTime_float, out float time, -TimeScales.SecPerMonth);
        ref Xian xian = ref actor.GetCultisys<Xian>();
        if (time <= -TimeScales.SecPerMonth)
            time = (xian.CurrLevel + 1) * TimeScales.SecPerMonth;

        if (time > 0f)
        {
            time -= TickInterval;
            var context = new CultivationTriggerContext(
                actor,
                CultivationTriggerKind.ActiveTick,
                CultivationActivityKind.EnvironmentalMeditation,
                TickInterval);
            CultivateMethods.TryDispatch(in context);

            if (pActor.isAlive() && xian.wakan < pActor.stats[BaseStatses.MaxWakan.id] && time > 0f)
            {
                pActor.data.set(ContentActorDataKeys.CultivateTime_float, time);
                pActor.timer_action = TickInterval;
                return BehResult.RepeatStep;
            }
        }

        pActor.data.set(ContentActorDataKeys.CultivateTime_float, -TimeScales.SecPerMonth);
        return BehResult.Continue;
    }
}
