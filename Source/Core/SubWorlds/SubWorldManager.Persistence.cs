using System;
using System.IO;
using System.Linq;
using Cultiway.Core.Components;
using Cultiway.Core.Persistence;
using Cultiway.Core.SubWorlds.Generation;
using Cultiway.Core.SubWorlds.Objects;
using Cultiway.Core.SubWorlds.Runtime;
using Cultiway.UI.SubWorlds;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SubWorlds;

/// <summary>小世界存档只恢复现有地图与对象，不重新运行生成器。</summary>
internal sealed partial class SubWorldManager
{
    /// <summary>记录全部存续实例及已经分配过的最大编号。</summary>
    internal SubWorldSaveData CaptureWorldSave()
    {
        var data = new SubWorldSaveData { NextId = nextInstanceId };
        foreach (SubWorldRuntime runtime in runtimes.Values.OrderBy(runtime => runtime.InstanceId))
        {
            var record = new SubWorldInstanceSaveData
            {
                Id = runtime.InstanceId, Template = runtime.TemplateId, Seed = runtime.Seed,
                AnchorX = runtime.Anchor.X, AnchorY = runtime.Anchor.Y, Map = runtime.MapData,
                SpawnPoints = runtime.SpawnPoints.CaptureWorldSave(), NextObjectId = runtime.LocalObjectIds.NextValue
            };
            runtime.Clock.CaptureWorldSave(record);
            runtime.TryGetDebugControllableActor(out Entity controllable);
            foreach (Entity entity in runtime.EntityStore.Query<SubWorldActor, Position, SubWorldMovement>().Entities)
            {
                var movement = entity.GetComponent<SubWorldMovement>();
                var position = entity.GetComponent<Position>();
                var visual = entity.GetComponent<SubWorldVisual>();
                record.Actors.Add(new SubWorldActorSaveData
                {
                    Asset = entity.GetComponent<SubWorldActor>().ActorAssetId,
                    X = position.x, Y = position.y, Z = position.z,
                    Speed = movement.MoveSpeedTilesPerSecond, Tile = movement.CurrentTileIndex,
                    Target = movement.TargetTileIndex, NextTile = movement.NextTileIndex,
                    Variant = visual.VariantIndex, Visual = visual.State, Controllable = entity == controllable
                });
            }
            foreach (Entity entity in runtime.EntityStore.Query<SubWorldBuilding, Position>().Entities)
            {
                var building = entity.GetComponent<SubWorldBuilding>();
                var position = entity.GetComponent<Position>();
                var visual = entity.GetComponent<SubWorldVisual>();
                record.Buildings.Add(new SubWorldBuildingSaveData
                {
                    Id = building.LocalObjectId.Value, Asset = building.BuildingAssetId,
                    X = position.x, Y = position.y, Z = position.z, Variant = visual.VariantIndex, Visual = visual.State
                });
            }
            data.Instances.Add(record);
        }
        return data;
    }

    /// <summary>每个小世界独立恢复，失败时只丢弃该实例并报告。</summary>
    /// <param name="data">待保存或恢复的资料。</param>
    /// <param name="report">本次读取的转换与跳过报告。</param>
    internal void RestoreWorldSave(SubWorldSaveData data, SaveLoadReport report)
    {
        if (data.Instances == null || data.Instances.Any(record => record == null || record.Id <= 0) ||
            data.Instances.Select(record => record.Id).Distinct().Count() != data.Instances.Count)
            throw new InvalidDataException("小世界编号缺失或重复");
        foreach (SubWorldInstanceSaveData record in data.Instances)
        {
            try { RestoreInstance(record); }
            catch (Exception error) when (error is InvalidDataException or SaveVersionTooNewException)
            { report.Skip("小世界 " + record.Id, record.Template, error.Message); }
        }
        nextInstanceId = Math.Max(data.NextId, data.Instances.Select(record => record.Id).DefaultIfEmpty(0).Max());
    }

