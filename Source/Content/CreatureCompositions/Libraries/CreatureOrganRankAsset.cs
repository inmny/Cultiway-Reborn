namespace Cultiway.Content.CreatureCompositions.Libraries;

/// <summary>没有独立外观的器官借用同一具身体的真实施放出口。</summary>
internal enum CreatureOrganBodyOutlet { None, Mouth, Torso }

/// <summary>定义器官某一级的固定数值、能力编号和外观资料。</summary>
public sealed class CreatureOrganRankAsset : Asset
{
    public int Rank;
    public int ComplexityCost;
    public CreatureStatValue[] StatValues = [];
    public string[] SkillContainerIds = [];
    public CreatureEffectRank[] EffectRanks = [];
    public string[] VisualLayerIds = [];
    /// <summary>提供制作出口的实际外观层。</summary>
    public string CastingLayerId;
    internal CreatureOrganBodyOutlet BodyOutlet;
    public CreatureUpkeepDescriptor UpkeepDescriptor;
}
