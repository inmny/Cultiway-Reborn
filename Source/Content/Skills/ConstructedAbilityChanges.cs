using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>真实结果改变人物后，只重新采集受影响对象，不按每次命中扫描整个世界。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>当前共同轮次已经采集人物，可接收主线程变更通知。</summary>
    private bool actorsSampled;

    /// <summary>同一结果回调中多次改变的人物合并为一次重新采集。</summary>
    private readonly Dictionary<long, Actor> changedActors = new();

    /// <summary>在主线程通知人物已经发生瞬时位置、身体或控制变化，不扫过新旧姿态之间的路径。</summary>
    /// <param name="actor">已经由相应玩法确认改变的人物。</param>
    public static void NotifyActorChanged(Actor actor)
    {
        if (simulation == null || actor?.data == null) return;
        if (!simulation.actorsSampled)
        {
            if (simulation.history.ContainsKey(actor.data.id))
                simulation.history[actor.data.id] = (SpatialActorPose.Foot(actor), SpatialActorPose.Height(actor));
            return;
        }

        simulation.changedActors[actor.data.id] = actor;
    }

    /// <summary>原版接触回调内的变化由薄补丁通知，普通移动仍沿用正常世界采集。</summary>
    /// <param name="actor">原版刚更新的人物。</param>
    /// <param name="force">体质或创建变化需要完整重读，位置更新可先比较数值。</param>
    internal static void ObserveActorChange(Actor actor, bool force)
    {
        if (simulation?.confirmingContact != true || actor?.data == null) return;
        if (!force && simulation.actorIndices.TryGetValue(actor.data.id, out int index))
        {
            ActorMotion before = simulation.actors[index];
            if (before.To == PhysicsFoot(actor) && before.NativeScale == actor.actor_scale &&
                before.Appearance == actor.animation_container) return;
        }

        NotifyActorChanged(actor);
    }

    /// <summary>先更新改变对象的空间范围，再重采样已有连接；新生人物同轮加入作用筛选。</summary>
    /// <param name="clock">变化已经确认的共同世界时点。</param>
    private void RefreshChangedActors(double clock)
    {
        if (changedActors.Count == 0) return;
        bodyCasts.Clear();
        foreach (RunningCast running in casts)
            if (running.BodyControlled && running.Cast.Plan.Body.ControlsStance && running.LastWorldTime <= clock)
                bodyCasts[running.SourceId] = running;
        foreach (var change in changedActors)
        {
            Actor actor = change.Value;
            if (actor.data?.id != change.Key || actor.isRekt() || !actor.isAlive() || !actor.hasHealth()) continue;
            Vector3 foot = PhysicsFoot(actor);
            float height = SpatialActorPose.Height(actor);
            bodyCasts.TryGetValue(change.Key, out RunningCast body);
            // 确认后的瞬移或变形从新姿态继续，不把新旧位置之间的整段空间当作接触路径。
            Vector3 from = foot;
            float fromHeight = height;
            bool existing = actorIndices.TryGetValue(change.Key, out int index);
            if (existing && actors[index].To == foot)
            {
                ActorMotion before = actors[index];
                from = before.From;
                // 仅体质或控制改变不能把仍在移动的人物提前挪到帧末。
                if (before.Height == height) fromHeight = before.FromHeight;
            }

            var motion = new ActorMotion(actor, change.Key, from, foot, fromHeight, height, body);
            float reach = Mathf.Max(height, fromHeight) * (1f + (body?.Cast.Plan.Body.MaximumTorsoOffset ?? 0f) +
                                                           (body?.Cast.Plan.Body.Construction.FootworkTravel ?? 0f));
            var start = new AbilityCapsule(0, from, from, reach, null);
            var end = new AbilityCapsule(0, foot, foot, reach, null);
            Bounds bounds = AbilityContactIndex.Swept(start, end);
            if (existing)
            {
                actors[index] = motion;
                actorIndex.Update(index, bounds);
            }
            else
            {
                actorIndices.Add(change.Key, actors.Count);
                actors.Add(motion);
                actorIndex.Add(bounds);
            }
        }

        active.Clear();
        foreach (RunningCast running in casts)
        {
            ValidateSource(running);
            if (!running.Cast.Finished && running.LastWorldTime <= clock) active.Add(running);
        }

        AdvanceAll(clock, 0f, true);
        changedActors.Clear();
    }
}