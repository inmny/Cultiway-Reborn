using System.Collections.Generic;
using Cultiway.Core;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.SpiritVeins;

/// <summary>龙脉当前布局与可消耗资源；不把后台任务写入存档。</summary>
internal sealed class SpiritVeinWorldSaveData
{
    /// <summary>地图宽度。</summary>
    public int Width;
    /// <summary>地图高度。</summary>
    public int Height;
    /// <summary>原版格式的龙脉身份和命名资料。</summary>
    public List<JObject> Veins = new();
    /// <summary>下次分配的龙脉身份。</summary>
    public long NextId;
    /// <summary>等待地势重流的地块。</summary>
    public int[] PendingTiles;
    /// <summary>地势变化已累计月份。</summary>
    public int PendingMonths;
    /// <summary>供气轮转位置。</summary>
    public int SupplyCursor;
    /// <summary>支脉。</summary>
    public List<SpiritVeinBranchSaveData> Branches = new();
    /// <summary>脉节储量、污染与通畅程度。</summary>
    public List<SpiritVeinSectionSaveData> Sections = new();
    /// <summary>结穴地。</summary>
    public List<GatheringGroundSaveData> Grounds = new();
    /// <summary>灵眼。</summary>
    public List<SpiritVeinEyeSaveData> Eyes = new();
    /// <summary>地块到脉域的归属。</summary>
    public SpiritVeinFieldSaveData Field;
}

/// <summary>SpiritVeinBranch 的稳定资料。</summary>
internal sealed class SpiritVeinBranchSaveData
{
    /// <summary>保存 Id 的当前值。</summary>
    public int Id;
    /// <summary>保存 VeinId 的当前值。</summary>
    public int VeinId;
    /// <summary>保存 Name 的当前值。</summary>
    public string Name;
    /// <summary>保存 Scale 的当前值。</summary>
    public SpiritBranchScale Scale;
    /// <summary>保存 SourceCenterTileId 的当前值。</summary>
    public int SourceCenterTileId;
    /// <summary>保存 JoinTileId 的当前值。</summary>
    public int JoinTileId;
    /// <summary>保存 Composition 的当前值。</summary>
    public ElementComposition Composition;
    /// <summary>保存 SectionIds 的当前值。</summary>
    public List<int> SectionIds;

    /// <summary>采集公开的稳定资料。</summary>
    /// <param name="value">当前值。</param>
    internal static SpiritVeinBranchSaveData Capture(SpiritVeinBranch value) => new()
    {
        Id = value.Id,
        VeinId = value.VeinId,
        Name = value.Name,
        Scale = value.Scale,
        SourceCenterTileId = value.SourceCenterTileId,
        JoinTileId = value.JoinTileId,
        Composition = value.Composition,
        SectionIds = value.SectionIds,
    };

    /// <summary>重建当前玩法对象，不重置储量与污染。</summary>
    internal SpiritVeinBranch Restore()
    {
        var value = new SpiritVeinBranch(Id, VeinId, SourceCenterTileId, JoinTileId, Scale, Composition);
        value.Name = Name;
        value.Scale = Scale;
        value.SourceCenterTileId = SourceCenterTileId;
        value.JoinTileId = JoinTileId;
        value.Composition = Composition;
        value.SectionIds.AddRange(SectionIds);
        return value;
    }
}

