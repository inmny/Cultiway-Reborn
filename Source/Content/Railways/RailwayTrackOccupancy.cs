using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Content.Railways;

/// <summary>离站前一次取得整个区间，交叉和共用格也只能由一车占用。</summary>
internal static class RailwayTrackOccupancy
{
    /// <summary>实际运行中的占用者。</summary>
    private static readonly Dictionary<int, long> owners = new();
    /// <summary>站内排队的首次申请顺序。</summary>
    private static readonly Dictionary<long, (long order, HashSet<int> tiles, int priority)> waiting = new();
    /// <summary>逻辑线程上的递增申请顺序。</summary>
    private static long nextOrder;

    /// <summary>按先申请、再按车辆编号排队，检查全部成功后才写入。</summary>
    /// <param name="train">申请离站的车头编号。</param>
    /// <param name="track">下一站区间的完整路径。</param>
    /// <param name="priority">普通运输为一，贸易为零；同级仍按首次申请排队。</param>
    internal static bool TryAcquire(long train, IReadOnlyList<WorldTile> track, int priority = 1)
    {
        var tiles = TrackTiles(track);
        if (!waiting.TryGetValue(train, out var entry)) entry = (++nextOrder, tiles, priority);
        else entry.tiles = tiles;
        entry.priority = priority;
        waiting[train] = entry;
        if (tiles.Any(t => owners.TryGetValue(t, out long owner) && owner != train)) return false;
        if (waiting.Any(p => p.Key != train &&
                (p.Value.priority > priority || (p.Value.priority == priority &&
                (p.Value.order < entry.order || (p.Value.order == entry.order && p.Key < train)))) &&
                p.Value.tiles.Overlaps(tiles))) return false;
        foreach (int tile in tiles) owners[tile] = train;
        waiting.Remove(train);
        return true;
    }

    /// <summary>恢复仍在区间内的列车；冲突时由调用者处理异常行程。</summary>
    /// <param name="train">存档中的车头编号。</param>
    /// <param name="track">尚未释放的区间。</param>
    internal static bool Restore(long train, IReadOnlyList<WorldTile> track)
    {
        var tiles = TrackTiles(track);
        if (tiles.Any(t => owners.TryGetValue(t, out long owner) && owner != train)) return false;
        foreach (int tile in tiles) owners[tile] = train;
        return true;
    }

    /// <summary>保存站内排队顺序。</summary>
    /// <param name="train">车头编号。</param>
    internal static long GetQueueOrder(long train)
        => waiting.TryGetValue(train, out var entry) ? entry.order : 0;

    /// <summary>读档只恢复排队，不抢占运行区间。</summary>
    /// <param name="train">车头编号。</param>
    /// <param name="track">正在等待的区间。</param>
    /// <param name="order">保存时的首次申请顺序。</param>
    /// <param name="priority">从本次实际任务用途恢复的优先级。</param>
    internal static void RestoreQueue(long train, IReadOnlyList<WorldTile> track, long order, int priority = 1)
    {
        waiting[train] = (order, TrackTiles(track), priority);
        nextOrder = System.Math.Max(nextOrder, order);
    }

    /// <summary>站内隐藏存车不占到发线路，也不参加站外冲突。</summary>
    /// <param name="track">实际运行路径。</param>
    private static HashSet<int> TrackTiles(IReadOnlyList<WorldTile> track)
        => new(track.Where(t => t.building?.asset != Buildings.TrainStation).Select(t => t.tile_id));

    /// <summary>维修改线前确认旧线没有仍在运行的车。</summary>
    /// <param name="track">需要改建的旧路径。</param>
    internal static bool IsOccupied(IReadOnlyList<WorldTile> track)
        => track.Any(t => owners.ContainsKey(t.tile_id));

    /// <summary>废弃旧线仍有列车占用时，原版道路衰退不能清除它。</summary>
    /// <param name="tile">待检查的铁轨格。</param>
    internal static bool IsOccupied(WorldTile tile) => owners.ContainsKey(tile.tile_id);

    /// <summary>车尾进站、行程结束或毁车后释放占用及排队记录。</summary>
    /// <param name="train">车头编号。</param>
    internal static void Release(long train)
    {
        waiting.Remove(train);
        foreach (int tile in owners.Where(p => p.Value == train).Select(p => p.Key).ToArray()) owners.Remove(tile);
    }

    /// <summary>切换世界时清空旧地图引用。</summary>
    internal static void Clear()
    {
        owners.Clear();
        waiting.Clear();
        nextOrder = 0;
    }
}
