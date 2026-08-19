using System;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using UnityEngine;

namespace Cultiway.Content.SpiritVeins;

public sealed partial class SpiritVeinManager
{
    /// <summary>按当前地势布局编号查找龙脉。</summary>
    public SpiritVein GetVeinByTopologyId(int topologyId)
    {
        return IsReady && veinByTopologyId.TryGetValue(topologyId, out SpiritVein vein) ? vein : null;
    }

    public SpiritVein GetVeinAtTile(int tileId)
    {
        if (!IsValidTileId(tileId)) return null;
        int topologyId = field.PrimaryVeinByTile[tileId];
        if (topologyId < 0) topologyId = field.SecondaryVeinByTile[tileId];
        return GetVeinByTopologyId(topologyId);
    }

    public SpiritVeinBranch GetBranch(int branchId)
    {
        return IsReady && branchById.TryGetValue(branchId, out SpiritVeinBranch branch) ? branch : null;
    }

    public SpiritVeinSection GetSection(int sectionId)
    {
        return IsReady && sectionById.TryGetValue(sectionId, out SpiritVeinSection section) ? section : null;
    }

    public GatheringGround GetGround(int groundId)
    {
        return IsReady && groundById.TryGetValue(groundId, out GatheringGround ground) ? ground : null;
    }

    public SpiritVeinEye GetEye(int eyeId)
    {
        return IsReady && eyeById.TryGetValue(eyeId, out SpiritVeinEye eye) ? eye : null;
    }

    public SpiritVeinSection GetSectionAtTile(int tileId)
    {
        return IsValidTileId(tileId) ? GetSection(field.SectionByTile[tileId]) : null;
    }

    public GatheringGround GetGroundAtTile(int tileId)
    {
        return IsValidTileId(tileId) ? GetGround(field.GroundByTile[tileId]) : null;
    }

    public SpiritVeinEye GetEyeAtTile(int tileId)
    {
        GatheringGround ground = GetGroundAtTile(tileId);
        return ground == null ? null : GetEye(ground.EyeId);
    }

    /// <summary>返回当前位置的龙脉、脉节、结穴地、行气和局部状态。</summary>
    public SpiritVeinLocalInfo GetLocalInfo(int tileId)
    {
        if (!IsValidTileId(tileId) || field.PrimaryVeinByTile[tileId] < 0) return EmptyLocalInfo();
        int topologyId = field.PrimaryVeinByTile[tileId];
        int secondaryTopologyId = field.SecondaryVeinByTile[tileId];
        SpiritVeinSection section = GetSection(field.SectionByTile[tileId]);
        GatheringGround ground = GetGround(field.GroundByTile[tileId]);
        SpiritVeinEye eye = ground == null ? null : GetEye(ground.EyeId);
        SpiritVein vein = GetVeinByTopologyId(topologyId);
        ElementComposition composition = section?.Composition ?? vein?.Composition ?? default;
        if (secondaryTopologyId >= 0 && sectionById.TryGetValue(
                field.SecondarySectionByTile[tileId], out SpiritVeinSection secondarySection))
        {
            float blend = field.SecondaryStrength[tileId] /
                          Mathf.Max(0.001f, field.FieldStrength[tileId] + field.SecondaryStrength[tileId]);
            composition = BlendComposition(composition, secondarySection.Composition, blend * 0.5f);
        }

        return new SpiritVeinLocalInfo(
            topologyId,
            secondaryTopologyId,
            section?.Id ?? -1,
            ground?.Id ?? -1,
            eye?.Id ?? -1,
            field.FieldStrength[tileId],
            field.SecondaryStrength[tileId],
            field.FlowX[tileId],
            field.FlowY[tileId],
            field.Convergence[tileId],
            field.Shelter[tileId],
            field.Leakage[tileId],
            section?.FillRatio ?? 0f,
            section?.Purity ?? 0f,
            composition,
            vein?.Name,
            ground?.Name,
            eye?.Name);
    }

    public SpiritVeinLocalInfo GetLocalInfo(WorldTile tile)
    {
        return tile?.data == null ? EmptyLocalInfo() : GetLocalInfo(tile.data.tile_id);
    }

    /// <summary>复制望气图所需的连续脉域；副本不会暴露内部可变数组。</summary>
    public SpiritVeinFieldSnapshot CreateFieldSnapshot()
    {
        if (!IsReady || field == null)
        {
            return new SpiritVeinFieldSnapshot(
                0,
                0,
                Array.Empty<int>(),
                Array.Empty<int>(),
                Array.Empty<int>(),
                Array.Empty<int>(),
                Array.Empty<int>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>());
        }

        return new SpiritVeinFieldSnapshot(
            field.Width,
            field.Height,
            (int[])field.PrimaryVeinByTile.Clone(),
            (int[])field.SecondaryVeinByTile.Clone(),
            (int[])field.SectionByTile.Clone(),
            (int[])field.SecondarySectionByTile.Clone(),
            (int[])field.GroundByTile.Clone(),
            (float[])field.FieldStrength.Clone(),
            (float[])field.SecondaryStrength.Clone(),
            (float[])field.FlowX.Clone(),
            (float[])field.FlowY.Clone(),
            (float[])field.Convergence.Clone(),
            (float[])field.Shelter.Clone(),
            (float[])field.Leakage.Clone());
    }

