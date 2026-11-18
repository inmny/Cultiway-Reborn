using System;
using Cultiway.Core.Combat;

namespace Cultiway.Content.Skills;

/// <summary>原版近身伤害沿原队列完成后的补充结果，对应此前同编号的人物接触。</summary>
public readonly struct ConstructedQueuedDamage
{
    /// <summary>原接触的施放、来源和目标。</summary>
    public readonly long CastId, SourceId, TargetId;

    /// <summary>原接触的构造。</summary>
    public readonly string AbilityId;

    /// <summary>本记录对应身体部位和同一目标的接触次数。</summary>
    public const int Node = -1;
    public readonly int TargetContactNumber;

    /// <summary>原先接触的世界时点，以及队列实际处理的时点。</summary>
    public readonly double ContactWorldTime, WorldTime;

    /// <summary>只含排队这次实际扣除的数值；无有效目标时结算次数为零。</summary>
    public readonly DamageSettlement Damage;

    /// <summary>队列处理后实际读取的生命；原目标已不存在时为空。</summary>
    public readonly int? TargetHealthAfter;

    /// <summary>在原队列实际处理后补全事实。</summary>
    /// <param name="cast">原施放。</param><param name="ability">原构造。</param>
    /// <param name="source">实际来源。</param><param name="target">实际目标。</param>
    /// <param name="number">原接触次数。</param><param name="contactTime">实际接触时点。</param>
    /// <param name="time">队列处理时点。</param><param name="damage">实际损耗。</param>
    /// <param name="health">队列处理后的生命。</param>
    internal ConstructedQueuedDamage(long cast, string ability, long source, long target, int number,
        double contactTime, double time, DamageSettlement damage, int? health)
    {
        CastId = cast;
        AbilityId = ability;
        SourceId = source;
        TargetId = target;
        TargetContactNumber = number;
        ContactWorldTime = contactTime;
        WorldTime = time;
        Damage = damage;
        TargetHealthAfter = health;
    }
}

public sealed partial class ConstructedAbilityService
{
    /// <summary>近身接触引发的原版伤害实际出队后触发；不会再次发布接触。</summary>
    public static event Action<ConstructedQueuedDamage> QueuedDamageConfirmed;

    /// <summary>只保留身份和时点；换世界由现有队列清理，不在新世界补发旧接触。</summary>
    /// <param name="cast">原施放。</param><param name="ability">原构造。</param>
    /// <param name="source">实际来源。</param><param name="target">实际目标。</param>
    /// <param name="number">原接触次数。</param><param name="time">真实接触时点。</param>
    private static DamageObservation ObserveQueuedContact(long cast, string ability, long source, long target,
        int number, double time)
    {
        long generation = worldGeneration;
        return new DamageObservation(source, target, damage =>
        {
            if (generation != worldGeneration) return;
            int? health = World.world.units.get(target)?.data?.health;
            QueuedDamageConfirmed?.Invoke(new ConstructedQueuedDamage(cast, ability, source, target, number,
                time, World.world.getCurWorldTime(), damage, health));
        });
    }
}
