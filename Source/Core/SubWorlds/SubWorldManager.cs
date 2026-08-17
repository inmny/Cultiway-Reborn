using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core.Performance;
using Cultiway.Core.SubWorlds.Generation;
using Cultiway.Core.SubWorlds.Model;
using Cultiway.Core.SubWorlds.Runtime;
using Cultiway.UI.SubWorlds;
using Friflo.Engine.ECS;
using UnityEngine;

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
    private readonly Dictionary<long, SubWorldWorldView> worldViews = new();
    private readonly ParallelJobRunner jobRunner;
    private readonly SubWorldSpatialLayout spatialLayout = new();
    private readonly SubWorldViewVisibilitySystem visibilitySystem = new();
    private readonly SubWorldCameraNavigator cameraNavigator = new();
    private readonly SubWorldNavigationSection navigationSection;
    private readonly SubWorldWorldInputRouter inputRouter;
    private long nextInstanceId;
    private bool acceptingOperations = true;

    /// <summary>
    /// 使用项目共享的 ECS 并行执行器创建 Manager。
    /// </summary>
    /// <param name="jobRunner">项目共享的 ECS 并行任务执行器。</param>
    internal SubWorldManager(ParallelJobRunner jobRunner)
    {
        this.jobRunner = jobRunner ?? throw new ArgumentNullException(nameof(jobRunner));
        navigationSection = new SubWorldNavigationSection(this);
        navigationSection.Build();
        inputRouter = new SubWorldWorldInputRouter(this, spatialLayout, worldViews);
    }

    /// <summary>当前会话内存活的小世界实例数量。</summary>
    internal int Count => runtimes.Count;

    /// <summary>WORLD 神力分区当前选中的小世界；为空表示主世界。</summary>
    internal long? FocusedInstanceId => cameraNavigator.FocusedInstanceId;

    /// <summary>
    /// 解析模板及其资产引用，生成并启动一个新的小世界实例。
    /// </summary>
    /// <param name="templateId">小世界模板 Asset ID。</param>
    /// <param name="anchor">实例在主世界中的入口锚点。</param>
    /// <param name="seed">场景生成与视觉变体使用的创建种子。</param>
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
        SubWorldCreationParameters creationParameters = parameters ?? SubWorldCreationParameters.Empty;
        int requestedWidth = creationParameters.Width > 0 ? creationParameters.Width : template.width;
        int requestedHeight = creationParameters.Height > 0 ? creationParameters.Height : template.height;
        ValidateCreationSize(template, creationParameters, requestedWidth, requestedHeight);
        SubWorldGeneratedScene scene = generator.Generate(
            template,
            seed,
            anchor,
            creationParameters);
        if (scene.MapData.Width != requestedWidth || scene.MapData.Height != requestedHeight)
        {
            throw new InvalidOperationException(
                $"SubWorld Generator 返回尺寸与请求不一致: template={template.id}, " +
                $"requested={requestedWidth}x{requestedHeight}, " +
                $"actual={scene.MapData.Width}x{scene.MapData.Height}");
        }
        long instanceId = ++nextInstanceId;
        SubWorldRuntime runtime = null;
        SubWorldWorldView worldView = null;
        bool slotAllocated = false;
        bool registered = false;
        try
        {
            runtime = new SubWorldRuntime(
                instanceId,
                template,
                seed,
                anchor,
                scene,
                clockProfile,
                visualProfile,
                jobRunner);
            runtime.InitializeScene(scene);
            runtime.Start();
            SubWorldSpatialSlot slot = spatialLayout.Allocate(instanceId, runtime.Grid.Width, runtime.Grid.Height);
            slotAllocated = true;
            worldView = new SubWorldWorldView(runtime, slot);
            runtimes.Add(instanceId, runtime);
            worldViews.Add(instanceId, worldView);
            registered = true;
            navigationSection.AddRuntime(runtime);
            return instanceId;
        }
        catch
        {
            if (registered)
            {
                navigationSection.RemoveRuntime(instanceId);
                worldViews.Remove(instanceId);
                runtimes.Remove(instanceId);
            }
            worldView?.Destroy();
            if (slotAllocated) spatialLayout.Release(instanceId);
            runtime?.Destroy();
            throw;
        }
    }

    /// <summary>
    /// 从当前主世界创建一个由用户选择模板和尺寸的小世界，并聚焦该实例。
    /// </summary>
    /// <param name="templateId">小世界模板 Asset ID。</param>
    /// <param name="width">本次创建的地图宽度。</param>
    /// <param name="height">本次创建的地图高度。</param>
    /// <param name="settings">本次创建冻结的自然地图参数。</param>
    /// <returns>新创建的小世界实例 ID。</returns>
    internal long CreateFromWorld(
        string templateId,
        int width,
        int height,
        SubWorldGenerationSettings settings)
    {
        EnsureAcceptingOperations();
        if (World.world == null || World.world.map_stats == null)
            throw new InvalidOperationException("当前没有可用的主世界");

        long worldIdentity = World.world.map_stats.life_dna;
        long instanceSeed = worldIdentity ^ (worldIdentity >> 32) ^ ((nextInstanceId + 1) * 397L);
        int seed = unchecked((int)instanceSeed);
        long instanceId = Create(templateId,
            new SubWorldAnchor(MapBox.width / 2, MapBox.height / 2),
            seed,
            new SubWorldCreationParameters(width, height, settings));
        Focus(instanceId);
        return instanceId;
    }

    private static void ValidateCreationSize(
        SubWorldTemplateAsset template,
        SubWorldCreationParameters parameters,
        int width,
        int height)
    {
        if ((parameters.Width > 0) != (parameters.Height > 0))
            throw new InvalidOperationException($"SubWorld 创建尺寸必须同时提供宽度和高度: {template.id}");
        if (width < 8 || height < 8)
            throw new InvalidOperationException($"SubWorld 创建尺寸不能小于 8: {width}x{height}");
        if (width > SubWorldSpatialLayout.MaxTemplateSize || height > SubWorldSpatialLayout.MaxTemplateSize)
        {
            throw new InvalidOperationException(
                $"SubWorld 创建尺寸超过槽位上限: {width}x{height}, " +
                $"max={SubWorldSpatialLayout.MaxTemplateSize}");
        }
        if (!template.allow_custom_size && (width != template.width || height != template.height))
        {
            throw new InvalidOperationException(
                $"SubWorld 模板不支持自定义尺寸: template={template.id}, " +
                $"default={template.width}x{template.height}");
        }
    }

    /// <summary>
    /// 取得当前会话中已经存在的小世界 Runtime。
    /// </summary>
    /// <param name="instanceId">小世界实例 ID。</param>
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
        worldViews[instanceId].Destroy();
        worldViews.Remove(instanceId);
        spatialLayout.Release(instanceId);
        if (FocusedInstanceId == instanceId) cameraNavigator.Reset();
        navigationSection.RemoveRuntime(instanceId);
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

    /// <summary>聚焦主地图中心，并把 WORLD 神力分区当前目标切回主世界。</summary>
    internal void FocusMainWorld()
    {
        EnsureAcceptingOperations();
        cameraNavigator.FocusMainWorld();
        navigationSection.Refresh();
    }

    /// <summary>聚焦指定小世界的地图中心。</summary>
    internal void Focus(long instanceId)
    {
        EnsureAcceptingOperations();
        cameraNavigator.Focus(Get(instanceId), spatialLayout.Get(instanceId));
        navigationSection.Refresh();
    }

    /// <summary>聚焦 WORLD 神力分区当前目标中的测试 Pawn。</summary>
    internal void FocusPawn()
    {
        EnsureAcceptingOperations();
        if (!FocusedInstanceId.HasValue) return;
        long instanceId = FocusedInstanceId.Value;
        cameraNavigator.FocusPawn(Get(instanceId), spatialLayout.Get(instanceId));
    }

    internal bool HasDebugControllableActor(long instanceId)
    {
        return Get(instanceId).TryGetDebugControllableActor(out _);
    }

    /// <summary>由 WorldView 左键选择实例，但不移动相机。</summary>
    internal void SelectFromWorldView(long instanceId)
    {
        EnsureAcceptingOperations();
        _ = Get(instanceId);
        cameraNavigator.Select(instanceId);
        navigationSection.Refresh();
    }

    /// <summary>在 MoveCamera 更新后同步所有与相机视野相交的 WorldView。</summary>
    internal void UpdateWorldViews()
    {
        if (!acceptingOperations || worldViews.Count == 0) return;
        visibilitySystem.Update(MoveCamera.instance.main_camera, worldViews);
        navigationSection.Refresh();
    }

    /// <summary>路由当前指针位置的世界输入。</summary>
    internal bool RouteWorldInput()
    {
        return acceptingOperations && inputRouter.Route();
    }

    /// <summary>取得包含主地图及全部占用槽位 CellBounds 的相机边界。</summary>
    internal bool TryGetCameraBounds(out Rect bounds)
    {
        bounds = spatialLayout.CameraBounds;
        return spatialLayout.HasOccupiedSlots;
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
            worldViews.Clear();
            spatialLayout.Clear();
            navigationSection.Clear();
            cameraNavigator.Reset();
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
