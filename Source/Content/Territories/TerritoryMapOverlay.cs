using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Cultiway.Content.Territories;

/// <summary>在原版地图图层显示各块地区，工作线程只读取已发布的颜色。</summary>
internal static class TerritoryMapOverlay
{
    /// <summary>定制地区地图开关的稳定编号。</summary>
    internal const string ToggleName = "cultiway_territories_layer";
    /// <summary>地区范围、选择和名称变化时通知名称图层重建。</summary>
    internal static int Revision { get; private set; }
    /// <summary>区分不同区域的领域颜色。</summary>
    private static readonly Color32[] palette =
    {
        new(70, 190, 245, 75), new(120, 220, 120, 75), new(220, 130, 245, 75),
        new(245, 150, 80, 75), new(70, 220, 205, 75), new(240, 110, 155, 75)
    };
    /// <summary>当前不可变的显示快照。</summary>
    private static Snapshot snapshot = new(0);

    /// <summary>只在编辑和选择改变时生成地区格子的颜色，不复制全图地形。</summary>
    /// <param name="regions">当前世界的地区。</param>
    /// <param name="selected">正在编辑的地区。</param>
    /// <param name="zoneCount">原版地区格子数量。</param>
    internal static void Publish(IEnumerable<CustomTerritory> regions, CustomTerritory selected, int zoneCount)
    {
        var updated = new Snapshot(zoneCount);
        foreach (CustomTerritory region in regions)
        {
            Color32 color = region == selected ? new Color32(255, 235, 120, 100) : palette[(region.Id - 1) % palette.Length];
            foreach (int zone in region.Zones)
            {
                updated.Ids[zone] = region.Id;
                updated.Colors[zone] = color;
            }
        }
        Volatile.Write(ref snapshot, updated);
        Revision++;
        ModClass.I.CustomMapModeManager.InvalidateCurrentMapMode();
    }

    /// <summary>逐地块读取填色，并仅在不同地区相接的位置描边。</summary>
    /// <param name="tile">原版正在绘制的地块。</param>
    /// <param name="color">输出填色或边线颜色。</param>
    internal static void Render(WorldTile tile, ref Color32 color)
    {
        Snapshot view = Volatile.Read(ref snapshot);
        TileZone zone = tile.zone;
        if ((uint)zone.id >= (uint)view.Ids.Length || view.Ids[zone.id] == 0)
        {
            color = new Color32(0, 0, 0, 0);
            return;
        }
        int id = view.Ids[zone.id];
        color = view.Colors[zone.id];
        if (tile.x % 8 == 0 && RegionId(view, zone.zone_left) != id
            || tile.x % 8 == 7 && RegionId(view, zone.zone_right) != id
            || tile.y % 8 == 0 && RegionId(view, zone.zone_down) != id
            || tile.y % 8 == 7 && RegionId(view, zone.zone_up) != id) color.a = 230;
    }

    /// <summary>地图边缘不存在相邻格子，按未划定区域处理。</summary>
    /// <param name="view">本次绘制的快照。</param>
    /// <param name="zone">相邻格子。</param>
    /// <returns>区域编号或零。</returns>
    private static int RegionId(Snapshot view, TileZone zone) => zone == null ? 0 : view.Ids[zone.id];

    /// <summary>换世界后立即移除旧颜色。</summary>
    internal static void Clear()
    {
        Volatile.Write(ref snapshot, new Snapshot(0));
        Revision++;
    }

    /// <summary>一起发布区域编号和颜色，避免渲染读到编辑中的数据。</summary>
    private sealed class Snapshot
    {
        /// <summary>按格子编号排列的区域编号。</summary>
        internal readonly int[] Ids;
        /// <summary>按格子编号排列的填色。</summary>
        internal readonly Color32[] Colors;
        /// <summary>分配本世界的格子显示数据。</summary>
        /// <param name="count">格子数量。</param>
        internal Snapshot(int count) { Ids = new int[count]; Colors = new Color32[count]; }
    }
}
