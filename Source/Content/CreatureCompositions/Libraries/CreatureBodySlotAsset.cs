namespace Cultiway.Content.CreatureCompositions.Libraries;

/// <summary>定义一个身体位置能够容纳的器官类别和容量。</summary>
public sealed class CreatureBodySlotAsset : Asset
{
    public CreatureOrganCategoryMask AcceptedCategoryMask = CreatureOrganCategoryMask.All;
    public int Capacity = 1;
    public CreatureSymmetryMode SymmetryMode;
    public string VisualChannel;
    public bool Required;
}
