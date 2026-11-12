using System.Collections.Generic;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>在共同时间中记录动作事实；预测只写本地记录，确认后由主线程取走。</summary>
public sealed partial class AbilityCast
{
    /// <summary>尚未交付的本次确认记录，交付后复用容量。</summary>
    private readonly List<AbilityActionEvent> actionEvents = new();

    /// <summary>真实身体本次已经经过的阶段。</summary>
    private byte bodyActionStages;

    /// <summary>预测前的身体阶段及记录数量。</summary>
    private byte savedBodyActionStages;

    /// <summary>预测前已有事实的数量。</summary>
    private int savedActionEventCount;

    /// <summary>没有新事实时主线程不租用交付缓冲。</summary>
    internal bool HasActionEvents => actionEvents.Count != 0;

    /// <summary>只在主线程确认后调用；同一批事实取走后不会再次交付。</summary>
    /// <param name="output">调用方复用的结果集合。</param>
    public void DrainActionEvents(ICollection<AbilityActionEvent> output)
    {
        foreach (AbilityActionEvent fact in actionEvents) output.Add(fact);
        actionEvents.Clear();
    }

    /// <summary>身体采用自己的阶段，独立部位则按当前真实作用窗口汇总。</summary>
    private AbilityCastPhase ResolveActionPhase()
    {
        if (ControlsBody)
        {
            if (bodyResponse.HasValue) return AbilityCastPhase.Recovering;
            return Age < Plan.Body.ReleaseAt ? AbilityCastPhase.Preparing :
                Age < Plan.Body.ActiveFrom ? AbilityCastPhase.Releasing :
                Age < Plan.Body.ActiveUntil ? AbilityCastPhase.Active : AbilityCastPhase.Recovering;
        }
        bool released = false, recovering = false;
        for (int i = 0; i < nodes.Length; i++)
        {
            if (!nodes[i].Exists || !nodes[i].Started) continue;
            if (nodes[i].Visible && ProcessIsActive(i, false)) return AbilityCastPhase.Active;
            released |= (nodes[i].ActionStages & (1 << (int)AbilityActionEventKind.Released)) != 0;
            recovering |= (nodes[i].ActionStages & (1 << (int)AbilityActionEventKind.Recovering)) != 0;
        }

        return recovering ? AbilityCastPhase.Recovering :
            released ? AbilityCastPhase.Releasing : AbilityCastPhase.Preparing;
    }

    /// <summary>同一阶段每次动作只记一次，结束及中断互不重放。</summary>
    /// <param name="node">实际身体或部位。</param><param name="kind">经过的阶段。</param>
    private void RecordActionStage(int node, AbilityActionEventKind kind)
    {
        ref byte sent = ref (node < 0 ? ref bodyActionStages : ref nodes[node].ActionStages);
        int ended = (1 << (int)AbilityActionEventKind.Completed) | (1 << (int)AbilityActionEventKind.Interrupted);
        int bit = 1 << (int)kind;
        if ((sent & ended) != 0 || (sent & bit) != 0) return;
        sent |= (byte)bit;
        RecordActionEvent(node, kind, null, node < 0 ? BodyPose.Value.Hand : nodes[node].Transform.GetColumn(3));
    }

