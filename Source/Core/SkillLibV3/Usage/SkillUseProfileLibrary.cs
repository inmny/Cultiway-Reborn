using Cultiway.Core.SkillLibV3.ActiveAbilities;

namespace Cultiway.Core.SkillLibV3.Usage;

public sealed class SkillUseProfileLibrary : AssetLibrary<SkillUseProfileAsset>
{
    public static SkillUseProfileAsset EnemyObjectOrPoint { get; private set; }
    public static SkillUseProfileAsset EnemyPoint { get; private set; }
    public static SkillUseProfileAsset CasterSelf { get; private set; }
    public static SkillUseProfileAsset BetweenCasterAndEnemy { get; private set; }

    public override void init()
    {
        base.init();
        EnemyObjectOrPoint = Add("EnemyObjectOrPoint", ActiveAbilityTargetMode.ObjectOrPoint,
            SkillUsePlacement.EnemyObjectOrPoint, 1, 0);
        EnemyPoint = Add("EnemyPoint", ActiveAbilityTargetMode.Point,
            SkillUsePlacement.EnemyPoint, 2, 0);
        CasterSelf = Add("CasterSelf", ActiveAbilityTargetMode.Self,
            SkillUsePlacement.CasterSelf, 1, 5);
        BetweenCasterAndEnemy = Add("BetweenCasterAndEnemy", ActiveAbilityTargetMode.ObjectOrPoint,
            SkillUsePlacement.BetweenCasterAndEnemy, 2, 4);
    }

    private SkillUseProfileAsset Add(string name, ActiveAbilityTargetMode targetMode,
        SkillUsePlacement placement, int baseAiWeight, int threatenedAiWeight)
    {
        return add(new SkillUseProfileAsset
        {
            id = $"Cultiway.SkillUseProfile.{name}",
            TargetMode = targetMode,
            Placement = placement,
            BaseAiWeight = baseAiWeight,
            ThreatenedAiWeight = threatenedAiWeight
        });
    }
}
