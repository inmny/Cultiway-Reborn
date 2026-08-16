using System;
using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.Pathfinding;
using Cultiway.Core.SubWorlds.Generation;
using Cultiway.Core.SubWorlds.Model;
using Cultiway.Core.SubWorlds.Objects;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SubWorlds.Runtime;

/// <summary>
/// 拥有一个小世界实例的地图、ECS、时钟和边界队列。
/// </summary>
internal sealed class SubWorldRuntime
{
    private const int MaxCommandsPerBoundary = 32;

    private readonly EntityStore entityStore;
    private readonly SystemRoot systemRoot;
    private Entity debugControllableActor;
    private bool sceneInitialized;

    /// <summary>
    /// 从已生成场景构造一个尚未启动的小世界 Runtime。
    /// </summary>
    /// <param name="instanceId">当前主世界会话内唯一的实例 ID。</param>
    /// <param name="template">创建此实例的模板。</param>
    /// <param name="seed">场景生成与视觉变体使用的创建种子。</param>
    /// <param name="anchor">实例在主世界中的入口锚点。</param>
    /// <param name="scene">生成完成的初始场景。</param>
    /// <param name="clockProfile">实例绑定的时钟配置。</param>
    /// <param name="visualProfile">实例绑定的视觉配置。</param>
    /// <param name="jobRunner">项目共享的 ECS 并行任务执行器。</param>
    internal SubWorldRuntime(
        long instanceId,
        SubWorldTemplateAsset template,
        int seed,
        SubWorldAnchor anchor,
        SubWorldGeneratedScene scene,
        SubWorldClockProfileAsset clockProfile,
        SubWorldVisualProfileAsset visualProfile,
        ParallelJobRunner jobRunner)
    {
        InstanceId = instanceId;
        TemplateId = template.id;
        Seed = seed;
        Anchor = anchor;
        MapData = scene.MapData;
        Grid = new SubWorldGrid(MapData);
        SpawnPoints = new SubWorldSpawnPointCollection(scene.SpawnPoints, Grid.TileCount);
        Navigation = new SubWorldNavigationContext(instanceId, Grid);
        ClockProfile = clockProfile;
        VisualProfile = visualProfile;
        Clock = new SubWorldClock(clockProfile);
        entityStore = new EntityStore
        {
            JobRunner = jobRunner
        };
        systemRoot = new SystemRoot(entityStore, $"SubWorld.{instanceId}");
        LocalObjectIds = new SubWorldLocalObjectIdAllocator();
        Buildings = new SubWorldBuildingSpatialIndex(this, Grid.TileCount);
        systemRoot.Add(new SubWorldMovementSystem(this));

        State = SubWorldRuntimeState.Created;
        Revision = 0;
        MapRevision = 0;
    }

    /// <summary>当前主世界会话内唯一的实例 ID。</summary>
    internal long InstanceId { get; }

    /// <summary>创建此实例的模板 Asset ID。</summary>
    internal string TemplateId { get; }

    /// <summary>场景生成与视觉变体使用的创建种子。</summary>
    internal int Seed { get; }

    /// <summary>实例在主世界中的入口锚点。</summary>
    internal SubWorldAnchor Anchor { get; }

    /// <summary>实例当前的生命周期状态。</summary>
    internal SubWorldRuntimeState State { get; private set; }

    /// <summary>实例私有的地图数据。</summary>
    internal SubWorldMapData MapData { get; }

    /// <summary>实例私有的 ECS Store。</summary>
    internal EntityStore EntityStore => entityStore;

    /// <summary>驱动此实例 ECS 系统的根节点。</summary>
    internal SystemRoot SystemRoot => systemRoot;

    /// <summary>用途 Generator 声明的命名出生点。</summary>
    internal SubWorldSpawnPointCollection SpawnPoints { get; }

    /// <summary>Runtime-local 且不复用的 Building ID 分配器。</summary>
    internal SubWorldLocalObjectIdAllocator LocalObjectIds { get; }

