using Cultiway.Core;

namespace Cultiway.Content.SpiritVeins;

/// <summary>按地块查询到的风水龙脉环境。</summary>
public readonly struct SpiritVeinLocalInfo
{
    public SpiritVeinLocalInfo(
        int veinId,
        int secondaryVeinId,
        int sectionId,
        int groundId,
        int eyeId,
        float fieldStrength,
        float secondaryStrength,
        float flowX,
        float flowY,
        float convergence,
        float shelter,
        float leakage,
        float fillRatio,
        float purity,
        ElementComposition composition,
        string veinName,
        string groundName,
        string eyeName)
    {
        VeinId = veinId;
        SecondaryVeinId = secondaryVeinId;
        SectionId = sectionId;
        GroundId = groundId;
        EyeId = eyeId;
        FieldStrength = fieldStrength;
        SecondaryStrength = secondaryStrength;
        FlowX = flowX;
        FlowY = flowY;
        Convergence = convergence;
        Shelter = shelter;
        Leakage = leakage;
        FillRatio = fillRatio;
        Purity = purity;
        Composition = composition;
        VeinName = veinName ?? string.Empty;
        GroundName = groundName ?? string.Empty;
        EyeName = eyeName ?? string.Empty;
    }

    public int VeinId { get; }
    public int SecondaryVeinId { get; }
    public int SectionId { get; }
    public int GroundId { get; }
    public int EyeId { get; }
    public float FieldStrength { get; }
    public float SecondaryStrength { get; }
    public float FlowX { get; }
    public float FlowY { get; }
    public float Convergence { get; }
    public float Shelter { get; }
    public float Leakage { get; }
    public float FillRatio { get; }
    public float Purity { get; }
    public ElementComposition Composition { get; }
    public string VeinName { get; }
    public string GroundName { get; }
    public string EyeName { get; }
    public bool HasInfluence => VeinId >= 0 && FieldStrength > 0f;
}
