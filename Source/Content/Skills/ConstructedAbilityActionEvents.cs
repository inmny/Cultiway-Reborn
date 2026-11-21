using System;
using Cultiway.Core.Skills;

namespace Cultiway.Content.Skills;

/// <summary>已经确认的动作事实与本次正式施放身份。</summary>
public readonly struct ConstructedActionEvent
{
    /// <summary>本次施放编号。</summary>
    public readonly long CastId;

    /// <summary>实际能力稳定编号。</summary>
    public readonly string AbilityId;

    /// <summary>本次来源人物编号。</summary>
    public readonly long SourceId;

    /// <summary>实际动作时点对应的世界时间。</summary>
    public readonly double WorldTime;

    /// <summary>已经确认的部位、阶段、位置和接触。</summary>
    public readonly AbilityActionEvent Fact;

    /// <summary>由正式运行状态绑定身份，不从显示对象推算。</summary>
    /// <param name="castId">本次编号。</param><param name="abilityId">能力编号。</param>
    /// <param name="sourceId">实际来源。</param><param name="startedAt">本次开始时间。</param>
    /// <param name="fact">已经确认的动作事实。</param>
    internal ConstructedActionEvent(long castId, string abilityId, long sourceId, double startedAt,
        AbilityActionEvent fact)
    {
        CastId = castId;
        AbilityId = abilityId;
        SourceId = sourceId;
        WorldTime = startedAt + fact.Age;
        Fact = fact;
    }
}

/// <summary>只在主线程确认之后交付动作事实，绘制与预测不发布通知。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>正式受理后及每个完整接触边界发布；通知本身不编排下一招。</summary>
    public static event Action<ConstructedActionEvent> ActionConfirmed;

    /// <summary>取走本次已确认记录；重入订阅者各自使用独立池化缓冲。</summary>
    /// <param name="running">实际施放。</param>
    private static void PublishActionEvents(RunningCast running)
    {
        if (!running.Cast.HasActionEvents) return;
        // 完成与中断的订阅者应看到已经归还的身体，独立形体仍由本施放维护。
        if (running.BodyControlled && !running.Cast.ControlsBody) ReleaseBody(running);
        long generation = worldGeneration;
        ConfirmLegacyAreaImpact(running);
        if (generation != worldGeneration) return;
        using var events = new ListPool<AbilityActionEvent>();
        running.Cast.DrainActionEvents(events);
        foreach (AbilityActionEvent fact in events)
        {
            ActionConfirmed?.Invoke(new ConstructedActionEvent(running.Id, running.Cast.Plan.Id,
                running.SourceId, running.StartedAt, fact));
            if (generation != worldGeneration) return;
        }
    }
}
