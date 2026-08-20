using Cultiway.Core;
using UnityEngine;

namespace Cultiway.Content.SpiritVeins;

/// <summary>结穴地内灵气最集中的自然显化。</summary>
public sealed class SpiritVeinEye
{
    internal SpiritVeinEye(
        int id,
        int veinId,
        int groundId,
        int sectionId,
        int tileId,
        SpiritEyeManifestation manifestation,
        float baseConcentration,
        ElementComposition composition)
    {
        Id = id;
        VeinId = veinId;
        GroundId = groundId;
        SectionId = sectionId;
        TileId = tileId;
        Manifestation = manifestation;
        BaseConcentration = Mathf.Max(1f, baseConcentration);
        Composition = composition;
    }

    public int Id { get; }
    public int VeinId { get; }
    public int GroundId { get; }
    public int SectionId { get; internal set; }
    public int TileId { get; internal set; }
    public string Name { get; internal set; } = string.Empty;
    public SpiritEyeManifestation Manifestation { get; internal set; }
    public float BaseConcentration { get; internal set; }
    public ElementComposition Composition { get; internal set; }
    public float FillRatio { get; internal set; }
    public float Purity { get; internal set; }
    public SpiritEyeConcentration Concentration => ResolveConcentration(BaseConcentration * FillRatio * Purity);

    private SpiritEyeConcentration ResolveConcentration(float value)
    {
        float ratio = value / Mathf.Max(1f, BaseConcentration);
        if (ratio >= 0.92f && BaseConcentration >= 1800f) return SpiritEyeConcentration.Liquid;
        if (ratio >= 0.72f) return SpiritEyeConcentration.Mist;
        if (ratio >= 0.45f) return SpiritEyeConcentration.Rich;
        if (ratio >= 0.18f) return SpiritEyeConcentration.Gathered;
        return SpiritEyeConcentration.Thin;
    }
}
