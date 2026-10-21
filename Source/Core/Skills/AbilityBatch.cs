using System.Collections.Generic;
using Cultiway.Core.Performance;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>主线程已经采集好的一个数值步，工作过程不访问人物、器物或显示对象。</summary>
internal readonly struct AbilityStep
{
    /// <summary>本步独占的施放状态，同一批次不能重复放入同一个实例。</summary>
    private readonly AbilityCast cast;
    /// <summary>实际来源及目标的数值位置。</summary>
    private readonly AbilityInputs inputs;
    /// <summary>实际身体输入，没有借用身体时为空。</summary>
    private readonly BodyFrame? body;
    /// <summary>本步追踪的数值目标。</summary>
    private readonly Vector3 target;
    /// <summary>共同世界时间步长。</summary>
    private readonly float seconds;
    /// <summary>只在外部输入改变后重新采样，不推进时间。</summary>
    private readonly bool refresh;
    /// <summary>本次需要处理的形体和身体数，用于避免复杂构造仍被塞进过大的任务份额。</summary>
    internal int WorkSize => cast.Plan.WorkSize;

    /// <summary>复制已经采集的输入，工作计算不回头读取游戏。</summary>
    /// <param name="cast">独占施放。</param>
    /// <param name="inputs">本步连接输入。</param>
    /// <param name="body">本步实际身体。</param>
    /// <param name="target">数值目标。</param>
    /// <param name="seconds">共同步长。</param>
    /// <param name="refresh">是否仅重新采样改变后的输入。</param>
    internal AbilityStep(AbilityCast cast, AbilityInputs inputs, BodyFrame? body, Vector3 target, float seconds, bool refresh = false)
    { this.cast = cast; this.inputs = inputs; this.body = body; this.target = target; this.seconds = seconds; this.refresh = refresh; }

    /// <summary>只推进独占数值，双方接触及实际伤害仍由主线程有序确认。</summary>
    internal void Execute()
    {
        if (body.HasValue) cast.UpdateBody(body.Value);
        cast.UpdateInputs(in inputs);
        if (refresh) cast.Refresh(target); else cast.Advance(seconds, target);
    }
}

/// <summary>使用现有工作池批量推进构造，不创建新的线程池或在工作任务内嵌套调度。</summary>
internal static class AbilityBatch
{
    /// <summary>每份任务连续处理的施放数，沿用先前数值基线的粒度。</summary>
    private const int Grain = 32;

    /// <summary>从主线程调用并等候本批完成；调用者决定小批顺序执行或大批并行。</summary>
    /// <param name="steps">已冻结输入、各自独占状态的共同步，计算期间调用者不修改集合。</param>
    /// <param name="useWorkers">是否复用现有工作池；顺序路径用于小批及结果对照。</param>
    internal static void Advance(IReadOnlyList<AbilityStep> steps, bool useWorkers)
    {
        if (!useWorkers)
        { for (int i = 0; i < steps.Count; i++) steps[i].Execute(); return; }
        int work = 0;
        for (int i = 0; i < steps.Count; i++) work += steps[i].WorkSize;
        int grain = System.Math.Max(1, Grain / System.Math.Max(1, work / System.Math.Max(1, steps.Count)));
        SimulationWorkerPool.Instance.RunIndexed(0, (steps.Count + grain - 1) / grain, part =>
        {
            int end = System.Math.Min(steps.Count, (part + 1) * grain);
            for (int i = part * grain; i < end; i++) steps[i].Execute();
        });
    }
}
