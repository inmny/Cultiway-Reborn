using System;
using Cultiway.Core;
using UnityEngine;

namespace Cultiway.Content.SpiritVeins;

/// <summary>龙脉中独立储气、输送、衰弱和受污染的局部脉节。</summary>
public sealed class SpiritVeinSection
{
    internal SpiritVeinSection(
        int id,
        int veinId,
        int branchId,
        VeinSectionKind kind,
        int centerTileId,
        int[] tileIds,
        float capacity,
        float monthlyRecovery,
        float monthlySupply,
        float monthlyTransfer,
        float patency,
        ElementComposition composition)
    {
        Id = id;
        VeinId = veinId;
        BranchId = branchId;
        Kind = kind;
        CenterTileId = centerTileId;
        TileIds = tileIds ?? Array.Empty<int>();
        Capacity = Mathf.Max(1f, capacity);
        CurrentAmount = Capacity * 0.82f;
        MonthlyRecovery = Mathf.Max(0f, monthlyRecovery);
        MonthlySupply = Mathf.Max(0f, monthlySupply);
        MonthlyTransfer = Mathf.Max(0f, monthlyTransfer);
        Patency = Mathf.Clamp01(patency);
        Purity = 1f;
        Composition = composition;
        RefreshStatus();
    }

    public int Id { get; }
    public int VeinId { get; }
    public int BranchId { get; }
    public VeinSectionKind Kind { get; internal set; }
    public string RegionName { get; internal set; } = string.Empty;
    public int CenterTileId { get; internal set; }
    public int[] TileIds { get; internal set; }
    public int[] UpstreamSectionIds { get; internal set; } = Array.Empty<int>();
    public int[] DownstreamSectionIds { get; internal set; } = Array.Empty<int>();
    public float Capacity { get; internal set; }
    public float CurrentAmount { get; internal set; }
    public float MonthlyRecovery { get; internal set; }
    public float MonthlySupply { get; internal set; }
    public float MonthlyTransfer { get; internal set; }
    public float Patency { get; internal set; }
    public float Purity { get; internal set; }
    public ElementComposition Composition { get; internal set; }
    public VeinSectionStatus Status { get; internal set; }

    public float FillRatio => Capacity <= 0f ? 0f : Mathf.Clamp01(CurrentAmount / Capacity);
    public float EffectiveSupply => MonthlySupply * ResolveOutputMultiplier() * Patency * Mathf.Clamp01(Purity);

    internal float ResolveOutputMultiplier()
    {
        float ratio = FillRatio;
        if (ratio >= 0.65f) return 1f;
        if (ratio <= 0.08f) return 0.12f;
        return Mathf.Lerp(0.12f, 1f, (ratio - 0.08f) / 0.57f);
    }

    internal void RefreshStatus()
    {
        if (Patency <= 0.08f)
        {
            Status = VeinSectionStatus.Blocked;
            return;
        }

        float ratio = FillRatio;
        Status = ratio >= 0.85f
            ? VeinSectionStatus.Flourishing
            : ratio >= 0.6f
                ? VeinSectionStatus.Full
                : ratio >= 0.3f
                    ? VeinSectionStatus.Tight
                    : ratio >= 0.1f
                        ? VeinSectionStatus.Weak
                        : VeinSectionStatus.Exhausted;
    }
}
