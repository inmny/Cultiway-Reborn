using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>真实事件开始有限部位过程；等待及开始状态保存在同一数值快照中。</summary>
public sealed partial class AbilityCast
{
    /// <summary>复用的开始队列，每部位每次施放最多入队开始一次。</summary>
    private readonly int[] birthQueue;

    /// <summary>本轮待开始队列的读写位置，完整推进边界均清空。</summary>
    private int birthHead, birthTail;

    /// <summary>身体自然收势只通知一次，不将中断当作正常完成。</summary>
    private bool bodyEndSent, savedBodyEndSent;

    /// <summary>一次同步派发内的事件队列，避免材料链递归及读取后来的材料。</summary>
    private readonly Queue<ConditionEvent> conditionEvents;

    /// <summary>是否正在按序通知事件。</summary>
    private bool dispatchingEvents;

    /// <summary>事件发生时的完整事实，不在通知途中改读来源的新状态。</summary>
    private readonly struct ConditionEvent
    {
        /// <summary>真实来源及事件种类。</summary>
        internal readonly int Source;

        /// <summary>本次已经发生的事件。</summary>
        internal readonly ConstructionChangeTrigger Trigger;

        /// <summary>真实位置及朝向。</summary>
        internal readonly Vector3 Point;

        /// <summary>事件当时的朝向。</summary>
        internal readonly Quaternion Rotation;

        /// <summary>事件当时的实际材料。</summary>
        internal readonly AbilityMatter Matter;

        /// <summary>数值事件发生前后，普通事件不使用。</summary>
        internal readonly float Before, After;

        /// <summary>复制已经确认的事实。</summary>
        /// <param name="source">实际来源。</param>
        /// <param name="trigger">事件种类。</param>
        /// <param name="point">实际位置。</param>
        /// <param name="rotation">实际朝向。</param>
        /// <param name="matter">实际材料。</param>
        /// <param name="before">真实变化前值。</param>
        /// <param name="after">真实变化后值。</param>
        internal ConditionEvent(int source, ConstructionChangeTrigger trigger, Vector3 point, Quaternion rotation,
            AbilityMatter matter, float before, float after)
        {
            Source = source;
            Trigger = trigger;
            Point = point;
            Rotation = rotation;
            Matter = matter;
            Before = before;
            After = after;
        }
    }

    /// <summary>部位自己已经运行的世界秒数，等待时为负一。</summary>
    /// <param name="node">展开部位。</param>
    public float NodeAge(int node) => nodes[node].Started ? Age - nodes[node].StartedAt : -1f;

    /// <summary>用来源与事件直接取得监听者，不遍历无关部位或访问游戏对象。</summary>
    /// <param name="source">实际来源，负一表示身体。</param>
    /// <param name="trigger">已经确认的事件。</param>
    /// <param name="point">该事件真正发生的位置。</param>
    /// <param name="before">真实数值变化前。</param>
    /// <param name="after">真实数值变化后。</param>
    private void SignalEvent(int source, ConstructionChangeTrigger trigger, Vector3 point, float before = 0f,
        float after = 0f)
    {
        if (Plan.EventListeners.Length == 0 || cancelled || Age >= Plan.Lifetime) return;
        conditionEvents.Enqueue(new ConditionEvent(source, trigger, point,
            source < 0 ? Quaternion.LookRotation(bodyFrame.Forward, Vector3.forward) : nodes[source].Transform.rotation,
            source < 0 ? null : nodes[source].Matter, before, after));
        if (dispatchingEvents) return;
        dispatchingEvents = true;
        while (conditionEvents.Count > 0)
        {
            ConditionEvent fact = conditionEvents.Dequeue();
            int bucket = (fact.Source < 0 ? nodes.Length : fact.Source) * CompiledConstructionCondition.EventCount +
                (int)fact.Trigger - 1;
            foreach (int index in Plan.EventListeners[bucket])
            {
                if (index < 0)
                {
                    ReceiveOperation(~index, fact.Matter, fact.Before, fact.After, fact.Point);
                    continue;
                }

                ref NodeState state = ref nodes[index];
                CompiledConstructionActivation gate = Plan.Nodes[index].Activation;
                if (!state.Exists || !float.IsPositiveInfinity(state.BirthAt) ||
                    !gate.Crossed(fact.Before, fact.After) ||
                    gate.Matter != null && !ReferenceEquals(gate.Matter, fact.Matter)) continue;
                state.BirthAt = Age + gate.Delay;
                if (gate.Placement == ConstructionEventPlacement.Event)
                {
                    state.Free = true;
                    state.EventPlaced = true;
                    state.EventPosition = fact.Point;
                    state.EventRotation = fact.Rotation;
                }

                QueueBirth(index);
            }
        }

        dispatchingEvents = false;
    }

    /// <summary>只有实际来源和时间均已就绪的部位才进入本轮开始队列。</summary>
    /// <param name="index">等待部位。</param>
    private void QueueBirth(int index)
    {
        ref NodeState state = ref nodes[index];
        if (!CanStart(index) || state.Queued) return;
        state.Queued = true;
        birthQueue[birthTail++] = index;
    }

