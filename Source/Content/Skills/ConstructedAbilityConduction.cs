using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.EventSystem.Systems;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Effects;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>真实水路电击的完整确认，不冒充主体直接命中。</summary>
public readonly struct ConstructedConduction
{
    /// <summary>原施放、原来源和当前控制者。</summary>
    public readonly long CastId, OriginalSourceId, OwnerId;

    /// <summary>稳定构造编号和真实部位。</summary>
    public readonly string AbilityId;

    /// <summary>实际部位。</summary>
    public readonly int Node;

    /// <summary>已经确认的世界时点。</summary>
    public readonly double WorldTime;

    /// <summary>实际到达的水柱中心，仍使用真实水面高度。</summary>
    public readonly Vector3[] Route;

    /// <summary>实际进入伤害接口的人物及结算前后的生命值。</summary>
    public readonly long[] Targets;

    /// <summary>人物作用前后生命值，包含原有抗性和防御。</summary>
    public readonly float[] HealthBefore, HealthAfter;

    /// <summary>已经实际扣除的作用量与尚存余力。</summary>
    public readonly float Spent, Remaining;

    /// <summary>在主线程完成确认后发布本次结果。</summary>
    /// <param name="cast">施放编号。</param><param name="original">原来源。</param><param name="owner">当前控制者。</param>
    /// <param name="ability">构造编号。</param><param name="node">实际部位。</param><param name="clock">实际时点。</param>
    /// <param name="route">实际水路。</param><param name="targets">实际作用人物。</param><param name="before">作用前生命值。</param>
    /// <param name="after">作用后生命值。</param><param name="spent">消耗。</param><param name="remaining">剩余。</param>
    internal ConstructedConduction(long cast, long original, long owner, string ability, int node, double clock,
        Vector3[] route, long[] targets, float[] before, float[] after, float spent, float remaining)
    {
        CastId = cast;
        OriginalSourceId = original;
        OwnerId = owner;
        AbilityId = ability;
        Node = node;
        WorldTime = clock;
        Route = route;
        Targets = targets;
        HealthBefore = before;
        HealthAfter = after;
        Spent = spent;
        Remaining = remaining;
    }
}

/// <summary>普通导电操作使用已有伤害及归属，数值传播与游戏结算分开。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>已经到达真实水路并完成实际结算后通知。</summary>
    public static event Action<ConstructedConduction> ConductionConfirmed;

    /// <summary>先在同一时点采样人物和水路，扣除真实预算，再提交仍有效的分支。</summary>
    /// <param name="running">已经确认的施放。</param><param name="node">真实入口部位。</param>
    /// <param name="settings">传播和消耗规则。</param><param name="clock">共同确认时点。</param>
    private static void Conduct(RunningCast running, int node, CompiledConduction settings, double clock)
    {
        long epoch = worldGeneration;
        var owner = running.Owner(node);
        if (!owner.IsLive) return;
        var targets = new List<ActorMotion>();
        var bodies = new List<AbilityCapsule>();
        var impact = running.Cast.Impact(node);
        foreach (var actor in simulation.actors.OrderBy(a => a.Id))
        {
            if (impact.DamageScale <= 0 || actor.Actor.data?.id != actor.Id || !actor.Actor.isAlive() ||
                !actor.Actor.hasHealth() ||
                !SkillTargetRelationResolver.Matches(impact.Targets, owner.Actor, actor.Actor)) continue;
            simulation.Sample(actor, clock, out var foot, out float height);
            targets.Add(actor);
            bodies.Add(ActorContact(foot, height, actor.Body?.Cast.BodyPose?.Torso ?? Vector3.zero));
        }

        var water = WorldMaterialService.ConductingWater(simulation.materialSelection, settings.Range);
        float cell = WorldMaterialService.ConductingCell;
        var trace = new AbilityConduction(water, simulation.materialSelection, bodies, settings, cell,
            running.Cast.RemainingForce(node));
        if (trace.Route.Count == 0) return;
        // 在原版回调前真实扣除，已消耗份额不能由回调再次回收。
        running.Cast.ConsumeForce(node, trace.Spent);
        if (epoch != worldGeneration) return;
        var reached = new List<ConductingWater>();
        var ids = new List<long>();
        var before = new List<float>();
        var after = new List<float>();
        var valid = new bool[trace.Route.Count];
        var currentWater = new ConductingWater[trace.Route.Count];
        int nextHit = 0;
        for (int i = 0; i < trace.Route.Count; i++)
        {
            if (!owner.IsLive) break;
            Validate(i);

            if (valid[i]) reached.Add(currentWater[i]);
            while (nextHit < trace.Hits.Count && trace.Hits[nextHit].Cell == i)
            {
                var hit = trace.Hits[nextHit++];
                var actor = targets[hit.Target];
                if (!valid[i] || actor.Actor.data?.id != actor.Id || !actor.Actor.isAlive() ||
                    !actor.Actor.hasHealth() || !owner.IsLive ||
                    !SkillTargetRelationResolver.Matches(impact.Targets, owner.Actor, actor.Actor)) continue;
                StartingPose(actor.Actor, out var foot, out float height);
                var body = ActorContact(foot, height, actor.Body?.Cast.BodyPose?.Torso ?? Vector3.zero);
                var shape = currentWater[i].Shape(cell);
                if (!AbilityPairSweep.Find(shape, shape, body, body, out _, out _, out _)) continue;
                float hp = actor.Actor.data.health;
                var damage = owner.Damage;
                damage.TargetID = actor.Id;
                damage.Damage = settings.DamagePerTarget * impact.DamageScale;
                damage.Element = ElementComposition.Static.Lightning;
                damage.DamageOrigin = DamageOrigin.Reaction;
                GetHitEventSystem.Resolve(damage);
                if (epoch != worldGeneration) return;
                ids.Add(actor.Id);
                before.Add(hp);
                after.Add(actor.Actor.data?.id == actor.Id ? actor.Actor.data.health : 0);
                // 回调可能移除已经经过的上游，剩余分支必须从入口重新确认连通。
                for (int prior = 0; prior <= i; prior++) Validate(prior);
            }
        }

        // 先前已经到达的水路仍保留实际记录；校验只决定尚未发生的后续作用。
        // index 是本次水路序号，按真实上游确认当前水格。
        void Validate(int index)
        {
            int parent = trace.Parents[index];
            valid[index] = (parent < 0 || valid[parent]) && WorldMaterialService.StillConducting(trace.Route[index],
                settings.MinimumDepth, out currentWater[index]);
            if (valid[index] && parent >= 0) valid[index] = currentWater[parent].Touches(currentWater[index], cell);
        }

        WorldMaterialService.PulseConduction(reached, clock);
        ConductionConfirmed?.Invoke(new ConstructedConduction(running.Id, running.SourceId, owner.Id,
            running.Cast.Plan.Id, node, clock,
            reached.Select(w => new Vector3(w.Footprint(cell).center.x, w.Footprint(cell).center.y,
                w.Bottom + w.Depth)).ToArray(),
            ids.ToArray(), before.ToArray(), after.ToArray(), trace.Spent, running.Cast.RemainingForce(node)));
    }
}