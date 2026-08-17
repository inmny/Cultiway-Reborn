using System;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SubWorlds.Objects;

/// <summary>声明一个 Entity 属于小世界 Building 类别，并引用原版 BuildingAsset。</summary>
internal struct SubWorldBuilding : IComponent
{
    internal SubWorldBuilding(LocalObjectId localObjectId, string buildingAssetId)
    {
        if (!localObjectId.IsValid)
            throw new ArgumentException("Building LocalObjectId 无效", nameof(localObjectId));
        if (string.IsNullOrWhiteSpace(buildingAssetId))
            throw new ArgumentException("BuildingAsset ID 为空", nameof(buildingAssetId));
        LocalObjectId = localObjectId;
        BuildingAssetId = buildingAssetId;
    }

    internal LocalObjectId LocalObjectId;
    internal string BuildingAssetId;
}
