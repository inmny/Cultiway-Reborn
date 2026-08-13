using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core.Performance;
using Cultiway.Core.SubWorlds.Generation;
using Cultiway.Core.SubWorlds.Model;
using Cultiway.Core.SubWorlds.Runtime;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SubWorlds;

/// <summary>
/// 作为当前主世界会话中全部小世界 Runtime 的唯一所有者和外部边界。
/// </summary>
internal sealed class SubWorldManager
{
    /// <summary>第一阶段测试小世界的模板 ID。</summary>
    internal const string TestSubWorldTemplateId = SubWorldTemplateLibrary.TestSubWorldId;
    private const string TickPhase = "cultiway.subworld.tick";

    private readonly Dictionary<long, SubWorldRuntime> runtimes = new();
    private readonly ParallelJobRunner jobRunner;
    private long nextInstanceId;
    private bool acceptingOperations = true;

    /// <summary>
    /// 使用项目共享的 ECS 并行执行器创建 Manager。
    /// </summary>
    /// <param name="jobRunner">项目共享的 ECS 并行任务执行器。</param>
    internal SubWorldManager(ParallelJobRunner jobRunner)
    {
        this.jobRunner = jobRunner ?? throw new ArgumentNullException(nameof(jobRunner));
    }

    /// <summary>当前会话内存活的小世界实例数量。</summary>
    internal int Count => runtimes.Count;

    /// <summary>
    /// 解析模板及其资产引用，生成并启动一个新的小世界实例。
    /// </summary>
    /// <param name="templateId">小世界模板 Asset ID。</param>
    /// <param name="anchor">实例在主世界中的入口锚点。</param>
    /// <param name="seed">实例的确定性随机种子。</param>
    /// <param name="parameters">可选的创建参数。</param>
    /// <returns>当前主世界会话内唯一的实例 ID。</returns>
    internal long Create(
        string templateId,
        SubWorldAnchor anchor,
        int seed,
        SubWorldCreationParameters parameters = null)
    {
        EnsureAcceptingOperations();
        SubWorldTemplateAsset template = ModClass.L.SubWorldTemplateLibrary.GetRequired(templateId);
        SubWorldGeneratorAsset generator = ModClass.L.SubWorldGeneratorLibrary.GetRequired(template.generator_id);
        SubWorldClockProfileAsset clockProfile = ModClass.L.SubWorldClockProfileLibrary.GetRequired(
            template.clock_profile_id);
        SubWorldVisualProfileAsset visualProfile = ModClass.L.SubWorldVisualProfileLibrary.GetRequired(
            template.visual_profile_id);
        SubWorldGeneratedScene scene = generator.Generate(
            template,
            seed,
            anchor,
            parameters ?? SubWorldCreationParameters.Empty);
        long instanceId = ++nextInstanceId;
        var runtime = new SubWorldRuntime(
            instanceId,
            template,
            seed,
            anchor,
            scene,
            clockProfile,
            visualProfile,
            jobRunner);
        runtime.Start();
        runtimes.Add(instanceId, runtime);
        return instanceId;
    }

    /// <summary>
    /// 取得当前会话中已经存在的小世界 Runtime。
    /// </summary>
    /// <param name="instanceId">实例 ID。</param>
    /// <returns>匹配的 Runtime。</returns>
    /// <exception cref="KeyNotFoundException">实例不存在时抛出。</exception>
    internal SubWorldRuntime Get(long instanceId)
    {
        if (!runtimes.TryGetValue(instanceId, out SubWorldRuntime runtime))
            throw new KeyNotFoundException($"SubWorld Runtime 不存在: instance={instanceId}");
        return runtime;
    }

    /// <summary>
    /// 同步销毁并移除指定小世界实例。
    /// </summary>
    /// <param name="instanceId">实例 ID。</param>
    /// <returns>找到并销毁实例时为 <see langword="true"/>。</returns>
    internal bool Destroy(long instanceId)
    {
        if (!runtimes.TryGetValue(instanceId, out SubWorldRuntime runtime)) return false;
        runtime.Destroy();
        runtimes.Remove(instanceId);
        return true;
    }

    /// <summary>
    /// 为所有实例累计时间，并在帧预算内执行待处理的完整固定 tick。
    /// </summary>
    /// <param name="unscaledDeltaTime">当前渲染帧经过的未缩放秒数。</param>
    /// <param name="parentPaused">主世界当前是否暂停。</param>
    internal void Update(float unscaledDeltaTime, bool parentPaused)
    {
        EnsureAcceptingOperations();
        foreach (SubWorldRuntime runtime in runtimes.Values)
        {
            runtime.Update(unscaledDeltaTime, parentPaused);
        }

        foreach (SubWorldRuntime runtime in runtimes.Values)
        {
            int tickCount = 0;
            while (runtime.Clock.HasPendingTick && tickCount < runtime.Clock.Profile.max_ticks_per_frame)
            {
                if (PerformanceSettings.EnableFramePriorityScheduler &&
                    !FramePriorityGovernor.CanRun(SimulationDomain.Cultiway, TickPhase))
                {
                    return;
                }

                if (PerformanceSettings.EnableFramePriorityScheduler)
                {
                    FramePriorityGovernor.RunPhase(SimulationDomain.Cultiway, TickPhase, runtime.RunTick);
                }
                else
                {
                    runtime.RunTick();
                }
                tickCount++;
            }
        }
    }

    /// <summary>
    /// 将命令加入指定 Runtime 的边界队列，不直接修改实例状态。
    /// </summary>
    /// <param name="instanceId">目标实例 ID。</param>
    /// <param name="command">待验证和执行的命令。</param>
    internal void IssueCommand(long instanceId, ISubWorldCommand command)
    {
        EnsureAcceptingOperations();
        if (command == null) throw new ArgumentNullException(nameof(command));
        Get(instanceId).CommandQueue.Enqueue(command);
    }

    /// <summary>
    /// 将唯一小世界浮窗绑定到指定实例。
    /// </summary>
    /// <param name="instanceId">要显示的实例 ID。</param>
    /// <remarks>第一阶段视图尚未实现，此方法当前只验证实例存在。</remarks>
    internal void ShowFloatingPanel(long instanceId)
    {
        EnsureAcceptingOperations();
        _ = Get(instanceId);
    }

    /// <summary>
    /// 销毁当前会话中的全部小世界实例并重置实例 ID 序列。
    /// </summary>
    internal void Clear()
    {
        acceptingOperations = false;
        try
        {
            long[] instanceIds = new long[runtimes.Count];
            runtimes.Keys.CopyTo(instanceIds, 0);
            for (int i = 0; i < instanceIds.Length; i++)
            {
                Destroy(instanceIds[i]);
            }
            nextInstanceId = 0;
        }
        finally
        {
            acceptingOperations = true;
        }
    }

    private void EnsureAcceptingOperations()
    {
        if (!acceptingOperations)
            throw new InvalidOperationException("SubWorld Manager 正在清理实例");
    }
}