/// <summary>SpiritVeinSection 的稳定资料。</summary>
internal sealed class SpiritVeinSectionSaveData
{
    /// <summary>保存 Id 的当前值。</summary>
    public int Id;
    /// <summary>保存 VeinId 的当前值。</summary>
    public int VeinId;
    /// <summary>保存 BranchId 的当前值。</summary>
    public int BranchId;
    /// <summary>保存 Kind 的当前值。</summary>
    public VeinSectionKind Kind;
    /// <summary>保存 RegionName 的当前值。</summary>
    public string RegionName;
    /// <summary>保存 CenterTileId 的当前值。</summary>
    public int CenterTileId;
    /// <summary>保存 TileIds 的当前值。</summary>
    public int[] TileIds;
    /// <summary>保存 UpstreamSectionIds 的当前值。</summary>
    public int[] UpstreamSectionIds;
    /// <summary>保存 DownstreamSectionIds 的当前值。</summary>
    public int[] DownstreamSectionIds;
    /// <summary>保存 Capacity 的当前值。</summary>
    public float Capacity;
    /// <summary>保存 CurrentAmount 的当前值。</summary>
    public float CurrentAmount;
    /// <summary>保存 MonthlyRecovery 的当前值。</summary>
    public float MonthlyRecovery;
    /// <summary>保存 MonthlySupply 的当前值。</summary>
    public float MonthlySupply;
    /// <summary>保存 MonthlyTransfer 的当前值。</summary>
    public float MonthlyTransfer;
    /// <summary>保存 Patency 的当前值。</summary>
    public float Patency;
    /// <summary>保存 Purity 的当前值。</summary>
    public float Purity;
    /// <summary>保存 Composition 的当前值。</summary>
    public ElementComposition Composition;
    /// <summary>保存 Status 的当前值。</summary>
    public VeinSectionStatus Status;

    /// <summary>采集公开的稳定资料。</summary>
    /// <param name="value">当前值。</param>
    internal static SpiritVeinSectionSaveData Capture(SpiritVeinSection value) => new()
    {
        Id = value.Id,
        VeinId = value.VeinId,
        BranchId = value.BranchId,
        Kind = value.Kind,
        RegionName = value.RegionName,
        CenterTileId = value.CenterTileId,
        TileIds = value.TileIds,
        UpstreamSectionIds = value.UpstreamSectionIds,
        DownstreamSectionIds = value.DownstreamSectionIds,
        Capacity = value.Capacity,
        CurrentAmount = value.CurrentAmount,
        MonthlyRecovery = value.MonthlyRecovery,
        MonthlySupply = value.MonthlySupply,
        MonthlyTransfer = value.MonthlyTransfer,
        Patency = value.Patency,
        Purity = value.Purity,
        Composition = value.Composition,
        Status = value.Status,
    };

    /// <summary>重建当前玩法对象，不重置储量与污染。</summary>
    internal SpiritVeinSection Restore()
    {
        var value = new SpiritVeinSection(Id, VeinId, BranchId, Kind, CenterTileId, TileIds, Capacity, MonthlyRecovery, MonthlySupply, MonthlyTransfer, Patency, Composition);
        value.Kind = Kind;
        value.RegionName = RegionName;
        value.CenterTileId = CenterTileId;
        value.TileIds = TileIds;
        value.UpstreamSectionIds = UpstreamSectionIds;
        value.DownstreamSectionIds = DownstreamSectionIds;
        value.Capacity = Capacity;
        value.CurrentAmount = CurrentAmount;
        value.MonthlyRecovery = MonthlyRecovery;
        value.MonthlySupply = MonthlySupply;
        value.MonthlyTransfer = MonthlyTransfer;
        value.Patency = Patency;
        value.Purity = Purity;
        value.Composition = Composition;
        value.Status = Status;
        return value;
    }
}

