using System;
using UnityEngine;

namespace Cultiway.Content.SpiritVeins;

/// <summary>山环水抱、能够聚气并容纳明堂的一片结穴地。</summary>
public sealed class GatheringGround
{
    internal GatheringGround(
        int id,
        int primaryVeinId,
        int guestVeinId,
        int sectionId,
        int guestSectionId,
        GatheringGroundKind kind,
        GatheringGroundQuality quality,
        int centerTileId,
        int[] tileIds,
        int[] hallTileIds,
        float convergence,
        float shelter,
        float leakage)
    {
        Id = id;
        PrimaryVeinId = primaryVeinId;
        GuestVeinId = guestVeinId;
        SectionId = sectionId;
        GuestSectionId = guestSectionId;
        Kind = kind;
        Quality = quality;
        CenterTileId = centerTileId;
        TileIds = tileIds ?? Array.Empty<int>();
        HallTileIds = hallTileIds ?? Array.Empty<int>();
        Convergence = Mathf.Clamp01(convergence);
        Shelter = Mathf.Clamp01(shelter);
        Leakage = Mathf.Clamp01(leakage);
    }

    public int Id { get; }
    public int PrimaryVeinId { get; }
    public int GuestVeinId { get; internal set; }
    public int SectionId { get; internal set; }
    public int GuestSectionId { get; internal set; }
    public int EyeId { get; internal set; } = -1;
    public string Name { get; internal set; } = string.Empty;
    public GatheringGroundKind Kind { get; internal set; }
    public GatheringGroundQuality Quality { get; internal set; }
    public int CenterTileId { get; internal set; }
    public int[] TileIds { get; internal set; }
    public int[] HallTileIds { get; internal set; }
    public string RegionName { get; internal set; } = string.Empty;
    public float Convergence { get; internal set; }
    public float Shelter { get; internal set; }
    public float Leakage { get; internal set; }
    public float FillRatio { get; internal set; }
    public float Purity { get; internal set; }
}