    /// <summary>LocalObjectId 与 Tile 到 Building Entity 的空间索引。</summary>
    internal SubWorldBuildingSpatialIndex Buildings { get; }

    /// <summary>实例私有的地图坐标与 terrain 引用缓存。</summary>
    internal SubWorldGrid Grid { get; }

    /// <summary>向共享 PathFinder 发布本实例的 terrain 导航快照。</summary>
    internal SubWorldNavigationContext Navigation { get; }

    /// <summary>实例绑定的静态时钟配置。</summary>
    internal SubWorldClockProfileAsset ClockProfile { get; }

    /// <summary>实例绑定的静态视觉配置。</summary>
    internal SubWorldVisualProfileAsset VisualProfile { get; }

    /// <summary>实例私有的固定时钟。</summary>
    internal SubWorldClock Clock { get; }

    /// <summary>等待在 Runtime 边界验证的外部命令。</summary>
    internal Queue<ISubWorldCommand> CommandQueue { get; } = new();

    /// <summary>已经通过边界验证、等待移动系统处理的命令。</summary>
    internal Queue<MoveToTileCommand> MoveCommandQueue { get; } = new();

    /// <summary>在 Runtime 启动前物化 Generator 声明的初始 Actor 和 Building。</summary>
    internal void InitializeScene(SubWorldGeneratedScene scene)
    {
        if (scene == null) throw new ArgumentNullException(nameof(scene));
        if (sceneInitialized) throw new InvalidOperationException($"SubWorld Runtime 已完成场景物化: {InstanceId}");
        if (State != SubWorldRuntimeState.Created)
            throw new InvalidOperationException($"SubWorld Runtime 只能在 Created 状态物化场景: {InstanceId}");

        for (int i = 0; i < scene.BuildingPlacements.Length; i++)
        {
            SubWorldBuildingPlacement placement = scene.BuildingPlacements[i];
            Vector3 position = GetTileCenter(placement.AnchorTileIndex);
            Entity building = entityStore.CreateEntity(
                new Position(position),
                new SubWorldBuilding(placement.LocalObjectId, placement.BuildingAssetId),
                new SubWorldVisual(placement.VisualVariantIndex, placement.VisualState));
            Buildings.Register(building);
        }

        for (int i = 0; i < scene.ActorPlacements.Length; i++)
        {
            SubWorldActorPlacement placement = scene.ActorPlacements[i];
            Entity actor = entityStore.CreateEntity(
                new Position(GetTileCenter(placement.TileIndex)),
                new SubWorldActor(placement.ActorAssetId),
                new SubWorldVisual(placement.VisualVariantIndex, placement.VisualState),
                new SubWorldMovement(placement.MoveSpeedTilesPerSecond, placement.TileIndex));
            if (placement.DebugControllable) SetDebugControllableActor(actor);
        }

        sceneInitialized = true;
    }

    private Vector3 GetTileCenter(int tileIndex)
    {
        return new Vector3(Grid.GetX(tileIndex) + 0.5f, Grid.GetY(tileIndex) + 0.5f, 0f);
    }

    /// <summary>注册 Debug 地图唯一可由右键命令控制的 Actor。</summary>
    internal void SetDebugControllableActor(Entity entity)
    {
        if (entity.IsNull || entity.Store != entityStore || !entity.HasComponent<SubWorldActor>() ||
            !entity.HasComponent<Position>() || !entity.HasComponent<SubWorldVisual>() ||
            !entity.HasComponent<SubWorldMovement>())
            throw new InvalidOperationException("Debug controllable Actor 缺少类别、Position、Visual 或 Movement");
        if (!debugControllableActor.IsNull)
            throw new InvalidOperationException($"SubWorld Runtime 已存在 Debug controllable Actor: {InstanceId}");
        debugControllableActor = entity;
    }

    internal bool TryGetDebugControllableActor(out Entity entity)
    {
        entity = debugControllableActor;
        return !entity.IsNull && entity.Store == entityStore;
    }

    /// <summary>地图或实体结构每次变更后递增的版本号。</summary>
    internal long Revision { get; private set; }

