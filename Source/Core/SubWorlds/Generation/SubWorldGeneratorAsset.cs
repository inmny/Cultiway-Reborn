using System;
using Cultiway.Core.SubWorlds.Model;

namespace Cultiway.Core.SubWorlds.Generation;

/// <summary>
/// 根据模板、种子和创建参数生成小世界初始场景的资产基类。
/// </summary>
public abstract class SubWorldGeneratorAsset : Asset
{
    /// <summary>验证生成器具备可注册的资产 ID。</summary>
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("SubWorld Generator 缺少 ID");
    }

    /// <summary>创建一个尚未绑定 Runtime 的完整初始场景。</summary>
    internal abstract SubWorldGeneratedScene Generate(
        SubWorldTemplateAsset template,
        int seed,
        SubWorldAnchor anchor,
        SubWorldCreationParameters parameters);
}

/// <summary>保存生成器构造完成、等待 Runtime 接管的初始场景。</summary>
internal sealed class SubWorldGeneratedScene
{
    internal SubWorldGeneratedScene(
        SubWorldMapData mapData,
        SubWorldSpawnPoint[] spawnPoints,
        SubWorldActorPlacement[] actorPlacements = null,
        SubWorldBuildingPlacement[] buildingPlacements = null)
    {
        MapData = mapData ?? throw new ArgumentNullException(nameof(mapData));
        SpawnPoints = spawnPoints ?? throw new ArgumentNullException(nameof(spawnPoints));
        ActorPlacements = actorPlacements ?? Array.Empty<SubWorldActorPlacement>();
        BuildingPlacements = buildingPlacements ?? Array.Empty<SubWorldBuildingPlacement>();
    }

    /// <summary>生成完成的地图数据；所有权将在创建时交给 Runtime。</summary>
    internal SubWorldMapData MapData { get; }

    /// <summary>用途层可按名称查询的出生点。</summary>
    internal SubWorldSpawnPoint[] SpawnPoints { get; }

    /// <summary>生成器声明的初始 Actor。</summary>
    internal SubWorldActorPlacement[] ActorPlacements { get; }

    /// <summary>生成器声明的初始 Building。</summary>
    internal SubWorldBuildingPlacement[] BuildingPlacements { get; }
}