    /// <summary>计算灵根与当地元素的正向加成；不相合时不产生惩罚。</summary>
    public float GetElementMatchBonus(int tileId, ElementRoot root)
    {
        SpiritVeinLocalInfo local = GetLocalInfo(tileId);
        if (!local.HasInfluence) return 0f;
        float rootTotal = 0f;
        float overlap = 0f;
        for (int i = 0; i < ElementIndex.Count; i++)
        {
            float rootValue = Mathf.Max(0f, root[i]);
            rootTotal += rootValue;
            overlap += rootValue * Mathf.Max(0f, local.Composition[i]);
        }
        if (rootTotal <= 0f) return 0f;
        float influence = Mathf.Clamp01(local.FieldStrength + local.Convergence * 0.35f);
        return SpiritVeinSettings.MaximumCultivationBonus *
               Mathf.Clamp01(overlap / rootTotal) * influence * Mathf.Clamp01(local.Purity);
    }

    /// <summary>返回一处地点的长期来气与聚气价值，供宗门明堂选址使用。</summary>
    public float GetLongTermSupplyScore(int tileId)
    {
        if (!IsValidTileId(tileId)) return 0f;
        SpiritVeinSection section = GetSection(field.SectionByTile[tileId]);
        if (section == null) return 0f;
        float score = Mathf.Log10(section.MonthlySupply + section.MonthlyTransfer + 1f) *
                      field.FieldStrength[tileId] * Mathf.Lerp(0.25f, 1f, section.Purity);
        GatheringGround ground = GetGround(field.GroundByTile[tileId]);
        if (ground != null)
        {
            float quality = 1f + (int)ground.Quality * 0.22f;
            bool inHall = ContainsTile(ground.HallTileIds, tileId);
            score += quality * (ground.Convergence * 1.4f + ground.Shelter - ground.Leakage * 0.5f) *
                     (inHall ? 1.35f : 0.8f);
        }
        if (field.SecondarySectionByTile[tileId] >= 0 &&
            sectionById.TryGetValue(field.SecondarySectionByTile[tileId], out SpiritVeinSection guest))
        {
            score += Mathf.Log10(guest.MonthlySupply + 1f) * field.SecondaryStrength[tileId] * 0.3f;
        }
        return Mathf.Max(0f, score);
    }

    public SpiritVeinEye FindEyeNearTile(int tileId, int maximumDistance)
    {
        if (!IsValidTileId(tileId)) return null;
        GatheringGround direct = GetGroundAtTile(tileId);
        if (direct != null) return GetEye(direct.EyeId);
        SpiritVeinEye result = null;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < eyes.Count; i++)
        {
            int distance = TileDistance(tileId, eyes[i].TileId);
            if (distance > maximumDistance || distance >= bestDistance) continue;
            bestDistance = distance;
            result = eyes[i];
        }
        return result;
    }

    public GatheringGround FindGroundNearTile(int tileId, int maximumDistance)
    {
        if (!IsValidTileId(tileId)) return null;
        GatheringGround direct = GetGroundAtTile(tileId);
        if (direct != null) return direct;
        GatheringGround result = null;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < grounds.Count; i++)
        {
            int distance = TileDistance(tileId, grounds[i].CenterTileId);
            if (distance > maximumDistance || distance >= bestDistance) continue;
            bestDistance = distance;
            result = grounds[i];
        }
        return result;
    }

    private static bool ContainsTile(int[] values, int tileId)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == tileId) return true;
        }
        return false;
    }

    private int TileDistance(int left, int right)
    {
        if (width <= 0) return int.MaxValue;
        return Mathf.Abs(left % width - right % width) + Mathf.Abs(left / width - right / width);
    }

    private bool IsValidTileId(int tileId)
    {
        return IsReady && field != null && (uint)tileId < (uint)field.PrimaryVeinByTile.Length;
    }

    private static SpiritVeinLocalInfo EmptyLocalInfo()
    {
        return new SpiritVeinLocalInfo(
            -1,
            -1,
            -1,
            -1,
            -1,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            default,
            string.Empty,
            string.Empty,
            string.Empty);
    }

    private static ElementComposition BlendComposition(
        ElementComposition left,
        ElementComposition right,
        float rightWeight)
    {
        float weight = Mathf.Clamp01(rightWeight);
        var result = new ElementComposition();
        for (int i = 0; i < ElementIndex.Count; i++)
            result[i] = left[i] * (1f - weight) + right[i] * weight;
        result.Normalize();
        return result;
    }
}
