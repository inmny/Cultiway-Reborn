using System.Collections.Generic;
using Cultiway.Core;

namespace Cultiway.Content.SpiritVeins;

/// <summary>汇入干龙的一片支龙脉域。</summary>
public sealed class SpiritVeinBranch
{
    internal SpiritVeinBranch(
        int id,
        int veinId,
        int sourceCenterTileId,
        int joinTileId,
        SpiritBranchScale scale,
        ElementComposition composition)
    {
        Id = id;
        VeinId = veinId;
        SourceCenterTileId = sourceCenterTileId;
        JoinTileId = joinTileId;
        Scale = scale;
        Composition = composition;
    }

    public int Id { get; }
    public int VeinId { get; }
    public string Name { get; internal set; } = string.Empty;
    public SpiritBranchScale Scale { get; internal set; }
    public int SourceCenterTileId { get; internal set; }
    public int JoinTileId { get; internal set; }
    public ElementComposition Composition { get; internal set; }
    public List<int> SectionIds { get; } = new();
}
