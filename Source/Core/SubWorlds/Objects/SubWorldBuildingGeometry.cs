using System;
using Cultiway.Core.Components;
using Cultiway.Core.SubWorlds.Runtime;
using UnityEngine;

namespace Cultiway.Core.SubWorlds.Objects;

/// <summary>由 Building anchor 与原版 BuildingAsset.fundament 派生几何边界。</summary>
internal static class SubWorldBuildingGeometry
{
    internal static SubWorldBuildingBounds GetBounds(
        SubWorldGrid grid,
        in Position position,
        BuildingAsset asset)
    {
        int anchorX = Mathf.FloorToInt(position.value.x);
        int anchorY = Mathf.FloorToInt(position.value.y);
        SubWorldBuildingBounds bounds = GetBounds(anchorX, anchorY, asset.fundament);
        if (!grid.Contains(bounds.MinX, bounds.MinY) || !grid.Contains(bounds.MaxX, bounds.MaxY))
            throw new InvalidOperationException(
                $"SubWorld Building footprint 超出地图: asset={asset.id}, bounds={bounds}");
        return bounds;
    }

    internal static SubWorldBuildingBounds GetBounds(int anchorX, int anchorY, BuildingFundament fundament)
    {
        if (fundament == null) throw new ArgumentNullException(nameof(fundament));
        return new SubWorldBuildingBounds(
            anchorX - fundament.left,
            anchorY - fundament.bottom,
            anchorX + fundament.right,
            anchorY + fundament.top);
    }

}

internal readonly struct SubWorldBuildingBounds
{
    internal SubWorldBuildingBounds(int minX, int minY, int maxX, int maxY)
    {
        if (maxX < minX || maxY < minY) throw new ArgumentException("Building bounds 无效");
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    internal int MinX { get; }
    internal int MinY { get; }
    internal int MaxX { get; }
    internal int MaxY { get; }
    internal int Width => MaxX - MinX + 1;
    internal int Height => MaxY - MinY + 1;
    internal int TileCount => Width * Height;

    public override string ToString() => $"({MinX},{MinY})-({MaxX},{MaxY})";
}
