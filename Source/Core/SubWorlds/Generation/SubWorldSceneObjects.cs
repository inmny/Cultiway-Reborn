using System;
using Cultiway.Core.SubWorlds.Objects;

namespace Cultiway.Core.SubWorlds.Generation;

/// <summary>Generator 声明的初始 Actor 放置。</summary>
internal readonly struct SubWorldActorPlacement
{
    internal SubWorldActorPlacement(
        string actorAssetId,
        int tileIndex,
        float moveSpeedTilesPerSecond,
        bool debugControllable = false,
        int visualVariantIndex = 0,
        SubWorldVisualState visualState = SubWorldVisualState.Default)
    {
        if (string.IsNullOrWhiteSpace(actorAssetId))
            throw new ArgumentException("ActorAsset ID 为空", nameof(actorAssetId));
        if (tileIndex < 0) throw new ArgumentOutOfRangeException(nameof(tileIndex));
        if (moveSpeedTilesPerSecond <= 0f)
            throw new ArgumentOutOfRangeException(nameof(moveSpeedTilesPerSecond));
        if (visualVariantIndex < 0) throw new ArgumentOutOfRangeException(nameof(visualVariantIndex));

        ActorAssetId = actorAssetId;
        TileIndex = tileIndex;
        MoveSpeedTilesPerSecond = moveSpeedTilesPerSecond;
        DebugControllable = debugControllable;
        VisualVariantIndex = visualVariantIndex;
        VisualState = visualState;
    }

    internal string ActorAssetId { get; }
    internal int TileIndex { get; }
    internal float MoveSpeedTilesPerSecond { get; }
    internal bool DebugControllable { get; }
    internal int VisualVariantIndex { get; }
    internal SubWorldVisualState VisualState { get; }
}

/// <summary>Generator 声明的初始 Building 放置。</summary>
internal readonly struct SubWorldBuildingPlacement
{
    internal SubWorldBuildingPlacement(
        LocalObjectId localObjectId,
        string buildingAssetId,
        int anchorTileIndex,
        int visualVariantIndex = 0,
        SubWorldVisualState visualState = SubWorldVisualState.Default)
    {
        if (!localObjectId.IsValid)
            throw new ArgumentException("Building LocalObjectId 无效", nameof(localObjectId));
        if (string.IsNullOrWhiteSpace(buildingAssetId))
            throw new ArgumentException("BuildingAsset ID 为空", nameof(buildingAssetId));
        if (anchorTileIndex < 0) throw new ArgumentOutOfRangeException(nameof(anchorTileIndex));
        if (visualVariantIndex < 0) throw new ArgumentOutOfRangeException(nameof(visualVariantIndex));

        LocalObjectId = localObjectId;
        BuildingAssetId = buildingAssetId;
        AnchorTileIndex = anchorTileIndex;
        VisualVariantIndex = visualVariantIndex;
        VisualState = visualState;
    }

    internal LocalObjectId LocalObjectId { get; }
    internal string BuildingAssetId { get; }
    internal int AnchorTileIndex { get; }
    internal int VisualVariantIndex { get; }
    internal SubWorldVisualState VisualState { get; }
}
