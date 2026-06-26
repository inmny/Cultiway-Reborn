using ai.behaviours;
using Cultiway.Content.Const;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 野外闭关等待行为，在闭关期间为可见单位低频显示地面灵气特效。
/// </summary>
public class BehOutdoorCultivationWait : BehaviourActionActor
{
    private const float EffectIntervalMin = 1.5f;
    private const float EffectIntervalMax = 2.5f;
    private const float TickInterval = 1f;

    private readonly float _minDuration;
    private readonly float _maxDuration;

    public BehOutdoorCultivationWait(float minDuration, float maxDuration)
    {
        _minDuration = minDuration;
        _maxDuration = maxDuration;
    }

    [Hotfixable]
    public override BehResult execute(Actor pActor)
    {
        var now = (float)World.world.map_stats.world_time;
        pActor.data.get(ContentActorDataKeys.OutdoorCultivationEndTime_float, out var endTime, -1f);

        if (endTime <= now)
        {
            if (endTime > 0f)
            {
                ClearCultivationTimers(pActor);
                return BehResult.Continue;
            }

            endTime = now + Randy.randomFloat(_minDuration, _maxDuration);
            pActor.data.set(ContentActorDataKeys.OutdoorCultivationEndTime_float, endTime);
        }

        TrySpawnVisibleCultivationEffect(pActor, now);
        pActor.timer_action = TickInterval;
        return BehResult.RepeatStep;
    }

    /// <summary>
    /// 清理本次野外闭关的临时计时数据，避免下一轮任务沿用旧时间。
    /// </summary>
    public static void ClearCultivationTimers(Actor actor)
    {
        actor.data.set(ContentActorDataKeys.OutdoorCultivationEndTime_float, -1f);
        actor.data.set(ContentActorDataKeys.NextOutdoorCultivationEffectTime_float, -1f);
    }

    /// <summary>
    /// 仅在单位可见且特效冷却结束时显示一次地面灵气特效。
    /// </summary>
    public static void TrySpawnVisibleCultivationEffect(Actor actor, float now)
    {
        if (!actor.is_visible) return;
        if (actor.isInsideSomething()) return;

        actor.data.get(ContentActorDataKeys.NextOutdoorCultivationEffectTime_float, out var nextEffectTime, -1f);
        if (nextEffectTime > now) return;

        var scale = Mathf.Clamp(actor.stats["scale"], 0.35f, 0.8f);
        EffectsLibrary.spawnAt("fx_cast_ground_blue", actor.current_position, scale);

        var nextTime = now + Randy.randomFloat(EffectIntervalMin, EffectIntervalMax);
        actor.data.set(ContentActorDataKeys.NextOutdoorCultivationEffectTime_float, nextTime);
    }
}
