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
    /// <summary>使用其逐帧cast点的实际外观层；未接入的器官保留原来的肉身出口约定。</summary>
    public string CastingLayerId;
    public CreatureUpkeepDescriptor UpkeepDescriptor;
}