    /// <summary>terrain 数据成功变更的版本号。</summary>
    internal long MapRevision { get; private set; }

    /// <summary>将新建 Runtime 切换到可推进状态。</summary>
    internal void Start()
    {
        if (State != SubWorldRuntimeState.Created)
            throw new InvalidOperationException($"SubWorld Runtime 不能从当前状态启动: instance={InstanceId}, state={State}");
        if (!sceneInitialized)
            throw new InvalidOperationException($"SubWorld Runtime 尚未完成场景物化: instance={InstanceId}");
        State = SubWorldRuntimeState.Running;
    }

    /// <summary>
    /// 处理当前帧的边界命令，并向实例时钟累计未缩放时间。
    /// </summary>
    /// <param name="unscaledDeltaTime">当前渲染帧经过的未缩放秒数。</param>
    /// <param name="parentPaused">主世界当前是否暂停。</param>
    internal void Update(float unscaledDeltaTime, bool parentPaused)
    {
        if (unscaledDeltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
        if (State != SubWorldRuntimeState.Running) return;
        ProcessCommands();
        Clock.Accumulate(unscaledDeltaTime, parentPaused);
    }

    /// <summary>
    /// 执行一个完整固定 tick，并在所有系统完成后提交时钟。
    /// </summary>
    internal void RunTick()
    {
        var tick = new UpdateTick(Clock.Profile.fixed_step, (float)Clock.NextLocalTime);
        systemRoot.Update(tick);
        Clock.CompleteTick();
    }

    /// <summary>记录一次会使旧命令或路径结果失效的结构变更。</summary>
    internal void MarkStructuralChange()
    {
        Revision++;
    }

    /// <summary>通过单一边界修改 terrain，并同步发布导航与版本。</summary>
    internal void SetTile(int tileIndex, SubWorldTile tile)
    {
        Grid.SetTile(tileIndex, tile);
        Navigation.PublishTerrainChange();
        MapRevision++;
        Revision++;
    }

    /// <summary>
    /// 删除实例 Entity、解绑 ECS Store、清空队列并进入销毁状态。
    /// </summary>
    internal void Destroy()
    {
        if (State == SubWorldRuntimeState.Collapsed) return;
        PathFinder.Instance.CancelWorld(Navigation.WorldKey, PathFailureReason.ClearWorld);
        Buildings.Clear();
        debugControllableActor = default;
        EntityList entities = entityStore.Entities.ToEntityList();
        for (int i = entities.Count - 1; i >= 0; i--)
        {
            Entity entity = entities[i];
            if (entity.IsNull || entity == entityStore.StoreRoot) continue;
            entity.DeleteEntity();
        }

        systemRoot.RemoveStore(entityStore);
        CommandQueue.Clear();
        MoveCommandQueue.Clear();
        State = SubWorldRuntimeState.Collapsed;
    }

    private void ProcessCommands()
    {
        int commandCount = Math.Min(MaxCommandsPerBoundary, CommandQueue.Count);
        for (int i = 0; i < commandCount; i++)
        {
            ISubWorldCommand command = CommandQueue.Dequeue();
            if (command.InstanceId != InstanceId) continue;

            switch (command)
            {
                case PauseCommand pause:
                    Clock.SetPaused(pause.Paused);
                    break;
                case SetLocalSpeedCommand speed:
                    _ = Clock.TrySetLocalSpeed(speed.LocalSpeed);
                    break;
                case MoveToTileCommand move:
                    AcceptMoveCommand(move);
                    break;
                default:
                    throw new InvalidOperationException($"SubWorld 命令类型未注册: {command.GetType().FullName}");
            }
        }
    }

    private void AcceptMoveCommand(MoveToTileCommand command)
    {
        if (command.Revision != Revision || (uint)command.TargetTileIndex >= (uint)Grid.TileCount) return;

        Entity entity = command.Entity;
        if (entity.IsNull || entity.Store != entityStore || !entity.HasComponent<Position>() ||
            !entity.HasComponent<SubWorldMovement>()) return;
        MoveCommandQueue.Enqueue(command);
    }
}
