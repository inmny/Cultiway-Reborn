using System;
using System.Collections.Generic;
using Cultiway.Core;

namespace Cultiway.Content.SpiritVeins;

/// <summary>龙脉作为世界对象保存的当前布局与显示数据。</summary>
public sealed class SpiritVeinData : MetaObjectData
{
    public int TopologyId;
    public DragonVeinScale Scale;
    public int SourceCenterTileId;
    public int OutletTileId;
    public int[] SourceTileIds = Array.Empty<int>();
    public int MainGroundId = -1;
    public string SourceRegionName = string.Empty;
    public string OutletRegionName = string.Empty;
    public ElementComposition Composition;
    public List<int> BranchIds = new();
    public List<int> SectionIds = new();
    public List<int> GroundIds = new();
    public List<int> EyeIds = new();
}
