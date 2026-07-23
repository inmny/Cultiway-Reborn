using Cultiway.Core.SkillLibV3.ActiveAbilities;

namespace Cultiway.Core.SkillLibV3.Usage;

public enum SkillUsePlacement
{
    EnemyObjectOrPoint,
    EnemyPoint,
    CasterSelf,
    BetweenCasterAndEnemy
}

/// <summary>
/// 法术向 AI 与玩家控制层公开的目标模式和使用倾向。
/// </summary>
public class SkillUseProfileAsset : Asset
{
    public ActiveAbilityTargetMode TargetMode;
    public SkillUsePlacement Placement;
    public int BaseAiWeight = 1;
    public int ThreatenedAiWeight;
    public float RangeMultiplier = 1f;
}
