namespace Cultiway.Core.SubWorlds.Model;

/// <summary>
/// 保存一个小世界 Runtime 私有的格子地图和入口、出口元数据。
/// </summary>
internal sealed class SubWorldMapData
{
    /// <summary>地图宽度，单位为格。</summary>
    public int Width = 0;

    /// <summary>地图高度，单位为格。</summary>
    public int Height = 0;

    /// <summary>按 <c>index = y * Width + x</c> 排列的格子数据。</summary>
    public SubWorldTile[] Tiles = [];

    /// <summary>入口格子的 row-major 索引。</summary>
    public int[] EntryTileIndices = [];

    /// <summary>出口格子的 row-major 索引。</summary>
    public int[] ExitTileIndices = [];
}
