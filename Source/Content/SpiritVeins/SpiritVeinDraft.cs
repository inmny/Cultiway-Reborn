using System;
using System.Collections.Generic;
using Cultiway.Core;

namespace Cultiway.Content.SpiritVeins;

/// <summary>后台生成阶段使用的纯布局草稿，不接触游戏对象管理器。</summary>
internal sealed class SpiritVeinDraft
{
    internal SpiritVeinDraft(
        int id,
        int sourceCenterTileId,
        int outletTileId,
        int[] sourceTileIds,
        DragonVeinScale scale,
        ElementComposition composition)
    {
        Id = id;
        SourceCenterTileId = sourceCenterTileId;
        OutletTileId = outletTileId;
        SourceTileIds = sourceTileIds ?? Array.Empty<int>();
        Scale = scale;
        Composition = composition;
    }

    internal int Id { get; }
    internal string Name { get; set; } = string.Empty;
    internal DragonVeinScale Scale { get; set; }
    internal int SourceCenterTileId { get; set; }
    internal int OutletTileId { get; set; }
    internal int[] SourceTileIds { get; set; }
    internal int MainGroundId { get; set; } = -1;
    internal string SourceRegionName { get; set; } = string.Empty;
    internal string OutletRegionName { get; set; } = string.Empty;
    internal ElementComposition Composition { get; set; }
    internal List<int> BranchIds { get; } = new();
    internal List<int> SectionIds { get; } = new();
    internal List<int> GroundIds { get; } = new();
    internal List<int> EyeIds { get; } = new();
}
