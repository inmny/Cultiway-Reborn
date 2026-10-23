using Cultiway.Core.SkillLibV3.ActiveAbilities;

namespace Cultiway.Core.SkillLibV3.Usage;

public enum SkillUsePlacement
{
    EnemyObjectOrPoint,
    EnemyPoint,
    CasterSelf,
    BetweenCasterAndEnemy,
    FriendlyObject,
    FriendlyPoint,
    WorldPoint,
}

/// <summary>能力在控制层允许选择的目标关系。</summary>
public enum SkillUseTargetRelation
{
    /// <summary>敌对对象。</summary>
    Hostile = 0,

    /// <summary>友方对象，包含使用者本人。</summary>
    Friendly = 1,

    /// <summary>仅使用者本人。</summary>
    Self = 2,

    /// <summary>世界地块。</summary>
    WorldTile = 3,
}

/// <summary>一次决策是否允许展开为自适应连发。</summary>
public enum SkillUseMultiplicity
{
    Adaptive,
    Single,
}

/// <summary>
/// 法术向 AI 与玩家控制层公开的目标模式和使用倾向。
/// </summary>
public class SkillUseProfileAsset : Asset
{
    public ActiveAbilityTargetMode TargetMode;
    public SkillUsePlacement Placement;
    public SkillUseTargetRelation TargetRelation = SkillUseTargetRelation.Hostile;
    public SkillUseMultiplicity Multiplicity = SkillUseMultiplicity.Adaptive;
    public ActiveAbilityChannel Channels = ActiveAbilityChannel.Combat;
    public int BaseAiWeight = 1;
    public int ThreatenedAiWeight;
    public float RangeMultiplier = 1f;

    /// <summary>大于零时，实际效果半径会按面积比增加单步资源需求。</summary>
    public float AreaCostBaseRadius;
}
