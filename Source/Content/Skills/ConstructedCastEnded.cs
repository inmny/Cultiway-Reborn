namespace Cultiway.Content.Skills;

/// <summary>实际施放已经结束的事实，不用动画长度或预计寿命冒充完成。</summary>
public readonly struct ConstructedCastEnded
{
    /// <summary>进程内施放编号、原来源和发起任务的关联编号。</summary>
    public readonly long CastId, SourceId, CorrelationId;

    /// <summary>本次实际使用的构造编号。</summary>
    public readonly string AbilityId;

    /// <summary>正常结束的共同时间；取消采用当前确认时点。</summary>
    public readonly double WorldTime;

    /// <summary>已经实际推进的施放年龄。</summary>
    public readonly float Age;

    /// <summary>是否由明确取消结束，不能作为正常完成的证明。</summary>
    public readonly bool Cancelled;

    /// <summary>实际改变世界的提交次数，任务仍须验收自己的具体结果。</summary>
    public readonly int WorldChanges;

    /// <summary>只在解除控制、释放额度和移除运行后创建。</summary>
    /// <param name="cast">实际施放。</param><param name="ability">实际构造。</param>
    /// <param name="source">最初来源。</param><param name="correlation">发起任务。</param>
    /// <param name="time">真实共同时间。</param><param name="age">实际年龄。</param>
    /// <param name="cancelled">是否明确取消。</param><param name="changes">真实世界变化次数。</param>
    internal ConstructedCastEnded(long cast, string ability, long source, long correlation, double time, float age,
        bool cancelled, int changes)
    {
        CastId = cast;
        AbilityId = ability;
        SourceId = source;
        CorrelationId = correlation;
        WorldTime = time;
        Age = age;
        Cancelled = cancelled;
        WorldChanges = changes;
    }
}