/// <summary>GatheringGround 的稳定资料。</summary>
internal sealed class GatheringGroundSaveData
{
    /// <summary>保存 Id 的当前值。</summary>
    public int Id;
    /// <summary>保存 PrimaryVeinId 的当前值。</summary>
    public int PrimaryVeinId;
    /// <summary>保存 GuestVeinId 的当前值。</summary>
    public int GuestVeinId;
    /// <summary>保存 SectionId 的当前值。</summary>
    public int SectionId;
    /// <summary>保存 GuestSectionId 的当前值。</summary>
    public int GuestSectionId;
    /// <summary>保存 EyeId 的当前值。</summary>
    public int EyeId;
    /// <summary>保存 Name 的当前值。</summary>
    public string Name;
    /// <summary>保存 Kind 的当前值。</summary>
    public GatheringGroundKind Kind;
    /// <summary>保存 Quality 的当前值。</summary>
    public GatheringGroundQuality Quality;
    /// <summary>保存 CenterTileId 的当前值。</summary>
    public int CenterTileId;
    /// <summary>保存 TileIds 的当前值。</summary>
    public int[] TileIds;
    /// <summary>保存 HallTileIds 的当前值。</summary>
    public int[] HallTileIds;
    /// <summary>保存 RegionName 的当前值。</summary>
    public string RegionName;
    /// <summary>保存 Convergence 的当前值。</summary>
    public float Convergence;
    /// <summary>保存 Shelter 的当前值。</summary>
    public float Shelter;
    /// <summary>保存 Leakage 的当前值。</summary>
    public float Leakage;
    /// <summary>保存 FillRatio 的当前值。</summary>
    public float FillRatio;
    /// <summary>保存 Purity 的当前值。</summary>
    public float Purity;

    /// <summary>采集公开的稳定资料。</summary>
    /// <param name="value">当前值。</param>
    internal static GatheringGroundSaveData Capture(GatheringGround value) => new()
    {
        Id = value.Id,
        PrimaryVeinId = value.PrimaryVeinId,
        GuestVeinId = value.GuestVeinId,
        SectionId = value.SectionId,
        GuestSectionId = value.GuestSectionId,
        EyeId = value.EyeId,
        Name = value.Name,
        Kind = value.Kind,
        Quality = value.Quality,
        CenterTileId = value.CenterTileId,
        TileIds = value.TileIds,
        HallTileIds = value.HallTileIds,
        RegionName = value.RegionName,
        Convergence = value.Convergence,
        Shelter = value.Shelter,
        Leakage = value.Leakage,
        FillRatio = value.FillRatio,
        Purity = value.Purity,
    };

    /// <summary>重建当前玩法对象，不重置储量与污染。</summary>
    internal GatheringGround Restore()
    {
        var value = new GatheringGround(Id, PrimaryVeinId, GuestVeinId, SectionId, GuestSectionId, Kind, Quality, CenterTileId, TileIds, HallTileIds, Convergence, Shelter, Leakage);
        value.GuestVeinId = GuestVeinId;
        value.SectionId = SectionId;
        value.GuestSectionId = GuestSectionId;
        value.EyeId = EyeId;
        value.Name = Name;
        value.Kind = Kind;
        value.Quality = Quality;
        value.CenterTileId = CenterTileId;
        value.TileIds = TileIds;
        value.HallTileIds = HallTileIds;
        value.RegionName = RegionName;
        value.Convergence = Convergence;
        value.Shelter = Shelter;
        value.Leakage = Leakage;
        value.FillRatio = FillRatio;
        value.Purity = Purity;
        return value;
    }
}

/// <summary>SpiritVeinEye 的稳定资料。</summary>
internal sealed class SpiritVeinEyeSaveData
{
    /// <summary>保存 Id 的当前值。</summary>
    public int Id;
    /// <summary>保存 VeinId 的当前值。</summary>
    public int VeinId;
    /// <summary>保存 GroundId 的当前值。</summary>
    public int GroundId;
    /// <summary>保存 SectionId 的当前值。</summary>
    public int SectionId;
    /// <summary>保存 TileId 的当前值。</summary>
    public int TileId;
    /// <summary>保存 Name 的当前值。</summary>
    public string Name;
    /// <summary>保存 Manifestation 的当前值。</summary>
    public SpiritEyeManifestation Manifestation;
    /// <summary>保存 BaseConcentration 的当前值。</summary>
    public float BaseConcentration;
    /// <summary>保存 Composition 的当前值。</summary>
    public ElementComposition Composition;
    /// <summary>保存 FillRatio 的当前值。</summary>
    public float FillRatio;
    /// <summary>保存 Purity 的当前值。</summary>
    public float Purity;

