using Cultiway.Core.SkillLibV3.ActiveAbilities;

namespace Cultiway.Core.SkillLibV3.Usage;

public sealed class SkillUseProfileLibrary : AssetLibrary<SkillUseProfileAsset>
{
    public static SkillUseProfileAsset EnemyObjectOrPoint { get; private set; }
    public static SkillUseProfileAsset EnemyPoint { get; private set; }
    public static SkillUseProfileAsset EnemyArea { get; private set; }
    public static SkillUseProfileAsset CasterSelf { get; private set; }
    public static SkillUseProfileAsset BetweenCasterAndEnemy { get; private set; }
    public static SkillUseProfileAsset FriendlyObject { get; private set; }
    public static SkillUseProfileAsset FriendlyArea { get; private set; }
    public static SkillUseProfileAsset WorldArea { get; private set; }

    public override void init()
    {
        base.init();
        EnemyObjectOrPoint = Add("EnemyObjectOrPoint", ActiveAbilityTargetMode.ObjectOrPoint,
            SkillUsePlacement.EnemyObjectOrPoint, 1, 0);
        EnemyPoint = Add("EnemyPoint", ActiveAbilityTargetMode.Point,
            SkillUsePlacement.EnemyPoint, 2, 0);
        EnemyArea = Add("EnemyArea", ActiveAbilityTargetMode.Area,
            SkillUsePlacement.EnemyPoint, 2, 0);
        CasterSelf = Add("CasterSelf", ActiveAbilityTargetMode.Self,
            SkillUsePlacement.CasterSelf, 1, 5);
        CasterSelf.TargetRelation = SkillUseTargetRelation.Self;
        BetweenCasterAndEnemy = Add("BetweenCasterAndEnemy", ActiveAbilityTargetMode.ObjectOrPoint,
            SkillUsePlacement.BetweenCasterAndEnemy, 2, 4);
        FriendlyObject = Add("FriendlyObject", ActiveAbilityTargetMode.Object,
            SkillUsePlacement.FriendlyObject, 2, 0);
        FriendlyObject.TargetRelation = SkillUseTargetRelation.Friendly;
        FriendlyObject.Multiplicity = SkillUseMultiplicity.Single;
        FriendlyObject.Channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World;
        FriendlyArea = Add("FriendlyArea", ActiveAbilityTargetMode.Area,
            SkillUsePlacement.FriendlyPoint, 2, 0);
        FriendlyArea.TargetRelation = SkillUseTargetRelation.Friendly;
        FriendlyArea.Multiplicity = SkillUseMultiplicity.Single;
        FriendlyArea.Channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World;
        WorldArea = Add("WorldArea", ActiveAbilityTargetMode.Area,
            SkillUsePlacement.WorldPoint, 0, 0);
        WorldArea.TargetRelation = SkillUseTargetRelation.WorldTile;
        WorldArea.Multiplicity = SkillUseMultiplicity.Single;
        WorldArea.Channels = ActiveAbilityChannel.World;
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
