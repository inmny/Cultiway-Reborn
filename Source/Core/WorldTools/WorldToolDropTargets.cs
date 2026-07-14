using System.Collections.Generic;

namespace Cultiway.Core.WorldTools;

/// <summary>为世界工具雨滴取得落点格子内稳定的存活角色快照。</summary>
internal static class WorldToolDropTargets
{
    /// <summary>
    /// 仅快照指定格子中当前存活的角色。先复制原版 <see cref="WorldTile.doUnits(System.Action{Actor})"/>
    /// 枚举结果，避免后续效果改变格子单位列表时干扰本次结算。
    /// </summary>
    public static List<Actor> SnapshotAliveActors(WorldTile tile)
    {
        if (tile == null || !tile.hasUnits()) return new List<Actor>();

        var actors = new List<Actor>(tile.countUnits());
        tile.doUnits(actor => actors.Add(actor));
        return actors;
    }
}
