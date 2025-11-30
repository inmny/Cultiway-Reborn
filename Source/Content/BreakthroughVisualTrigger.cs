using System;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
///     触发突破异象的工具类，负责写入状态组件和基础视觉状态。
/// </summary>
internal static class BreakthroughVisualTrigger
{
    public static void TryTriggerXian(ActorExtend ae, int fromLevel, int toLevel)
    {
        ModClass.LogInfo($"TryTriggerXian {ae.Base.name}({ae.Base.id}), {fromLevel} -> {toLevel}");
        var manager = BreakthroughVisualManager.Instance;
        if (manager == null || !manager.Enabled) return;
        if (toLevel <= fromLevel) return;

        var def = manager.GetDefinition((byte)toLevel);
        if (def == null) return;
        ModClass.LogInfo($"Def: {def.ToLevel}");

        ref var state = ref EnsureState(ae);
        state.last_level = (byte)Mathf.Clamp(fromLevel, 0, byte.MaxValue);
        state.visual_level = (byte)Mathf.Clamp(toLevel, 0, byte.MaxValue);
        state.visual_timer = def.Duration;
        state.rng_seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        state.flags = 0;

        EnsureRealmVisual(ae, (byte)toLevel);
    }

    private static ref XianBreakthroughState EnsureState(ActorExtend ae)
    {
        if (!ae.E.HasComponent<XianBreakthroughState>())
        {
            ae.E.AddComponent(new XianBreakthroughState());
        }

        return ref ae.E.GetComponent<XianBreakthroughState>();
    }

    private static void EnsureRealmVisual(ActorExtend ae, byte targetLevel)
    {
        var rvManager = RealmVisualManager.Instance;
        if (rvManager == null) return;

        var def = rvManager.GetDefinitionForLevel(targetLevel + 1); // 视觉层级比境界编号高半级，确保新定义可见
        if (!ae.E.HasComponent<RealmVisual>())
        {
            ae.E.AddComponent(new RealmVisual
            {
                definition_index = def?.Index ?? byte.MaxValue,
                realm_stage = targetLevel,
                visual_state = RealmVisual.VisualStateBreakthrough,
                has_element_root = ae.HasElementRoot()
            });
        }
        else
        {
            ref var visual = ref ae.E.GetComponent<RealmVisual>();
            if (def != null)
            {
                visual.definition_index = def.Index;
                visual.realm_stage = (byte)Mathf.Clamp(def.RealmLevel, 0, byte.MaxValue);
                visual.has_element_root = ae.HasElementRoot();
            }
            visual.visual_state = RealmVisual.VisualStateBreakthrough;

            visual.indicator_flags = 0;
            if (ae.E.HasComponent<Yuanying>())
            {
                visual.indicator_flags |= RealmVisual.IndicatorFlagYuanying;
            }
            else if (ae.E.HasComponent<Jindan>())
            {
                visual.indicator_flags |= RealmVisual.IndicatorFlagJindan;
            }
        }
    }
}
