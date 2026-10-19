using System.IO;
using System.Linq;

namespace Cultiway.Content.ActorComponents;

/// <summary>读取实际行驶距离，避免整趟行程从头重开。</summary>
public partial class Train
{
    /// <summary>保存轨道顺序和当前行驶位置。</summary>
    internal TrainMotionSaveData CaptureWorldSave() => new()
    {
        Tiles = _track_tiles.Select(tile => tile.tile_id).ToArray(), Distance = _lead_distance,
        Forward = _moving_forward, StopAtEnd = _stop_at_end, Finished = _finished_segment, Visible = _visible
    };

    /// <summary>重建轨道长度缓存，再恢复行驶位置与显示。</summary>
    /// <param name="data">待保存或恢复的资料。</param>
    internal void RestoreWorldSave(TrainMotionSaveData data)
    {
        if (data.Tiles.Any(tile => (uint)tile >= (uint)World.world.tiles_list.Length)) throw new InvalidDataException("列车轨道越界");
        SetTrackTiles(data.Tiles.Select(tile => World.world.tiles_list[tile]).ToList());
        _lead_distance = data.Distance;
        _moving_forward = data.Forward;
        _stop_at_end = data.StopAtEnd;
        _finished_segment = data.Finished;
        SetVisibility(data.Visible);
    }
}

/// <summary>列车运动的稳定记录。</summary>
internal sealed class TrainMotionSaveData
{
    /// <summary>当前轨道的顺序。</summary>
    public int[] Tiles;
    /// <summary>车头已行驶距离。</summary>
    public float Distance;
    /// <summary>当前行驶方向。</summary>
    public bool Forward;
    /// <summary>是否在轨道末端停车。</summary>
    public bool StopAtEnd;
    /// <summary>是否已经到达本段终点。</summary>
    public bool Finished;
    /// <summary>是否显示列车。</summary>
    public bool Visible;
}