    /// <summary>检查已确认条件和真实父体，不让附着分支借用尚未出现的姿态。</summary>
    /// <param name="index">准备开始的部位。</param>
    private bool CanStart(int index)
    {
        NodeState state = nodes[index];
        CompiledForm form = Plan.Nodes[index];
        return state.Exists && !state.Started && state.BirthAt <= Age &&
               (form.RemainderRoot < 0 || form.RemainderRoot == index || nodes[form.RemainderRoot].Started) &&
               (state.Free || form.Parent < 0 ||
                nodes[form.Parent].Started && nodes[form.Parent].Exists && AnchorBuilt(index));
    }

    /// <summary>实际部位尚未形成时不借用未来挂点；独立和事件放置遵守原规则。</summary>
    /// <param name="index">当前子部位。</param>
    private bool AnchorBuilt(int index)
    {
        CompiledForm form = Plan.Nodes[index];
        NodeState state = nodes[index];
        if (Plan.NeedsRayTarget && !state.Free && !state.EventPlaced)
            for (int parent = form.Parent; parent >= 0; parent = Plan.Nodes[parent].Parent)
            {
                NodeState ancestor = nodes[parent];
                if (Plan.Nodes[parent].HasRay && (ancestor.Ray == null || ancestor.RayLength < ancestor.Ray.Span.Radius * 2f)) return false;
                if (ancestor.Free || ancestor.EventPlaced) break;
            }
        return state.Free || state.EventPlaced || form.Parent < 0 ||
               (poses[form.Parent]?.BuildProgress ?? 1f) >= form.AnchorBuild &&
               (form.AnchorSurface == null || form.AnchorSurface.Ready(poses[form.Parent]));
    }

    /// <summary>开始已经就绪的部位，普通后代继承该分支时钟。</summary>
    /// <param name="index">实际开始的部位。</param>
    private void StartNode(int index)
    {
        ref NodeState state = ref nodes[index];
        CompiledForm form = Plan.Nodes[index];
        float parentStart = form.Parent < 0 || state.EventPlaced ? 0f : nodes[form.Parent].StartedAt;
        if (form.RemainderRoot >= 0 && form.RemainderRoot != index)
            parentStart = Mathf.Max(parentStart, nodes[form.RemainderRoot].StartedAt);
        state.StartedAt = form.Activation == null ? parentStart : Mathf.Max(state.BirthAt, parentStart);
        if (form.AnchorBuild > 0f || form.Parent >= 0 && Plan.Nodes[form.Parent].HasRay) state.StartedAt = Mathf.Max(state.StartedAt, Age);
        state.Started = true;
        state.Revision++;
        foreach (int child in form.Children) QueueBirth(child);
    }

    /// <summary>只采样刚开始的部位，不重采样已推进对象而破坏真实扫掠前态。</summary>
    /// <param name="target">本轮实际目标数值。</param>
    private void FlushBirths(Vector3 target)
    {
        while (birthHead < birthTail)
        {
            int index = birthQueue[birthHead++];
            nodes[index].Queued = false;
            if (CanStart(index)) SampleNode(index, 0f, target, false);
        }

        birthHead = birthTail = 0;
    }

    /// <summary>结束事件只发出一次，后续清理不会重复生成新部位。</summary>
    /// <param name="node">结束部位。</param>
    /// <param name="point">真正结束的位置。</param>
    private void SignalEnd(int node, Vector3 point)
    {
        if (!nodes[node].Started || nodes[node].EndSent) return;
        nodes[node].EndSent = true;
        if (!cancelled && Age < Plan.Lifetime) TransferRemainder(node);
        SignalEvent(node, ConstructionChangeTrigger.End, point);
    }

    /// <summary>来源已失去产生事件的可能，结束还没有收到条件的监听者。</summary>
    /// <param name="source">已经结束或中断的来源，负一为身体。</param>
    private void CancelAwaiters(int source)
    {
        if (Plan.EventListeners.Length == 0) return;
        int bucket = (source < 0 ? nodes.Length : source) * CompiledConstructionCondition.EventCount;
        for (int eventKind = 0; eventKind < CompiledConstructionCondition.EventCount; eventKind++)
            foreach (int index in Plan.EventListeners[bucket + eventKind])
                if (index >= 0 && nodes[index].Exists && float.IsPositiveInfinity(nodes[index].BirthAt))
                    EndNode(index);
    }

    /// <summary>在真实交锋处通知变化及后续部位，耗尽时仍能留下独立结果。</summary>
    /// <param name="node">已经参与交锋的部位。</param>
    /// <param name="point">共同时间确认的接触位置。</param>
    /// <param name="spent">随后确实消耗的作用量。</param>
    internal void ConfirmClash(int node, Vector3 point, float spent)
    {
        RecordActionContact(node, point, AbilityActionContactKind.Ability);
        if (fractures != null && Plan.Nodes[node].Fracture != null) fractures[node].AwaitingSpend = spent > 0f;
        nodes[node].HasClashPoint = true;
        nodes[node].ClashPoint = point;
        ApplyMatterChanges(node, ConstructionChangeTrigger.Clash);
        SignalEvent(node, ConstructionChangeTrigger.Clash, point);
        FlushBirths(inputs.Target);
        RecordActionTimeline();
    }
}
