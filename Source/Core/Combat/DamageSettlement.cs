using System;
using UnityEngine;

namespace Cultiway.Core.Combat;

/// <summary>实际完成的受击，不把请求伤害、抗性减伤或之后回血算进扣除。</summary>
public readonly struct DamageSettlement
{
    /// <summary>确实进入有效目标结算的次数；免疫也属于结算。</summary>
    public readonly int SettledCount;

    /// <summary>原版实际扣除的生命点数，包含按剩余生命截断和取整。</summary>
    public readonly int HealthLost;

    /// <summary>可消耗护盾实际吸收的量，不包含其他减伤。</summary>
    public readonly float ShieldAbsorbed;

    /// <summary>记录一次实际受击。</summary>
    /// <param name="health">实际扣除生命。</param><param name="shield">护盾实际吸收。</param>
    internal DamageSettlement(int health, float shield) : this(1, health, shield) { }

    /// <summary>合计同一个目标的数次已确认受击。</summary>
    /// <param name="count">结算次数。</param><param name="health">累计扣血。</param><param name="shield">累计吸收。</param>
    private DamageSettlement(int count, int health, float shield)
    {
        SettledCount = count;
        HealthLost = health;
        ShieldAbsorbed = shield;
    }

    /// <summary>把同一人物的两份结果相加，保持扣血与吸收各自独立。</summary>
    /// <param name="first">先结算的结果。</param><param name="second">随后结算的结果。</param>
    public static DamageSettlement operator +(DamageSettlement first, DamageSettlement second) =>
        new(first.SettledCount + second.SettledCount, first.HealthLost + second.HealthLost,
            first.ShieldAbsorbed + second.ShieldAbsorbed);
}

/// <summary>让一次原版近身攻击沿原有结算顺序交回自己的真实受击结果。</summary>
internal sealed class DamageObservation
{
    /// <summary>观察这一次原版攻击的双方稳定身份。</summary>
    internal readonly long Source, Target;

    /// <summary>原版排队受理后才置位；跳过受击或换世界不伪造结果。</summary>
    internal bool Pending;

    /// <summary>需要立即结算的攻击直接填回此值。</summary>
    internal DamageSettlement Result;

    /// <summary>本次排队受理后的结果通知。</summary>
    private readonly Action<DamageSettlement> completed;

    /// <summary>只观察本次攻击的原目标。</summary>
    /// <param name="source">攻击者。</param><param name="target">目标。</param><param name="completed">队列结算通知。</param>
    internal DamageObservation(long source, long target, Action<DamageSettlement> completed = null)
    {
        Source = source;
        Target = target;
        this.completed = completed;
    }

    /// <summary>写回本次实际结算结果，已经入队的再通知订阅者。</summary>
    /// <param name="resolved">真实结算结果。</param>
    internal void Complete(DamageSettlement resolved)
    {
        Result = resolved;
        if (Pending) completed?.Invoke(resolved);
    }
}

/// <summary>只累计当前受击人物实际消耗的护盾，回调中的另一次受击单独计量。</summary>
internal readonly struct ShieldAbsorptionScope : IDisposable
{
    [ThreadStatic] private static ActorExtend measuredTarget;
    [ThreadStatic] private static float absorbed;
    [ThreadStatic] private static Vector3? contactPoint;

    private readonly ActorExtend previousTarget;
    private readonly float previousAbsorbed;
    private readonly Vector3? previousPoint;

    internal ShieldAbsorptionScope(ActorExtend target, Vector3? point)
    {
        previousTarget = measuredTarget;
        previousAbsorbed = absorbed;
        previousPoint = contactPoint;
        measuredTarget = target;
        absorbed = 0f;
        contactPoint = point;
    }

    internal float Absorbed => absorbed;

    /// <summary>只读取这一次受击的位置，嵌套受击不会继承外层的接触。</summary>
    internal static Vector3? ContactFor(ActorExtend target) => ReferenceEquals(target, measuredTarget) ? contactPoint : null;

    internal static void Record(ActorExtend target, float amount)
    {
        if (ReferenceEquals(target, measuredTarget)) absorbed += amount;
    }

    public void Dispose()
    {
        measuredTarget = previousTarget;
        absorbed = previousAbsorbed;
        contactPoint = previousPoint;
    }
}