    /// <summary>先核对外部资产与地图，再一次安装地图、对象、显示和导航。</summary>
    /// <param name="record">当前对象的存档记录。</param>
    private void RestoreInstance(SubWorldInstanceSaveData record)
    {
        if (record.Version != 1) throw new InvalidDataException("尚不支持的小世界实例版本");
        var template = ModClass.L.SubWorldTemplateLibrary.get(record.Template);
        if (template == null) throw new InvalidDataException("小世界模板已移除");
        var clock = ModClass.L.SubWorldClockProfileLibrary.get(template.clock_profile_id);
        var visual = ModClass.L.SubWorldVisualProfileLibrary.get(template.visual_profile_id);
        if (clock == null || visual == null || record.Map == null || record.Map.Width < 8 || record.Map.Height < 8 ||
            record.Map.Width > SubWorldSpatialLayout.MaxTemplateSize || record.Map.Height > SubWorldSpatialLayout.MaxTemplateSize ||
            record.Map.Tiles?.Length != checked(record.Map.Width * record.Map.Height) || record.LocalTime < 0 || record.Tick < 0 ||
            !clock.AllowsLocalSpeed(record.Speed) || !clock.AllowsLocalSpeed(record.RunningSpeed) || record.NextObjectId <= 0)
            throw new InvalidDataException("小世界地图、时钟或配置无效");
        int count = record.Map.Tiles.Length;
        foreach (var tile in record.Map.Tiles)
            if (AssetManager.tiles.get(tile.MainAssetId) == null ||
                !string.IsNullOrEmpty(tile.TopAssetId) && AssetManager.top_tiles.get(tile.TopAssetId) == null)
                throw new InvalidDataException("小世界地形资产已移除");
        foreach (var actor in record.Actors)
            if (AssetManager.actor_library.get(actor.Asset) == null || (uint)actor.Tile >= (uint)count || actor.Speed <= 0 ||
                actor.NextTile < -1 || actor.NextTile >= count || actor.Target < -1 || actor.Target >= count)
                throw new InvalidDataException("小世界人物资产或移动目标无效");
        if (record.Buildings.Select(building => building.Id).Distinct().Count() != record.Buildings.Count ||
            record.NextObjectId <= record.Buildings.Select(building => building.Id).DefaultIfEmpty(0).Max())
            throw new InvalidDataException("小世界建筑编号重复");
        foreach (var building in record.Buildings)
            if (building.Id <= 0 || AssetManager.buildings.get(building.Asset) == null)
                throw new InvalidDataException("小世界建筑资产或编号无效");
        var scene = new SubWorldGeneratedScene(record.Map,
            record.SpawnPoints.Select(entry => new SubWorldSpawnPoint(entry.Key, entry.Value)).ToArray());
        var runtime = new SubWorldRuntime(record.Id, template, record.Seed,
            new Model.SubWorldAnchor(record.AnchorX, record.AnchorY), scene, clock, visual, jobRunner);
        bool allocated = false;
        SubWorldWorldView view = null;
        try
        {
            runtime.InitializeScene(scene);
            foreach (var building in record.Buildings)
            {
                Entity entity = runtime.EntityStore.CreateEntity(new Position(building.X, building.Y, building.Z),
                    new SubWorldBuilding(new LocalObjectId(building.Id), building.Asset), new SubWorldVisual(building.Variant, building.Visual));
                runtime.Buildings.Register(entity);
            }
            runtime.LocalObjectIds.RestoreNextValue(record.NextObjectId);
            foreach (var actor in record.Actors)
            {
                var movement = new SubWorldMovement(actor.Speed, actor.Tile)
                {
                    TargetTileIndex = actor.Target, NextTileIndex = actor.NextTile
                };
                Entity entity = runtime.EntityStore.CreateEntity(new Position(actor.X, actor.Y, actor.Z),
                    new SubWorldActor(actor.Asset), new SubWorldVisual(actor.Variant, actor.Visual), movement);
                if (actor.Controllable) runtime.SetDebugControllableActor(entity);
            }
            runtime.Clock.RestoreWorldSave(record);
            runtime.Start();
            var slot = spatialLayout.Allocate(record.Id, record.Map.Width, record.Map.Height);
            allocated = true;
            view = new SubWorldWorldView(runtime, slot);
            runtimes.Add(record.Id, runtime);
            worldViews.Add(record.Id, view);
            navigationSection.AddRuntime(runtime);
        }
        catch
        {
            view?.Destroy();
            if (allocated) spatialLayout.Release(record.Id);
            runtime.Destroy();
            throw;
        }
    }
}
