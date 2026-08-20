using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.SpiritVeins;

/// <summary>一条完整风水龙脉；它是可识别的世界对象，但不拥有任何成员。</summary>
public sealed class SpiritVein : MetaObject<SpiritVeinData>
{
    private static readonly Actor[] NoMembers = Array.Empty<Actor>();

    public override MetaType meta_type => MetaTypeExtend.SpiritVein.Back();
    public override BaseSystemManager manager => WorldboxGame.I?.SpiritVeins;

    /// <summary>当前地势布局中的编号，脉节与结穴地通过它关联龙脉。</summary>
    public int Id => data.TopologyId;
    public string Name
    {
        get => name;
        internal set => data.name = value ?? string.Empty;
    }
    public DragonVeinScale Scale => data.Scale;
    public int SourceCenterTileId => data.SourceCenterTileId;
    public int OutletTileId => data.OutletTileId;
    public int[] SourceTileIds => data.SourceTileIds;
    public int MainGroundId => data.MainGroundId;
    public string SourceRegionName => data.SourceRegionName;
    public string OutletRegionName => data.OutletRegionName;
    public ElementComposition Composition => data.Composition;
    public List<int> BranchIds => data.BranchIds;
    public List<int> SectionIds => data.SectionIds;
    public List<int> GroundIds => data.GroundIds;
    public List<int> EyeIds => data.EyeIds;

    internal void Setup(SpiritVeinDraft layout)
    {
        ApplyLayout(layout);
        ColorLibrary colors = getColorLibrary();
        int colorCount = colors?.list?.Count ?? 0;
        data.setColorID(colorCount == 0 ? 0 : (layout.Id - 1) % colorCount);
        unDirty();
    }

    internal void ApplyLayout(SpiritVeinDraft layout)
    {
        if (layout == null) throw new ArgumentNullException(nameof(layout));

        data.TopologyId = layout.Id;
        data.name = layout.Name ?? string.Empty;
        data.Scale = layout.Scale;
        data.SourceCenterTileId = layout.SourceCenterTileId;
        data.OutletTileId = layout.OutletTileId;
        data.SourceTileIds = layout.SourceTileIds ?? Array.Empty<int>();
        data.MainGroundId = layout.MainGroundId;
        data.SourceRegionName = layout.SourceRegionName ?? string.Empty;
        data.OutletRegionName = layout.OutletRegionName ?? string.Empty;
        data.Composition = layout.Composition;
        ReplaceIds(data.BranchIds, layout.BranchIds);
        ReplaceIds(data.SectionIds, layout.SectionIds);
        ReplaceIds(data.GroundIds, layout.GroundIds);
        ReplaceIds(data.EyeIds, layout.EyeIds);
        stats_dirty_version++;
    }

    public override bool isReadyForRemoval() => false;
    public override void listUnit(Actor pActor) { }
    public override int countUnits() => 0;
    public override IEnumerable<Actor> getUnits() => NoMembers;
    public override void generateBanner() { }
    public override ColorLibrary getColorLibrary() => AssetManager.families_colors_library;

    private static void ReplaceIds(List<int> target, List<int> source)
    {
        target.Clear();
        target.AddRange(source);
    }
}
