namespace Cultiway.Content.CreatureCompositions.Libraries;

/// <summary>定义器官某一级的固定数值、能力编号和外观资料。</summary>
public sealed class CreatureOrganRankAsset : Asset
{
    public int Rank;
    public int ComplexityCost;
    public CreatureStatValue[] StatValues = [];
    public string[] SkillContainerIds = [];
    public CreatureEffectRank[] EffectRanks = [];
    public string[] VisualLayerIds = [];
    public CreatureUpkeepDescriptor UpkeepDescriptor;
}
