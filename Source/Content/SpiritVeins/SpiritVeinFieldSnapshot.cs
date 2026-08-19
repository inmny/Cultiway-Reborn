namespace Cultiway.Content.SpiritVeins;

/// <summary>供望气图一次性读取的脉域数据副本。</summary>
public sealed class SpiritVeinFieldSnapshot
{
    internal SpiritVeinFieldSnapshot(
        int width,
        int height,
        int[] primaryVeinByTile,
        int[] secondaryVeinByTile,
        int[] sectionByTile,
        int[] secondarySectionByTile,
        int[] groundByTile,
        float[] fieldStrength,
        float[] secondaryStrength,
        float[] flowX,
        float[] flowY,
        float[] convergence,
        float[] shelter,
        float[] leakage)
    {
        Width = width;
        Height = height;
        PrimaryVeinByTile = primaryVeinByTile;
        SecondaryVeinByTile = secondaryVeinByTile;
        SectionByTile = sectionByTile;
        SecondarySectionByTile = secondarySectionByTile;
        GroundByTile = groundByTile;
        FieldStrength = fieldStrength;
        SecondaryStrength = secondaryStrength;
        FlowX = flowX;
        FlowY = flowY;
        Convergence = convergence;
        Shelter = shelter;
        Leakage = leakage;
    }

    public int Width { get; }
    public int Height { get; }
    public int[] PrimaryVeinByTile { get; }
    public int[] SecondaryVeinByTile { get; }
    public int[] SectionByTile { get; }
    public int[] SecondarySectionByTile { get; }
    public int[] GroundByTile { get; }
    public float[] FieldStrength { get; }
    public float[] SecondaryStrength { get; }
    public float[] FlowX { get; }
    public float[] FlowY { get; }
    public float[] Convergence { get; }
    public float[] Shelter { get; }
    public float[] Leakage { get; }
}
