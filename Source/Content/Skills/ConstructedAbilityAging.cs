using System;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>同一真实人物已经接受的年龄变化，死亡结果由原版规则确认。</summary>
public readonly struct ConstructedAging
{
    /// <summary>本次施放、原来源、当前归属和实际接触人物。</summary>
    public readonly long CastId, SourceId, OwnerId, TargetId;
    /// <summary>本次构造的正式编号。</summary>
    public readonly string AbilityId;
    /// <summary>真实作用部位及前后整数年龄。</summary>
    public readonly int Node, Before, After;
    /// <summary>已经确认接触的共同世界时点。</summary>
    public readonly double WorldTime;
    /// <summary>原版寿终规则本次是否确认死亡。</summary>
    public readonly bool NaturalDeath;

    /// <summary>只在人物已经接受年龄变化后建立事实。</summary>
    /// <param name="cast">本次施放。</param><param name="source">原来源。</param><param name="owner">当前归属。</param>
    /// <param name="target">实际人物。</param><param name="ability">正式构造。</param><param name="node">作用部位。</param>
    /// <param name="before">变化前年龄。</param><param name="after">变化后年龄。</param>
    /// <param name="time">真实接触时点。</param><param name="naturalDeath">原版确认的寿终结果。</param>
    internal ConstructedAging(long cast, long source, long owner, long target, string ability, int node,
        int before, int after, double time, bool naturalDeath)
    {
        CastId = cast; SourceId = source; OwnerId = owner; TargetId = target;
        AbilityId = ability; Node = node; Before = before; After = after;
        WorldTime = time; NaturalDeath = naturalDeath;
    }
}

/// <summary>普通接触推进原人物年龄，成长、寿命与死亡继续交给原玩法。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>实际老化已经发生，供说明及验证观察。</summary>
    public static event Action<ConstructedAging> AgingConfirmed;

    /// <summary>先确定可表示年数，再更新原年龄及成长，最后由原版确认寿终。</summary>
    /// <param name="running">实际施放。</param><param name="node">实际部位。</param><param name="target">已确认的存活人物。</param>
    /// <param name="years">声明的整数年数。</param><param name="result">真实攻击结果。</param>
    /// <param name="strength">接触或传播的衰减倍率。</param>
    /// <returns>仍在同一世界且为原来存活人物时返回真。</returns>
    private static bool ApplyContactAging(RunningCast running, int node, Actor target, int years,
        ApplyAttackState result, float strength)
    {
        if (years == 0 || result != ApplyAttackState.Hit) return true;
        int before = target.getAge();
        int added = (int)Math.Min(Math.Floor(years * (double)strength),
            Math.Min((long)int.MaxValue - before, (long)int.MaxValue - target.data.age_overgrowth));
        if (added <= 0) return true;
        long generation = worldGeneration, targetId = target.id, owner = running.Owner(node).Id;
        double time = simulation.contactTime;
        target.data.age_overgrowth += added;
        int after = target.getAge();
        target.setStatsDirty();
        target.updateStats();
        if (generation != worldGeneration || target.data?.id != targetId) return false;
        bool died = target.isAlive() && target.hasHealth() && target.checkNaturalDeath();
        if (!died) AddTimeWear(new TimeWear(target, targetId, default, WearKind.Age, Mathf.Clamp(added * .12f, .55f, 1.5f), time));
        AgingConfirmed?.Invoke(new ConstructedAging(running.Id, running.SourceId, owner, targetId,
            running.Cast.Plan.Id, node, before, after, time, died));
        return generation == worldGeneration && target.data?.id == targetId && target.isAlive() && target.hasHealth();
    }
}