    /// <summary>采集公开的稳定资料。</summary>
    /// <param name="value">当前值。</param>
    internal static SpiritVeinEyeSaveData Capture(SpiritVeinEye value) => new()
    {
        Id = value.Id,
        VeinId = value.VeinId,
        GroundId = value.GroundId,
        SectionId = value.SectionId,
        TileId = value.TileId,
        Name = value.Name,
        Manifestation = value.Manifestation,
        BaseConcentration = value.BaseConcentration,
        Composition = value.Composition,
        FillRatio = value.FillRatio,
        Purity = value.Purity,
    };

    /// <summary>重建当前玩法对象，不重置储量与污染。</summary>
    internal SpiritVeinEye Restore()
    {
        var value = new SpiritVeinEye(Id, VeinId, GroundId, SectionId, TileId, Manifestation, BaseConcentration, Composition);
        value.SectionId = SectionId;
        value.TileId = TileId;
        value.Name = Name;
        value.Manifestation = Manifestation;
        value.BaseConcentration = BaseConcentration;
        value.Composition = Composition;
        value.FillRatio = FillRatio;
        value.Purity = Purity;
        return value;
    }
}

/// <summary>龙脉地图的稳定数值数组。</summary>
internal sealed class SpiritVeinFieldSaveData
{
    /// <summary>保存 Width。</summary>
    public int Width;
    /// <summary>保存 Height。</summary>
    public int Height;
    /// <summary>保存 PrimaryVeinByTile。</summary>
    public int[] PrimaryVeinByTile;
    /// <summary>保存 SecondaryVeinByTile。</summary>
    public int[] SecondaryVeinByTile;
    /// <summary>保存 SectionByTile。</summary>
    public int[] SectionByTile;
    /// <summary>保存 SecondarySectionByTile。</summary>
    public int[] SecondarySectionByTile;
    /// <summary>保存 GroundByTile。</summary>
    public int[] GroundByTile;
    /// <summary>保存 FieldStrength。</summary>
    public float[] FieldStrength;
    /// <summary>保存 SecondaryStrength。</summary>
    public float[] SecondaryStrength;
    /// <summary>保存 FlowX。</summary>
    public float[] FlowX;
    /// <summary>保存 FlowY。</summary>
    public float[] FlowY;
    /// <summary>保存 Convergence。</summary>
    public float[] Convergence;
    /// <summary>保存 Shelter。</summary>
    public float[] Shelter;
    /// <summary>保存 Leakage。</summary>
    public float[] Leakage;

    /// <summary>复制归属和影响数值。</summary>
    /// <param name="value">当前值。</param>
    internal static SpiritVeinFieldSaveData Capture(SpiritVeinFieldSnapshot value) => new()
    {
        Width = value.Width,
        Height = value.Height,
        PrimaryVeinByTile = value.PrimaryVeinByTile,
        SecondaryVeinByTile = value.SecondaryVeinByTile,
        SectionByTile = value.SectionByTile,
        SecondarySectionByTile = value.SecondarySectionByTile,
        GroundByTile = value.GroundByTile,
        FieldStrength = value.FieldStrength,
        SecondaryStrength = value.SecondaryStrength,
        FlowX = value.FlowX,
        FlowY = value.FlowY,
        Convergence = value.Convergence,
        Shelter = value.Shelter,
        Leakage = value.Leakage,
    };

    /// <summary>重新发布只读脉域数据。</summary>
    internal SpiritVeinFieldSnapshot Restore() => new(Width, Height, PrimaryVeinByTile, SecondaryVeinByTile, SectionByTile, SecondarySectionByTile, GroundByTile, FieldStrength, SecondaryStrength, FlowX, FlowY, Convergence, Shelter, Leakage);
}
