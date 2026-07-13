using System;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Progression;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
///     触发突破异象的工具类，负责写入状态组件和基础视觉状态。
/// </summary>
internal static class BreakthroughVisualTrigger
{
    /// <summary>
    ///     只响应仙道大境界提交；其他体系可通过同一生命周期入口注册自己的表现。
    /// </summary>
    public static void OnProgressionCommitted(ProgressionCommittedEvent evt)
    {
        if (evt.Cultisys != Cultisyses.Xian || evt.Kind != ProgressionKind.Major) return;
        TryTriggerXian(evt.Actor, evt.FromLevel, evt.ToLevel);
    }

    /// <summary>在表现系统启用且目标境界存在定义时初始化仙道突破视觉状态。</summary>
    private static void TryTriggerXian(ActorExtend ae, int fromLevel, int toLevel)
    {
        var manager = BreakthroughVisualManager.Instance;
        if (manager == null || !manager.Enabled) return;
        if (toLevel <= fromLevel) return;

        var def = manager.GetDefinition((byte)toLevel);
        if (def == null) return;

        ref var state = ref EnsureState(ae);
        state.last_level = (byte)Mathf.Clamp(fromLevel, 0, byte.MaxValue);
        state.visual_level = (byte)Mathf.Clamp(toLevel, 0, byte.MaxValue);
        state.visual_timer = def.Duration;
        state.rng_seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        state.flags = 0;

        EnsureRealmVisual(ae, (byte)toLevel);
    }

    /// <summary>取得角色的突破状态组件；缺失时先创建默认组件。</summary>
    private static ref XianBreakthroughState EnsureState(ActorExtend ae)
    {
        if (!ae.E.HasComponent<XianBreakthroughState>())
        {
            ae.E.AddComponent(new XianBreakthroughState());
        }

        return ref ae.E.GetComponent<XianBreakthroughState>();
    }

    /// <summary>创建或更新常驻境界视觉组件，使其与新境界及角色结构状态一致。</summary>
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
