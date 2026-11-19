using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cultiway.Content.Railways;
using Cultiway.Core.Performance;
using UnityEngine;

namespace Cultiway.Content.MapModeVisuals;

/// <summary>仅显示所选车站所在网络，复用原版地图覆盖层。</summary>
internal static class RailwayMapOverlay
{
    /// <summary>所选车站。</summary>
    private static long selected;
    /// <summary>下次刷新时刻。</summary>
    private static double nextRefresh;
    /// <summary>只包含选中网络轨道的颜色，不复制世界地形。</summary>
    private static Dictionary<int, Color32> colors = new();

    /// <summary>选择网络并打开原版地图开关。</summary>
    /// <param name="station">选中的实际车站。</param>
    internal static void Select(long station)
    {
        selected = station; nextRefresh = 0;
        if (!PlayerConfig.optionBoolEnabled(MapModes.Railway.toggle_name))
            AssetManager.powers.get(MapModes.Railway.toggle_name).toggle_action(MapModes.Railway.toggle_name);
        Tick();
    }

    /// <summary>只在地图打开时低频读取工程，关图后不扫描全网。</summary>
    internal static void Tick()
    {
        if (selected <= 0 || !PlayerConfig.optionBoolEnabled(MapModes.Railway.toggle_name) || SimulationTime.Now < nextRefresh) return;
        nextRefresh = SimulationTime.Now + 2;
        var updated = new Dictionary<int, Color32>();
        var links = TrainTrackRepairSystem.InspectLines();
        var network = new HashSet<long> { selected };
        bool changed;
        do
        {
            changed = false;
            foreach (var link in links.Where(l => l.State != RailwayLinkState.Revoked))
                if (network.Contains(link.StationA) || network.Contains(link.StationB))
                { changed |= network.Add(link.StationA); changed |= network.Add(link.StationB); }
        } while (changed);
        foreach (var link in links.Where(l => network.Contains(l.StationA) && network.Contains(l.StationB)))
        {
            Color32 color = link.State switch
            {
                RailwayLinkState.Normal => new(65, 230, 115, 220),
                RailwayLinkState.Construction or RailwayLinkState.Repairing => new(255, 205, 60, 220),
                RailwayLinkState.Damaged => new(255, 65, 50, 230),
                _ => new(160, 160, 160, 210)
            };
            foreach (int tile in link.Tiles) updated[tile] = color;
        }
        Volatile.Write(ref colors, updated);
        ModClass.I.CustomMapModeManager.InvalidateCurrentMapMode();
    }

    /// <summary>地图渲染只查颜色，不触碰领域对象。</summary>
    /// <param name="tile">当前地块。</param>
    /// <param name="color">输出覆盖颜色。</param>
    internal static void Render(WorldTile tile, ref Color32 color)
        => color = Volatile.Read(ref colors).TryGetValue(tile.tile_id, out var value) ? value : new Color32(0, 0, 0, 0);

    /// <summary>清世界时移除选区。</summary>
    internal static void Clear() { selected = 0; nextRefresh = 0; Volatile.Write(ref colors, new Dictionary<int, Color32>()); }
}