    /// <summary>只追加已经确定的数值记录，不调用外部订阅者。</summary>
    /// <param name="node">实际部位。</param><param name="kind">事实种类。</param>
    /// <param name="marker">可选制作标记。</param><param name="point">实际位置。</param>
    /// <param name="contactKind">实际接触类别。</param><param name="targetId">实际人物编号。</param>
    private void RecordActionEvent(int node, AbilityActionEventKind kind, string marker, Vector3 point,
        AbilityActionContactKind contactKind = AbilityActionContactKind.None, long targetId = 0)
    {
        FormMotion motion = node < 0 ? null : nodes[node].MotionData?.Motion ?? Plan.Nodes[node].Motion;
        int instance = node < 0 ? 0 : nodes[node].ActionInstance;
        var fact = new AbilityActionEvent(node, instance, kind, motion?.Id, marker, Age, point, contactKind, targetId,
            node < 0 ? BodyResult : null);
        int at = actionEvents.Count;
        // 只调整同一动作同一时刻的末端接触，不按阶段名称重排不同部位的真实因果。
        if (kind == AbilityActionEventKind.Contact)
            for (int i = 0; i < actionEvents.Count; i++)
            {
                AbilityActionEvent prior = actionEvents[i];
                if (prior.Node == node && prior.Instance == instance && prior.Age == Age &&
                    prior.Kind is AbilityActionEventKind.Recovering or AbilityActionEventKind.Completed
                        or AbilityActionEventKind.Interrupted)
                {
                    at = i;
                    break;
                }
            }

        actionEvents.Insert(at, fact);
    }

    /// <summary>实际接触独立于制作标记，可按真实重复规则确认多个目标。</summary>
    /// <param name="node">实际部位。</param><param name="point">实际接触位置。</param>
    /// <param name="kind">实际接触类别。</param><param name="targetId">实际人物编号。</param>
    internal void RecordActionContact(int node, Vector3 point, AbilityActionContactKind kind, long targetId = 0) =>
        RecordActionEvent(node, AbilityActionEventKind.Contact, null, point, kind, targetId);

    /// <summary>完成取样、条件出生与步骤更新后，从同一时钟记录身体和部位阶段。</summary>
    private void RecordActionTimeline()
    {
        if (Plan.Body != null && !bodyInterrupted)
        {
            RecordActionStage(-1, AbilityActionEventKind.Preparing);
            if (!bodyResponse.HasValue)
            {
                if (Age >= Plan.Body.ReleaseAt) RecordActionStage(-1, AbilityActionEventKind.Released);
                if (Age >= Plan.Body.ActiveFrom) RecordActionStage(-1, AbilityActionEventKind.Active);
                if (Age >= Plan.Body.ActiveUntil) RecordActionStage(-1, AbilityActionEventKind.Recovering);
            }
            if (Age >= BodyUntil) RecordActionStage(-1, AbilityActionEventKind.Completed);
        }

        for (int i = 0; i < nodes.Length; i++)
        {
            ref NodeState state = ref nodes[i];
            if (!state.Exists || !state.Started) continue;
            CompiledForm form = Plan.Nodes[i];
            RecordActionStage(i, AbilityActionEventKind.Preparing);
            if (state.Visible && ProcessIsActive(i, true))
            {
                RecordActionStage(i, AbilityActionEventKind.Released);
                RecordActionStage(i, AbilityActionEventKind.Active);
            }

            FormMotion motion = state.MotionData?.Motion ?? form.Motion;
            if (motion != null && (state.Motion != null || NodeAge(i) >= form.MotionAt))
            {
                while (state.NextActionMarker < motion.StageMarkers.Length &&
                       motion.StageMarkers[state.NextActionMarker].Value <= state.MotionAge)
                {
                    string marker = motion.StageMarkers[state.NextActionMarker++].Key;
                    RecordActionEvent(i, AbilityActionEventKind.Marker, marker, state.Transform.GetColumn(3));
                }
            }

            if ((state.ActionStages & (1 << (int)AbilityActionEventKind.Active)) != 0 &&
                !ProcessIsActive(i, false, true))
                RecordActionStage(i, AbilityActionEventKind.Recovering);

            if (motion != null && (state.Motion != null || NodeAge(i) >= form.MotionAt))
            {
                if (!motion.Loop && state.MotionAge >= motion.Duration)
                    RecordActionStage(i, AbilityActionEventKind.Completed);
                else if (state.MotionStopped) RecordActionStage(i, AbilityActionEventKind.Interrupted);
            }

            if (Age >= Plan.Lifetime || NodeAge(i) >= form.Presence.y)
                RecordActionStage(i, AbilityActionEventKind.Completed);
        }
    }
}
